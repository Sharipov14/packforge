using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal sealed class PackageDetailsPage
{
    private readonly AppState _state;
    private readonly UpdateService _updates;
    private readonly Action<AppPage> _navigate;
    private readonly IPackageService _packageService;
    private readonly ISystemInterop _systemInterop;

    internal PackageDetailsPage(AppState state, UpdateService updates, Action<AppPage> navigate, IPackageService packageService, ISystemInterop systemInterop)
    {
        _state = state;
        _updates = updates;
        _navigate = navigate;
        _packageService = packageService;
        _systemInterop = systemInterop;
    }

    internal Visual Build()
    {
        return new ComputedVisual(() => BuildDetails(_state.SelectedPackage.Value)).Stretch();
    }

    private void OpenDocs(PackageRow pkg, IPackageProvider? provider)
    {
        var homepage = _state.SelectedDoc.Value?.Homepage;
        var url = (homepage is not null &&
                   homepage.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            ? homepage
            : provider?.PackageUrl(pkg.Name);

        if (string.IsNullOrWhiteSpace(url))
        {
            _state.Notify($"No documentation URL for {pkg.Name}", ToastSeverity.Warning);
            return;
        }

        _systemInterop.OpenUrl(url);
        _state.Notify($"Opening documentation: {url}", ToastSeverity.Info);
    }

    private void SetCommand(string cmd)
    {
        _state.CommandInput.Value = cmd;
        _state.Notify($"Command set: {cmd}", ToastSeverity.Info);
    }

    private Visual BuildDetails(PackageRow? pkg)
    {
        if (pkg is null)
        {
            return new VStack(
                new Markup("[dim]No package selected. Go to Dashboard and click a package.[/]"),
                new Button("BACK TO DASHBOARD")
                    .Style(Widgets.PrimaryButtonStyle)
                    .Click(() => _navigate(AppPage.Dashboard)))
                .Spacing(Layout.Section)
                .Pad(Layout.PagePad);
        }

        var inst = _state.Packages.Value.FirstOrDefault(p =>
            p.ManagerId.Equals(pkg.ManagerId, StringComparison.OrdinalIgnoreCase) &&
            p.Name.Equals(pkg.Name, StringComparison.OrdinalIgnoreCase));
        bool isInstalled = inst is not null;
        bool outdated = inst?.Outdated ?? false;

        var provider = _packageService.Providers
            .FirstOrDefault(p => p.Id.Equals(pkg.ManagerId, StringComparison.OrdinalIgnoreCase));

        var chipTone = !isInstalled ? ChipTone.Available
                     : outdated     ? ChipTone.Outdated
                     :                ChipTone.Installed;

        var versionLine = isInstalled
            ? $"[dim]VERSION:[/] {inst!.Installed} → [primary]{inst.Latest}[/]"
            : $"[dim]VERSION:[/] [dim]not installed[/] → [primary]{pkg.Latest}[/]";

        var header = new VStack(
                new HStack(
                        new Markup($"[dim]/usr/bin/[/][bold primary]{pkg.Name}[/]  "),
                        Widgets.StatusChip(chipTone))
                    .Spacing(0),
                new Markup(versionLine))
            .Spacing(0);

        var actions = BuildActions(pkg, provider, isInstalled, outdated);

        var installDate = provider is not null
            ? SystemInfo.BinaryModified(provider.Command) ?? "—"
            : "—";
        var architecture = SystemInfo.Architecture();

        // ── metadata quadrant — single Panel wrapping a 3×2 Star grid of borderless stat cells ──
        var innerMetaGrid = new Grid()
            .Rows(
                new RowDefinition { Height = GridLength.Star(1) },
                new RowDefinition { Height = GridLength.Star(1) })
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) })
            .ColumnGap(Layout.Gap)
            .RowGap(Layout.Section)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("LICENSE", _state.SelectedDoc.Value?.License ?? pkg.License, ControlTone.Default))
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 0)
            .Cell(Widgets.StatCard("ARCHITECTURE", architecture, ControlTone.Default)
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 1)
            .Cell(Widgets.StatCard("INSTALL_DATE", installDate, ControlTone.Default)
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 2)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("SIZE", _state.SelectedDoc.Value?.Size ?? pkg.Size, ControlTone.Default))
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 1, 0)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("SOURCE", _state.SelectedDoc.Value?.Homepage ?? pkg.Source, ControlTone.Default))
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 1, 1)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("LAST_UPDATED", _state.SelectedDoc.Value?.LastModified ?? "—", ControlTone.Default))
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 1, 2)
            .HorizontalAlignment(Align.Stretch);

        var metadataPanel = Widgets.Panel(" metadata ", PanelAccent.Info, innerMetaGrid)
            .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch);

        // ── dependency_tree quadrant ──────────────────────────────────────────────────────────────
        var dependencyTree = BuildDependencyTree(pkg)
            .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch);

        // ── documentation quadrant — tabs fixed at top, content scrolls inside the panel ─────────
        Visual DocTab(string label, DetailsDocTab target)
            => new Button(new Markup(() =>
                    _state.DocTab.Value == target ? $"[bold]{label}[/]" : label))
                .Style(() => _state.DocTab.Value == target
                    ? ButtonStyle.Default with
                    {
                        ShowBorder = false,
                        Padding    = Layout.FieldPad,
                        Normal     = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Hovered    = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                        Pressed    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Focused    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    }
                    : ButtonStyle.Default with { Padding = Layout.FieldPad })
                .Click(() => _state.DocTab.Value = target);

        var docTabs = new HStack(
                DocTab("README.md",    DetailsDocTab.Readme),
                DocTab("CHANGELOG.md", DetailsDocTab.Changelog))
            .Spacing(Layout.Gap);

        var docContent = new ComputedVisual(() => BuildDocContent(pkg));

        var docPanel = Widgets.Panel(" documentation ", PanelAccent.Info,
                new VStack(docTabs, new ScrollViewer(docContent).Stretch()).Spacing(Layout.Section))
            .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch);

        // ── right-bottom quadrant — equal stacked sub-panels in a Star-row nested Grid ──────────
        var isNpm = pkg.ManagerId.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
                    pkg.ManagerId.Equals("yarn", StringComparison.OrdinalIgnoreCase);

        Visual rightBottomPanel;
        if (isNpm)
        {
            var histPanel = new ComputedVisual(() =>
            {
                if (_state.IsDocLoading.Value)
                    return (Visual)Widgets.Panel(" download_activity (30d) ", PanelAccent.Primary,
                        Widgets.Loader("fetching stats…", () => _state.IsDocLoading.Value, ControlTone.Primary));
                var histValues = GenerateHistValues(pkg.Name, 30);
                return (Visual)Widgets.Panel(" download_activity (30d) ", PanelAccent.Primary,
                    new Markup($"[primary]{Widgets.Histogram(histValues, 30)}[/]"));
            });

            var (usagePanel, systemPanel) = BuildPackageMetricPanels();
            rightBottomPanel = new Grid()
                .Rows(
                    new RowDefinition { Height = GridLength.Star(1) },
                    new RowDefinition { Height = GridLength.Star(1) },
                    new RowDefinition { Height = GridLength.Star(1) })
                .Columns(new ColumnDefinition { Width = GridLength.Star(1) })
                .RowGap(Layout.Section)
                .Cell(histPanel.VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 0)
                .Cell(usagePanel.VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 1, 0)
                .Cell(systemPanel.VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 2, 0)
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch);
        }
        else
        {
            var (usagePanel, systemPanel) = BuildPackageMetricPanels();
            rightBottomPanel = new Grid()
                .Rows(
                    new RowDefinition { Height = GridLength.Star(1) },
                    new RowDefinition { Height = GridLength.Star(1) })
                .Columns(new ColumnDefinition { Width = GridLength.Star(1) })
                .RowGap(Layout.Section)
                .Cell(usagePanel.VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 0, 0)
                .Cell(systemPanel.VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch), 1, 0)
                .VerticalAlignment(Align.Stretch).HorizontalAlignment(Align.Stretch);
        }

        bool narrow = _state.Viewport.Value.Columns < Layout.NarrowWidth;
        if (narrow)
        {
            var tabs = Widgets.PanelTabs(_state.PanelTab,
                ("metadata", () => metadataPanel),
                ("deps",     () => dependencyTree),
                ("docs",     () => docPanel),
                ("metrics",  () => rightBottomPanel));
            return new DockLayout()
                .Top(new VStack(header, actions).Spacing(Layout.Section).Pad(Layout.PagePad))
                .Content(tabs.Pad(Layout.PagePad).Stretch())
                .Stretch();
        }

        // ── 2×2 Star main grid — equal width AND height for all four quadrants ─────────────────
        var mainGrid = new Grid()
            .Rows(
                new RowDefinition { Height = GridLength.Star(1) },
                new RowDefinition { Height = GridLength.Star(1) })
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(7) },
                new ColumnDefinition { Width = GridLength.Star(3) })
            .ColumnGap(Layout.Gap)
            .RowGap(Layout.Section)
            .Cell(metadataPanel,    0, 0)
            .Cell(dependencyTree,   0, 1)
            .Cell(docPanel,         1, 0)
            .Cell(rightBottomPanel, 1, 1)
            .HorizontalAlignment(Align.Stretch)
            .VerticalAlignment(Align.Stretch);

        // ── DockLayout: header+actions pinned at top, panel grid fills the rest ────────────────
        return new DockLayout()
            .Top(new VStack(header, actions).Spacing(Layout.Section).Pad(Layout.PagePad))
            .Content(new ScrollViewer(mainGrid.Pad(Layout.PagePad)).Stretch())
            .Stretch();
    }

    private Visual BuildDocContent(PackageRow pkg)
    {
        if (_state.IsDocLoading.Value)
            return Widgets.Loader("Fetching documentation…", () => _state.IsDocLoading.Value, ControlTone.Primary);

        var doc = _state.SelectedDoc.Value;

        if (_state.DocTab.Value == DetailsDocTab.Readme)
        {
            if (doc is null)
            {
                return new TextBlock("Documentation not available.") { Wrap = true };
            }

            var items = new List<Visual>
            {
                new Markup($"[bold primary]{doc.Name}[/]"),
                new TextBlock(Widgets.Truncate(doc.Description, 300)) { Wrap = true },
                new Markup($"[dim]Homepage:[/] {doc.Homepage}"),
                new Markup($"[dim]License:[/] {doc.License}"),
                new Markup($"[dim]Latest Version:[/] {doc.LatestVersion}"),
            };

            if (doc.Dependencies.Count > 0)
            {
                items.Add(new Markup("[dim]Dependencies:[/]"));
                foreach (var dep in doc.Dependencies)
                    items.Add(new Markup($"  [primary]■[/] {dep}"));
            }
            else
            {
                items.Add(new Markup("[dim]Dependencies:[/] (none)"));
            }

            return new VStack([.. items]).Spacing(Layout.Section);
        }
        else
        {
            if (doc is null || doc.RecentVersions.Count == 0)
                return new TextBlock("No version history available.") { Wrap = true };

            var items = new List<Visual> { new Markup("[bold primary]RECENT VERSIONS[/]") };
            foreach (var ver in doc.RecentVersions)
                items.Add(new Markup($"  [dim]■[/] {ver}"));

            return new VStack([.. items]).Spacing(Layout.Section);
        }
    }

    private WrapHStack BuildActions(PackageRow pkg, IPackageProvider? provider, bool isInstalled, bool outdated)
    {
        var buttons = new List<Visual>();

        if (!isInstalled)
        {
            buttons.Add(provider is not null
                ? (Visual)new Button("INSTALL").Style(Widgets.PrimaryButtonStyle)
                    .Click(() => SetCommand(provider.InstallPackageCommand(pkg.Name)))
                : new Markup("[dim](no provider)[/]"));
        }
        else
        {
            if (outdated)
            {
                buttons.Add(new Button("UPDATE_PACKAGE").Style(Widgets.PrimaryButtonStyle)
                    .Click(() => _updates.StartUpdate(pkg)));
            }

            if (provider is not null)
            {
                var prov = provider;
                var name = pkg.Name;
                buttons.Add(new Button("REINSTALL")
                    .Click(() => SetCommand(prov.InstallPackageCommand(name))));
                buttons.Add(new Button("UNINSTALL").Style(Widgets.PastelRedButtonStyle)
                    .Click(() => SetCommand(prov.RemovePackageCommand(name))));
            }
        }

        buttons.Add(new Button("DOCS").Style(Widgets.PastelBlueButtonStyle).Click(() => OpenDocs(pkg, provider)));
        buttons.Add(new Button("BACK").Click(() => _navigate(AppPage.Dashboard)));

        return new WrapHStack([.. buttons]).Spacing(Layout.Item);
    }

    private static Visual BuildDependencyTree(PackageRow pkg)
    {
        var root = new TreeNode(new Markup($"[primary]{pkg.Name}[/]"))
        {
            IsExpanded = true,
        };

        if (pkg.Dependencies.Length > 0)
        {
            foreach (var dep in pkg.Dependencies)
                root.Children.Add(new TreeNode(new TextBlock(dep)));
        }
        else
        {
            root.Children.Add(new TreeNode(new Markup("[dim](no dependencies recorded)[/]")));
        }

        return Widgets.Panel(" dependency_tree ", PanelAccent.Info,
            new TreeView([root]));
    }

    private static IReadOnlyList<double> GenerateHistValues(string seed, int days)
    {
        var rng = new Random(seed.GetHashCode());
        return Enumerable.Range(0, days)
            .Select(_ => rng.NextDouble() * 100_000 + 10_000)
            .ToList();
    }

    /// <summary>
    /// Returns the two right-bottom sub-panels as a tuple so callers can place them
    /// individually in a stretched Star-row Grid instead of an uneven VStack.
    /// </summary>
    private (Visual usagePanel, Visual systemPanel) BuildPackageMetricPanels()
    {
        var usagePanel = Widgets.Panel(" usage_examples ", PanelAccent.Success,
            new Markup(
                """
                [dim]# INSTALL[/]
                $ [success]pkg_mgr install <package>[/]

                [dim]# UPDATE[/]
                $ [success]pkg_mgr upgrade <package>[/]
                """));

        var systemPanel = Widgets.Panel(" system_status ", PanelAccent.Warning,
            new VStack(
                    Widgets.MeterRow("cpu_usage", () => _state.CpuUsage.Value),
                    Widgets.MeterRow("mem_usage", () => _state.RamUsage.Value,
                        () => $"{_state.RamUsage.Value * 16:F1} / 16 GB"))
                .Spacing(Layout.Section));

        return (usagePanel, systemPanel);
    }
}
