using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal sealed class DashboardPage
{
    private readonly AppState _state;
    private readonly UpdateService _updates;
    private readonly Action<AppPage> _navigate;
    private readonly IPackageService _packageService;

    internal DashboardPage(AppState state, UpdateService updates, Action<AppPage> navigate, IPackageService packageService)
    {
        _state = state;
        _updates = updates;
        _navigate = navigate;
        _packageService = packageService;
    }

    internal Visual Build()
    {
        var stats = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .ColumnGap(Layout.Gap)
            .Cell(BuildPackagesCard().VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 0)
            .Cell(BuildOutdatedCard().VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 1)
            .Cell(BuildSystemLoadCard().VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 2)
            .Cell(BuildStorageCard().VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 3)
            .HorizontalAlignment(Align.Stretch);

        var tableContainer = new ComputedVisual(() => BuildTable()).Stretch();
        var tableScrollViewer = new ScrollViewer(tableContainer).Stretch();

        var promptText = SystemInfo.Prompt();
        var prompt = Widgets.Panel(" command ", PanelAccent.Warning,
            new VStack(
                    new Markup($"[success]{promptText}[/] $ [bold]pkgr update --all[/]"),
                    new ComputedVisual(() =>
                    {
                        if (_state.IsLoading.Value)
                            return (Visual)Widgets.Loader("scanning managers…", () => _state.IsLoading.Value, ControlTone.Primary);
                        if (_state.IsCommandRunning.Value)
                            return (Visual)Widgets.Loader("running update…", () => _state.IsCommandRunning.Value, ControlTone.Warning);
                        return (Visual)new TextBlock("> ready; select a package or run UPDATE_ALL");
                    }))
                .Spacing(Layout.Section));

        bool narrow = _state.Viewport.Value.Columns < Layout.NarrowWidth;
        if (narrow)
        {
            return Widgets.PanelTabs(_state.PanelTab,
                ("packages", () => tableScrollViewer),
                ("stats",    () => stats.Pad(Layout.PagePad)));
        }

        return new DockLayout()
            .Top(stats.Pad(Layout.PagePad))
            .Content(tableScrollViewer)
            //.Bottom(prompt)
            .Stretch();
    }

    private Visual BuildPackagesCard()
    {
        return Widgets.Panel(" packages ", PanelAccent.Primary,
            new VStack(
                    new Markup("[dim]installed[/]"),
                    new ComputedVisual(() =>
                    {
                        var all = GetFilteredPackages();
                        var cyanHex = $"#{Palette.Cyan.R:x2}{Palette.Cyan.G:x2}{Palette.Cyan.B:x2}";
                        return (Visual)new Markup($"[bold {cyanHex}]{all.Count}[/]");
                    }))
                .Spacing(Layout.Section));
    }

    private Visual BuildOutdatedCard()
    {
        return Widgets.Panel(" outdated ", PanelAccent.Warning,
            new VStack(
                    new Markup("[dim]need update[/]"),
                    new ComputedVisual(() =>
                    {
                        var outdated = GetFilteredPackages().Count(p => p.Outdated);
                        var amberHex = $"#{Palette.Amber.R:x2}{Palette.Amber.G:x2}{Palette.Amber.B:x2}";
                        return (Visual)new Markup($"[bold {amberHex}]{outdated}[/]");
                    }))
                .Spacing(Layout.Section));
    }

    private Visual BuildSystemLoadCard()
    {
        return Widgets.Panel(" system_load ", PanelAccent.Success,
            new VStack(
                    new ComputedVisual(() =>
                    {
                        var pct = (int)(_state.CpuUsage.Value * 100);
                        var greenHex = $"#{Palette.Green.R:x2}{Palette.Green.G:x2}{Palette.Green.B:x2}";
                        return (Visual)new Markup($"[dim]cpu[/]  [bold {greenHex}]{pct,3}%[/]");
                    }),
                    Widgets.Meter(() => _state.CpuUsage.Value, Layout.MeterWidth))
                .Spacing(Layout.Section));
    }

    private static Visual BuildStorageCard()
    {
        var (fraction, label) = SystemInfo.DiskUsage();
        return Widgets.Panel(" storage ", PanelAccent.Store,
            new VStack(
                    new Markup($"[dim]disk[/]  [bold]{label}[/]"),
                    Widgets.Meter(fraction, Layout.MeterWidth))
                .Spacing(Layout.Section));
    }

    private Visual BuildTable()
    {
        var rows = GetFilteredPackages();

        var amberHex      = $"#{Palette.Amber.R:x2}{Palette.Amber.G:x2}{Palette.Amber.B:x2}";
        var outlineHex    = $"#{Palette.OutlineVariant.R:x2}{Palette.OutlineVariant.G:x2}{Palette.OutlineVariant.B:x2}";

        var table = new Table()
            .Headers("Package Name", "Installed", "Latest", "Status", "Action")
            .Style(TableStyle.RoundedGrid with
            {
                HeaderStyle = Style.None.WithForeground(Palette.Amber) | TextStyle.Bold,
                BorderStyle = Style.None.WithForeground(Palette.OutlineVariant),
            })
            .HorizontalAlignment(Align.Stretch);

        if (rows.Count == 0)
        {
            if (_state.IsLoading.Value)
            {
                table.AddRow(
                    Widgets.Loader("Scanning managers…", () => _state.IsLoading.Value, ControlTone.Primary),
                    new TextBlock("—"),
                    new TextBlock("—"),
                    new TextBlock("—"),
                    new TextBlock("—"));
            }
            else
            {
                table.AddRow(
                    new Markup("[dim]No packages found for selected manager.[/]"),
                    new TextBlock("—"),
                    new TextBlock("—"),
                    new TextBlock("—"),
                    new TextBlock("—"));
            }
            return table;
        }

        var focusedIdx = _state.DashboardFocusIndex.Value;
        var inTableZone = _state.KeyboardFocus.Value == FocusZone.Table;
        foreach (var (pkg, rowIdx) in rows.Select((p, i) => (p, i)))
        {
            var selected = pkg;
            var isFocused = rowIdx == focusedIdx && inTableZone;
            var nameStyle = isFocused
                ? ButtonStyle.Default with
                {
                    Padding  = Thickness.Zero,
                    Normal   = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    Hovered  = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                }
                : ButtonStyle.Default with
                {
                    Padding  = Thickness.Zero,
                    Normal   = Style.None.WithForeground(Palette.Cyan),
                    Hovered  = Style.None.WithForeground(Palette.CyanBright),
                };
            table.AddRow(
                new Button(new Markup($"{Widgets.Truncate(pkg.Name, Layout.NameColMax)}"))
                    .Style(nameStyle)
                    .Click(() =>
                    {
                        _state.SelectedPackage.Value = selected;
                        _navigate(AppPage.PackageDetails);
                    }),
                pkg.Installed,
                pkg.Latest,
                Widgets.StatusChip(pkg.Outdated),
                pkg.Outdated
                    ? (Visual)new Button("UPDATE")
                        .Tone(ControlTone.Warning)
                        .Click(() => SetUpdateCommand(selected))
                    : new Markup("[success]✓[/]"));
        }

        return table;
    }

    private void SetUpdateCommand(PackageRow pkg)
    {
        var provider = _packageService.Providers
            .FirstOrDefault(p => p.Id.Equals(pkg.ManagerId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            _state.Notify($"Unknown manager: {pkg.ManagerId}", ToastSeverity.Error);
            return;
        }

        var command = provider.UpdatePackageCommand(pkg.Name);
        _state.CommandInput.Value = command;
        _state.Notify($"Command set: {command}", ToastSeverity.Info);
    }

    private IReadOnlyList<PackageRow> GetFilteredPackages()
    {
        var all = _state.Packages.Value;
        var managerId = _state.ActiveManagerId.Value;
        var query = _state.SearchQuery.Value ?? string.Empty;

        var filtered = all.Where(p =>
            p.ManagerId.Equals(managerId, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return filtered;
    }
}
