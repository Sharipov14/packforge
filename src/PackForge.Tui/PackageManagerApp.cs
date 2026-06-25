using PackForge.Core;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Commands;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PackForge.Tui;

public sealed class PackageManagerApp
{
    private readonly AppState _state;
    private readonly UpdateService _updates;
    private readonly CommandService _commands;
    private readonly IPackageService _packageService;
    private readonly IConfigStore _configStore;
    private readonly ISystemInterop _systemInterop;
    private readonly IProcessRunner _processRunner;
    private readonly Dictionary<AppPage, Visual> _pageCache = [];
    private readonly CommandPalette _commandPalette = new();
    private PackageRow? _lastDocTarget;

    public PackageManagerApp(
        IPackageService packageService,
        IConfigStore configStore,
        ISystemInterop systemInterop,
        IProcessRunner processRunner)
    {
        _packageService = packageService;
        _configStore = configStore;
        _systemInterop = systemInterop;
        _processRunner = processRunner;

        _state = new AppState(configStore, packageService);
        _commands = new CommandService(_state, Navigate, processRunner);
        _updates = new UpdateService(_state, Navigate, _commands, packageService);
    }

    public void Run()
    {
        // Apply the persisted palette before building any visuals.
        Palette.Apply(_state.ThemeVariant.Value);
        _state.Viewport.Value = Terminal.Size;

        // Wrap the shell in a stable root so commands and KeyDown survive theme-driven rebuilds.
        var root = new DockLayout()
            .Content(new ComputedVisual(() =>
            {
                // Reading these makes the ComputedVisual re-run on theme OR size changes.
                _ = _state.ThemeVariant.Value;
                var size = _state.Viewport.Value;
                if (size.Columns < Layout.MinWidth || size.Rows < Layout.MinHeight)
                    return BuildTooSmall(size);
                return BuildShell(size.Columns < Layout.NarrowWidth);
            }).Stretch())
            .Stretch();

        RegisterCommands(root);

        root.KeyDown((_, e) =>
        {
            switch (e.Key)
            {
                case TerminalKey.Escape:
                    _state.KeyboardFocus.Value = FocusZone.None;
                    Navigate(AppPage.Dashboard);
                    e.Handled = true;
                    break;

                case TerminalKey.Tab:
                    _state.KeyboardFocus.Value = _state.KeyboardFocus.Value switch
                    {
                        FocusZone.None => FocusZone.Sidebar,
                        FocusZone.Sidebar => FocusZone.Table,
                        _ => FocusZone.None,
                    };
                    e.Handled = true;
                    break;

                case TerminalKey.Up when _state.KeyboardFocus.Value == FocusZone.Sidebar:
                    NavigateSidebar(-1);
                    e.Handled = true;
                    break;
                case TerminalKey.Down when _state.KeyboardFocus.Value == FocusZone.Sidebar:
                    NavigateSidebar(+1);
                    e.Handled = true;
                    break;
                case TerminalKey.Up when _state.KeyboardFocus.Value == FocusZone.Table:
                    NavigateTable(-1);
                    e.Handled = true;
                    break;
                case TerminalKey.Down when _state.KeyboardFocus.Value == FocusZone.Table:
                    NavigateTable(+1);
                    e.Handled = true;
                    break;
            }
        });

        _state.ToastHost.Content(root);
        _state.ToastHost.Style(AppTheme.Create());

        _state.IsLoading.Value = true;
        _packageService.BeginRefresh();

        Terminal.Run(_state.ToastHost, Update);
    }

    /// <summary>
    /// Swaps the palette, clears the page cache, re-applies the theme to the toast host,
    /// updates the shared state (which triggers the reactive shell rebuild), and persists the
    /// new variant to <c>~/.config/pkg_mgr/config.json</c>.
    /// </summary>
    private void ReloadTheme(AppThemeVariant v)
    {
        Palette.Apply(v);
        _pageCache.Clear();
        _state.ToastHost.Style(AppTheme.Create());
        _state.ThemeVariant.Value = v;
        _configStore.Save(_state.BuildConfigSnapshot());
    }

    private void RegisterCommands(Visual root)
    {
        void Nav(string id, string label, TerminalKey key, AppPage page)
            => root.AddCommand(new Command
            {
                Id = id,
                Name = label.ToLowerInvariant(),
                LabelMarkup = label,
                Gesture = new KeyGesture(key),
                Importance = CommandImportance.Primary,
                Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
                Execute = _ => Navigate(page),
            });

        Nav("App.Nav.Dashboard", "Dashboard", TerminalKey.F1, AppPage.Dashboard);
        Nav("App.Nav.Details", "Details", TerminalKey.F2, AppPage.PackageDetails);
        Nav("App.Nav.Logs", "Logs", TerminalKey.F3, AppPage.UpdateLogs);
        Nav("App.Nav.Settings", "Settings", TerminalKey.F4, AppPage.SourceSettings);
        Nav("App.Nav.Store", "Store", TerminalKey.F5, AppPage.Store);

        root.AddCommand(new Command
        {
            Id = "App.UpdateAll",
            Name = "update-all",
            LabelMarkup = "Update All",
            Gesture = new KeyGesture(TerminalKey.F6),
            Importance = CommandImportance.Primary,
            Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
            Execute = _ => _updates.StartUpdate(),
        });

        root.AddCommand(new Command
        {
            Id = "App.CommandPalette",
            Name = "command-palette",
            LabelMarkup = "Command Palette",
            Gesture = new KeyGesture(TerminalChar.CtrlP, TerminalModifiers.Ctrl),
            Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
            Execute = _ => _commandPalette.Show(),
        });

        root.AddCommand(new Command
        {
            Id = "App.Help",
            Name = "help",
            LabelMarkup = "Help",
            Gesture = new KeyGesture(TerminalKey.F12),
            Presentation = CommandPresentation.CommandBar | CommandPresentation.CommandPalette,
            Execute = _ => _state.Notify(
                "F1-F5 Pages · F6 Update All · Ctrl+P Palette · Tab = cycle zone (Sidebar/Table) · ↑↓ Navigate · Enter Select",
                ToastSeverity.Info),
        });

        root.AddCommand(new Command
        {
            Id = "App.Exit",
            Name = "exit",
            LabelMarkup = "Exit",
            Presentation = CommandPresentation.CommandPalette,
            Execute = _ => _state.ExitRequested.Value = true,
        });

        // Enter for keyboard navigation: fires in command-dispatch phase (before KeyDown reaches
        // the focused Button), so it intercepts Enter only when a navigation zone is active.
        // ConsumesGestureWhenUnavailable = false lets Enter fall through to normal handling
        // (TextBox execute, Button click) when no zone is active.
        root.AddCommand(new Command
        {
            Id = "App.Nav.Confirm",
            Name = "nav-confirm",
            LabelMarkup = string.Empty,
            Gesture = new KeyGesture(TerminalKey.Enter),
            Presentation = CommandPresentation.None,
            Importance = CommandImportance.Tertiary,
            ConsumesGestureWhenUnavailable = false,
            CanExecute = _ => _state.KeyboardFocus.Value != FocusZone.None,
            Execute = _ =>
            {
                if (_state.KeyboardFocus.Value == FocusZone.Sidebar) ActivateSidebarItem();
                else if (_state.KeyboardFocus.Value == FocusZone.Table) ActivateTableItem();
            },
        });
    }

    private TerminalLoopResult Update()
    {
        var vp = Terminal.Size;
        if (vp.Columns != _state.Viewport.Value.Columns || vp.Rows != _state.Viewport.Value.Rows)
            _state.Viewport.Value = vp;

        if (_state.ExitRequested.Value)
            return TerminalLoopResult.Stop;

        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_state.LastTick, now) < TimeSpan.FromMilliseconds(75))
            return TerminalLoopResult.Continue;

        _state.LastTick = now;

        if (_packageService.HasPendingResult)
        {
            var result = _packageService.TakePendingResult();
            _state.Packages.Value = result.Packages.Count > 0
                ? result.Packages
                : PackageCatalog.FallbackRows;
            _state.InstalledFlags.Value = result.InstalledFlags;
            _state.IsLoading.Value = false;
            _pageCache.Remove(AppPage.Dashboard);
            _pageCache.Remove(AppPage.SourceSettings);
        }

        if (_packageService.HasPendingSearch)
        {
            _state.SearchResults.Value = _packageService.TakePendingSearch();
            _state.IsSearching.Value = false;
        }

        var sel = _state.SelectedPackage.Value;
        if (!ReferenceEquals(sel, _lastDocTarget))
        {
            _lastDocTarget = sel;
            if (sel is not null)
            {
                _state.IsDocLoading.Value = true;
                _state.SelectedDoc.Value = null;
                _packageService.BeginDocFetch(sel);
            }
        }

        if (_packageService.HasPendingDoc)
        {
            var (name, doc) = _packageService.TakePendingDoc();
            if (_state.SelectedPackage.Value?.Name == name)
            {
                _state.SelectedDoc.Value = doc;
                _state.IsDocLoading.Value = false;
            }
        }

        _updates.TickMetrics();
        _commands.Pump();
        return TerminalLoopResult.Continue;
    }

    private Visual BuildTooSmall(TerminalSize size)
    {
        string w = size.Columns < Layout.MinWidth ? $"[error]{size.Columns}[/]" : $"{size.Columns}";
        string h = size.Rows    < Layout.MinHeight ? $"[error]{size.Rows}[/]"    : $"{size.Rows}";

        var msg = new VStack(
                new Markup("[bold]Terminal size too small:[/]").HorizontalAlignment(Align.Center),
                new Markup($"Width {w}  Height {h}").HorizontalAlignment(Align.Center),
                new TextBlock(" "),
                new Markup("[bold]Needed for current config:[/]").HorizontalAlignment(Align.Center),
                new Markup($"Width {Layout.MinWidth}  Height {Layout.MinHeight}").HorizontalAlignment(Align.Center))
            .Spacing(0)
            .HorizontalAlignment(Align.Center)
            .VerticalAlignment(Align.Center);

        return new DockLayout().Content(msg.Stretch()).Stretch();
    }

    private Visual BuildShell(bool isNarrow)
    {
        // Clear the page cache to ensure visuals don't have stale parents from the previous tree.
        _pageCache.Clear();

        if (isNarrow)
        {
            var top = new VStack(
                    BuildCompactHeader(),
                    BuildManagerStrip(),
                    new ComputedVisual(() => Widgets.TopTabs(_state.Page.Value, Navigate)))
                .Spacing(Layout.Item);

            return new Grid()
                .Rows(
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Star(1) },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto })
                .Columns(new ColumnDefinition { Width = GridLength.Star(1) })
                .Cell(top.Pad(Layout.FieldPad), 0, 0)
                .Cell(new DockLayout()
                        .Content(new ComputedVisual(() => GetPage(_state.Page.Value)).Stretch())
                        .Bottom(BuildCommandBar())
                        .Stretch(), 1, 0)
                .Cell(new CommandBar().HorizontalAlignment(Align.Stretch), 2, 0)
                .Cell(BuildStatusBar(), 3, 0)
                .Stretch();
        }

        var shell = new Grid()
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star(1) },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Columns(
                new ColumnDefinition { Width = GridLength.Fixed(Layout.SidebarWidth) },
                new ColumnDefinition { Width = GridLength.Star(1) })
            .Cell(BuildHeader(), 0, 0, columnSpan: 2)
            .Cell(BuildSidebar(), 1, 0)
            .Cell(new DockLayout()
                    .Content(new ComputedVisual(() => GetPage(_state.Page.Value)).Stretch())
                    .Bottom(BuildCommandBar())
                    .Stretch(), 1, 1)
            .Cell(new CommandBar().HorizontalAlignment(Align.Stretch), 2, 0, columnSpan: 2)
            .Cell(BuildStatusBar(), 3, 0, columnSpan: 2)
            .Stretch();

        return shell;
    }

    private Visual BuildHeader()
    {
        var leftSection = new VStack(
                new Markup("[bold primary]PKG_MGR[/]"),
                new Markup(() => $"[dim]Active Session: {GetActiveManagerName()}[/]")
                    { Wrap = false })
            .Spacing(0);

        var centerSection = new ComputedVisual(() =>
            Widgets.TopTabs(_state.Page.Value, Navigate));

        var searchCol = new ColumnDefinition { Width = GridLength.Star(1) }.MaxWidth(Layout.SearchMaxWidth);
        var rightSection = new HStack(
                new ComputedVisual(() =>
                {
                    var outdated = _state.Packages.Value.Count(p => p.Outdated);
                    return outdated > 0
                        ? Widgets.Badge($"⚠ {outdated} updates available", ControlTone.Warning)
                        : Widgets.Badge("● All packages current", ControlTone.Success);
                }),
                new Grid()
                    .Columns(searchCol)
                    .Rows(new RowDefinition { Height = GridLength.Auto })
                    .Cell(new TextBox(_state.SearchQuery).HorizontalAlignment(Align.Stretch), 0, 0))
            .Spacing(Layout.Item);

        return new Header
        {
            Left = leftSection,
            Center = centerSection,
            Right = rightSection,
        };
    }

    private string GetActiveManagerName()
    {
        var id = _state.ActiveManagerId.Value;
        return _packageService.Providers.FirstOrDefault(p => p.Id == id)?.DisplayName ?? id;
    }

    private Visual BuildCompactHeader()
    {
        var searchCol = new ColumnDefinition { Width = GridLength.Star(1) }.MaxWidth(Layout.SearchMaxWidth);
        return new HStack(
                new Markup("[bold primary]PKG_MGR[/]"),
                new ComputedVisual(() =>
                {
                    var outdated = _state.Packages.Value.Count(p => p.Outdated);
                    return outdated > 0
                        ? Widgets.Badge($"⚠ {outdated} updates", ControlTone.Warning)
                        : Widgets.Badge("● current", ControlTone.Success);
                }),
                new Grid()
                    .Columns(searchCol)
                    .Rows(new RowDefinition { Height = GridLength.Auto })
                    .Cell(new TextBox(_state.SearchQuery).HorizontalAlignment(Align.Stretch), 0, 0))
            .Spacing(Layout.Item)
            .HorizontalAlignment(Align.Stretch);
    }

    private Visual BuildManagerStrip()
    {
        return new ComputedVisual(() =>
        {
            var flags = _state.InstalledFlags.Value;
            var visibility = _state.ManagerVisibility.Value;
            var activeId = _state.ActiveManagerId.Value;
            var packages = _state.Packages.Value;

            var visibleProviders = _packageService.Providers
                .Where(p => (flags.TryGetValue(p.Id, out var inst) && inst)
                         && (visibility.TryGetValue(p.Id, out var v) ? v : true))
                .ToList();

            var currentActiveId = visibleProviders.Any(p => p.Id == activeId)
                ? activeId
                : visibleProviders.FirstOrDefault()?.Id;

            var items = visibleProviders.Select(p =>
            {
                var isActive = currentActiveId == p.Id;
                var id = p.Id;
                var outdatedCount = packages.Count(pkg =>
                    pkg.ManagerId.Equals(p.Id, StringComparison.OrdinalIgnoreCase) && pkg.Outdated);
                return (Visual)Widgets.ManagerNavItem(
                    p.Id, p.DisplayName, isActive, true,
                    () => SelectManager(id), outdatedCount);
            }).ToArray();

            return (Visual)new WrapHStack([.. items]).Spacing(Layout.Item).HorizontalAlignment(Align.Start);
        });
    }

    private Visual BuildSidebar()
    {
        var loadingSpinner = Widgets.Loader("scanning…", () => _state.IsLoading.Value, ControlTone.Primary);

        var managerList = new ComputedVisual(() =>
        {
            var flags = _state.InstalledFlags.Value;
            var visibility = _state.ManagerVisibility.Value;
            var activeId = _state.ActiveManagerId.Value;
            var packages = _state.Packages.Value;

            var visibleProviders = _packageService.Providers
                .Where(p => (flags.TryGetValue(p.Id, out var inst) && inst)
                         && (visibility.TryGetValue(p.Id, out var v) ? v : true))
                .ToList();

            var currentActiveId = visibleProviders.Any(p => p.Id == activeId)
                ? activeId
                : visibleProviders.FirstOrDefault()?.Id;
            var sidebarFocusIdx = _state.SidebarFocusIndex.Value;
            var inSidebarZone = _state.KeyboardFocus.Value == FocusZone.Sidebar;
            var items = visibleProviders.Select((p, i) =>
            {
                var isInstalled = flags.TryGetValue(p.Id, out var inst) && inst;
                var isActive = currentActiveId == p.Id;
                var id = p.Id;
                var outdatedCount = packages.Count(pkg =>
                    pkg.ManagerId.Equals(p.Id, StringComparison.OrdinalIgnoreCase) && pkg.Outdated);
                return Widgets.ManagerNavItem(
                    p.Id,
                    p.DisplayName,
                    isActive,
                    isInstalled,
                    () => SelectManager(id),
                    outdatedCount,
                    isKeyboardFocused: i == sidebarFocusIdx && inSidebarZone);
            }).ToArray();

            return (Visual)new VStack(items).Spacing(Layout.Item);
        });

        var sidebarScrollViewer = new ScrollViewer(
            new VStack(
                    new Markup("[dim]Active Repositories[/]"),
                    loadingSpinner,
                    managerList)
                .Spacing(Layout.Section));

        return Widgets.Panel(" managers ", PanelAccent.Primary,
                new DockLayout()
                    .Content(sidebarScrollViewer)
                    .Bottom(new VStack(
                            new Rule(),
                            new Button("UPDATE_ALL")
                                .HorizontalAlignment(Align.Stretch)
                                .Click(() => _updates.StartUpdate()),
                            new Button("Help")
                                .HorizontalAlignment(Align.Stretch)
                                .Click(() => _state.Notify("F1-F5 Pages · F6 Update All · Ctrl+P Palette · Tab = cycle zone (Sidebar/Table) · ↑↓ Navigate · Enter Select", ToastSeverity.Info)),
                            new Button("Exit")
                                .Style(Widgets.PastelRedButtonStyle)
                                .HorizontalAlignment(Align.Stretch)
                                .Click(() => _state.ExitRequested.Value = true))
                        .Spacing(Layout.Item)))
            .Stretch();
    }

    private void SelectManager(string managerId)
    {
        _state.ActiveManagerId.Value = managerId;
        _state.DashboardFocusIndex.Value = -1;
        _pageCache.Remove(AppPage.Dashboard);
        Navigate(AppPage.Dashboard);
        var flags = _state.InstalledFlags.Value;
        if (flags.TryGetValue(managerId, out var installed) && installed)
        {
            _state.IsLoading.Value = true;
            _packageService.BeginRefresh();
        }
    }

    private IReadOnlyList<IPackageProvider> GetVisibleProviders()
    {
        var flags = _state.InstalledFlags.Value;
        var visibility = _state.ManagerVisibility.Value;
        return _packageService.Providers
            .Where(p => (flags.TryGetValue(p.Id, out var inst) && inst)
                     && (visibility.TryGetValue(p.Id, out var v) ? v : true))
            .ToList();
    }

    private void NavigateSidebar(int delta)
    {
        var providers = GetVisibleProviders();
        if (providers.Count == 0) return;
        _state.SidebarFocusIndex.Value = Math.Clamp(_state.SidebarFocusIndex.Value + delta, 0, providers.Count - 1);
    }

    private void ActivateSidebarItem()
    {
        var providers = GetVisibleProviders();
        var idx = _state.SidebarFocusIndex.Value;
        if (idx >= 0 && idx < providers.Count)
            SelectManager(providers[idx].Id);
    }

    private IReadOnlyList<PackageRow> GetDashboardRows()
    {
        var all = _state.Packages.Value;
        var managerId = _state.ActiveManagerId.Value;
        var query = _state.SearchQuery.Value ?? string.Empty;
        var filtered = all.Where(p =>
            p.ManagerId.Equals(managerId, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(query))
            filtered = filtered.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        return filtered;
    }

    private void NavigateTable(int delta)
    {
        var rows = GetDashboardRows();
        if (rows.Count == 0) return;
        var idx = _state.DashboardFocusIndex.Value;
        _state.DashboardFocusIndex.Value = delta > 0
            ? Math.Min(rows.Count - 1, idx < 0 ? 0 : idx + 1)
            : Math.Max(0, idx <= 0 ? 0 : idx - 1);
    }

    private void ActivateTableItem()
    {
        var rows = GetDashboardRows();
        var idx = _state.DashboardFocusIndex.Value;
        if (idx >= 0 && idx < rows.Count)
        {
            _state.SelectedPackage.Value = rows[idx];
            Navigate(AppPage.PackageDetails);
        }
    }

    private Visual BuildCommandBar()
    {
        var input = new TextBox(_state.CommandInput)
            .HorizontalAlignment(Align.Stretch)
            .KeyDown((_, e) =>
            {
                if (e.Key == TerminalKey.Enter)
                {
                    _commands.Execute(_state.CommandInput.Value ?? string.Empty);
                    e.Handled = true;
                }
            });

        return Widgets.Panel(" command ", PanelAccent.Warning,
                new HStack(
                        new Markup("[primary]> [/]"),
                        input)
                    .Spacing(0)
                    .HorizontalAlignment(Align.Stretch))
            .HorizontalAlignment(Align.Stretch);
    }

    private Visual BuildStatusBar()
    {
        return new StatusBar()
            .LeftText(new TextBlock(() => GetLeftStatusText()))
            .RightText(new TextBlock(() => GetRightStatusText()));
    }

    private string GetLeftStatusText()
    {
        var zone = _state.KeyboardFocus.Value switch
        {
            FocusZone.Sidebar => " · [Sidebar ↑↓]",
            FocusZone.Table => " · [Table ↑↓]",
            _ => " · Tab to navigate",
        };
        return _state.Page.Value switch
        {
            AppPage.Dashboard =>
                _state.IsLoading.Value
                    ? "> scanning managers..."
                    : $"SYSTEM_OK | {_state.Packages.Value.Count} packages loaded{zone}",
            AppPage.PackageDetails => $"PACKAGE_VIEW | {_state.SelectedPackage.Value?.Name ?? "—"}",
            AppPage.UpdateLogs =>
                _state.IsCommandRunning.Value
                    ? "ACTIVE_UPDATE | running..."
                    : "UPDATE_READY | No active transactions",
            AppPage.SourceSettings => "MODE: CONFIGURATION   ENCODING: UTF-8",
            _ => _state.Status.Value,
        };
    }

    private string GetRightStatusText()
    {
        return _state.Page.Value switch
        {
            AppPage.SourceSettings =>
                $"{_packageService.Providers.Count(p => _state.InstalledFlags.Value.TryGetValue(p.Id, out var inst) && inst)} sources · SYSTEM_NORMAL",
            AppPage.UpdateLogs => $"Elapsed: {_state.UpdateClock.Elapsed:hh\\:mm\\:ss}",
            _ => $"MANAGER: {_state.ActiveManagerId.Value.ToUpper()}",
        };
    }

    private Visual GetPage(AppPage page)
    {
        if (page == AppPage.SourceSettings)
            return new SourceSettingsPage(_state, Navigate, _packageService, _configStore, _systemInterop, ReloadTheme).Build();

        // Replace the LogControl's panel content with a placeholder before rebuild to properly
        // detach the shared LogControl from its old tree (avoids "already has a parent" crash)
        if (page == AppPage.UpdateLogs && _state.UpdateLog.Parent is Group logPanel)
            logPanel.Content(new Markup(""));

        if (_pageCache.TryGetValue(page, out var cached))
            return cached;

        var visual = page switch
        {
            AppPage.Dashboard => new DashboardPage(_state, _updates, Navigate, _packageService).Build(),
            AppPage.PackageDetails => new PackageDetailsPage(_state, _updates, Navigate, _packageService, _systemInterop).Build(),
            AppPage.UpdateLogs => new UpdateLogsPage(_state, _updates).Build(),
            AppPage.Store => new StorePage(_state, _packageService, Navigate).Build(),
            _ => new DashboardPage(_state, _updates, Navigate, _packageService).Build(),
        };

        _pageCache.Add(page, visual);
        return visual;
    }

    private void Navigate(AppPage page)
    {
        _state.PanelTab.Value = 0;
        _state.Page.Value = page;
        var title = Widgets.PageTitle(page, page == AppPage.PackageDetails ? _state.SelectedPackage.Value?.Name : null);
        _state.Status.Value = $"SYSTEM_OK | {title}";
    }
}
