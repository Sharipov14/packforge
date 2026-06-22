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
                    .Tone(ControlTone.Primary)
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

        var metadata = new Grid()
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Star(1) })
            .ColumnGap(Layout.Gap)
            .RowGap(Layout.Section)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("LICENSE", _state.SelectedDoc.Value?.License ?? pkg.License, ControlTone.Primary)), 0, 0)
            .Cell(Widgets.StatCard("ARCHITECTURE", architecture, ControlTone.Default), 0, 1)
            .Cell(Widgets.StatCard("INSTALL_DATE", installDate, ControlTone.Default), 0, 2)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("SIZE", _state.SelectedDoc.Value?.Size ?? pkg.Size, ControlTone.Success)), 1, 0)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("SOURCE", _state.SelectedDoc.Value?.Homepage ?? pkg.Source, ControlTone.Default)), 1, 1)
            .Cell(new ComputedVisual(() =>
                Widgets.StatCard("LAST_UPDATED", _state.SelectedDoc.Value?.LastModified ?? "—", ControlTone.Default)), 1, 2)
            .HorizontalAlignment(Align.Stretch);

        var dependencyTree = BuildDependencyTree(pkg);

        var docTabs = new HStack(
                new Button(new Markup(() =>
                    _state.DocTab.Value == DetailsDocTab.Readme ? "[bold primary]README.md[/]" : "[dim]README.md[/]"))
                    .Style(() => ButtonStyle.Default with
                    {
                        ShowBorder = _state.DocTab.Value == DetailsDocTab.Readme,
                        Padding = Layout.FieldPad,
                    })
                    .Click(() => _state.DocTab.Value = DetailsDocTab.Readme),
                new Button(new Markup(() =>
                    _state.DocTab.Value == DetailsDocTab.Changelog ? "[bold primary]CHANGELOG.md[/]" : "[dim]CHANGELOG.md[/]"))
                    .Style(() => ButtonStyle.Default with
                    {
                        ShowBorder = _state.DocTab.Value == DetailsDocTab.Changelog,
                        Padding = Layout.FieldPad,
                    })
                    .Click(() => _state.DocTab.Value = DetailsDocTab.Changelog))
            .Spacing(Layout.Gap);

        var docContent = new ComputedVisual(() => BuildDocContent(pkg));

        var docPanel = new Group()
            .TopLeftText(" DOCUMENTATION ")
            .Padding(Layout.GroupPad)
            .Content(new VStack(docTabs, docContent).Spacing(Layout.Section));

        var isNpm = pkg.ManagerId.Equals("npm", StringComparison.OrdinalIgnoreCase) ||
                    pkg.ManagerId.Equals("yarn", StringComparison.OrdinalIgnoreCase);
        Visual rightBottomPanel;
        if (isNpm)
        {
            var histPanel = new ComputedVisual(() =>
            {
                if (_state.IsDocLoading.Value)
                    return (Visual)new Group()
                        .TopLeftText(" DOWNLOAD_ACTIVITY (30d) ")
                        .Padding(Layout.GroupPad)
                        .Content(Widgets.Loader("fetching stats…", () => _state.IsDocLoading.Value, ControlTone.Primary));
                var histValues = GenerateHistValues(pkg.Name, 30);
                return (Visual)new Group()
                    .TopLeftText(" DOWNLOAD_ACTIVITY (30d) ")
                    .Padding(Layout.GroupPad)
                    .Content(new Markup($"[primary]{Widgets.Histogram(histValues, 30)}[/]"));
            });
            rightBottomPanel = new VStack(histPanel, BuildPackageMetrics()).Spacing(Layout.Section);
        }
        else
        {
            rightBottomPanel = BuildPackageMetrics();
        }

        var layout = new Grid()
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(2) },
                new ColumnDefinition { Width = GridLength.Star(1) }.MinWidth(Layout.RightColMin))
            .ColumnGap(Layout.Gap)
            .RowGap(Layout.Section)
            .Cell(new VStack(header, actions).Spacing(Layout.Section), 0, 0, columnSpan: 2)
            .Cell(metadata, 1, 0)
            .Cell(dependencyTree, 1, 1)
            .Cell(docPanel, 2, 0)
            .Cell(rightBottomPanel, 2, 1)
            .HorizontalAlignment(Align.Stretch);

        return new ScrollViewer(layout.Pad(Layout.PagePad)).Stretch();
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
                ? (Visual)new Button("INSTALL").Tone(ControlTone.Primary)
                    .Click(() => SetCommand(provider.InstallPackageCommand(pkg.Name)))
                : new Markup("[dim](no provider)[/]"));
        }
        else
        {
            if (outdated)
            {
                buttons.Add(new Button("UPDATE_PACKAGE").Tone(ControlTone.Primary)
                    .Click(() => _updates.StartUpdate(pkg)));
            }

            if (provider is not null)
            {
                var prov = provider;
                var name = pkg.Name;
                buttons.Add(new Button("REINSTALL")
                    .Click(() => SetCommand(prov.InstallPackageCommand(name))));
                buttons.Add(new Button("UNINSTALL").Tone(ControlTone.Error)
                    .Click(() => SetCommand(prov.RemovePackageCommand(name))));
            }
        }

        buttons.Add(new Button("DOCS").Click(() => OpenDocs(pkg, provider)));
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

        return new Group()
            .TopLeftText(" DEPENDENCY_TREE ")
            .Padding(Layout.GroupPad)
            .Content(new TreeView([root]));
    }

    private static IReadOnlyList<double> GenerateHistValues(string seed, int days)
    {
        var rng = new Random(seed.GetHashCode());
        return Enumerable.Range(0, days)
            .Select(_ => rng.NextDouble() * 100_000 + 10_000)
            .ToList();
    }

    private Visual BuildPackageMetrics()
    {
        return new VStack(
                new Group()
                    .TopLeftText(" USAGE_EXAMPLES ")
                    .Padding(Layout.GroupPad)
                    .Content(new Markup(
                        """
                        [dim]# INSTALL[/]
                        $ [success]pkg_mgr install <package>[/]

                        [dim]# UPDATE[/]
                        $ [success]pkg_mgr upgrade <package>[/]
                        """)),
                new Group()
                    .TopLeftText(" SYSTEM_STATUS ")
                    .Padding(Layout.GroupPad)
                    .Content(new VStack(
                            new TextBlock(() => $"CPU_USAGE                 {(int)(_state.CpuUsage.Value * 100),6}%"),
                            new ProgressBar().Value(() => _state.CpuUsage.Value).Style(ProgressBarStyle.Thin),
                            new TextBlock(() => $"MEM_USAGE        {_state.RamUsage.Value * 16,6:F1} / 16 GB"),
                            new ProgressBar().Value(() => _state.RamUsage.Value).Style(ProgressBarStyle.Thin))
                        .Spacing(Layout.Section)))
            .Spacing(Layout.Section);
    }
}
