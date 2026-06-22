using PackForge.Core;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal sealed class StorePage
{
    private readonly AppState _state;
    private readonly IPackageService _packageService;
    private readonly Action<AppPage> _navigate;

    private const int RowLimit = 8;
    private readonly Dictionary<string, State<bool>> _expanded = new();

    internal StorePage(AppState state, IPackageService packageService, Action<AppPage> navigate)
    {
        _state = state;
        _packageService = packageService;
        _navigate = navigate;
    }

    private State<bool> Expanded(string key)
        => _expanded.TryGetValue(key, out var s) ? s : (_expanded[key] = new State<bool>(false));

    internal Visual Build()
    {
        var searchBar = BuildSearchBar();
        var results = new ComputedVisual(() => BuildProviderGroups()).Stretch();

        return new DockLayout()
            .Top(searchBar.Pad(Layout.FieldPad))
            .Content(new ScrollViewer(results).Stretch())
            .Stretch();
    }

    private PackageRow? FindInstalled(string managerId, string name)
        => _state.Packages.Value.FirstOrDefault(p =>
            p.ManagerId.Equals(managerId, StringComparison.OrdinalIgnoreCase) &&
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void SetCommand(string cmd)
    {
        _state.CommandInput.Value = cmd;
        _state.Notify($"Command set: {cmd}", ToastSeverity.Info);
    }

    private static ChipTone StatusFor(PackageRow? inst)
        => inst is null ? ChipTone.Available : inst.Outdated ? ChipTone.Outdated : ChipTone.Installed;

    private bool HasContent(IPackageProvider p, bool showInstalled)
    {
        if (showInstalled)
            return _state.IsLoading.Value ||
                   _state.Packages.Value.Any(r => r.ManagerId.Equals(p.Id, StringComparison.OrdinalIgnoreCase));
        if (_state.IsSearching.Value) return true;
        if (p.Id == "pip") return false;
        return _state.SearchResults.Value.Any(r => r.ManagerId.Equals(p.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static Visual ToggleButton(State<bool> exp, int total)
        => new Button(new Markup(() => exp.Value
                ? "Collapse"
                : $"Show all (+{total - RowLimit})"))
            .Click(() => exp.Value = !exp.Value);

    private Visual ActionCell(IPackageProvider provider, string name, PackageRow? inst)
    {
        if (inst is null)
        {
            return (Visual)new Button("INSTALL")
                .Tone(ControlTone.Primary)
                .Click(() => SetCommand(provider.InstallPackageCommand(name)));
        }

        if (inst.Outdated)
        {
            return (Visual)new HStack(
                    new Button("UPDATE")
                        .Tone(ControlTone.Warning)
                        .Click(() => SetCommand(provider.UpdatePackageCommand(name))),
                    new Button("REMOVE")
                        .Tone(ControlTone.Error)
                        .Click(() => SetCommand(provider.RemovePackageCommand(name))))
                .Spacing(Layout.Item);
        }

        return (Visual)new Button("REMOVE")
            .Tone(ControlTone.Error)
            .Click(() => SetCommand(provider.RemovePackageCommand(name)));
    }

    private Visual BuildSearchBar()
    {
        var searchBox = new TextBox(_state.StoreQuery)
            .HorizontalAlignment(Align.Stretch)
            .KeyDown((_, e) =>
            {
                if (e.Key == TerminalKey.Enter)
                {
                    TriggerSearch();
                    e.Handled = true;
                }
            });

        var searchButton = new Button("SEARCH")
            .Tone(ControlTone.Primary)
            .Click(TriggerSearch);

        var status = new ComputedVisual(() =>
        {
            if (_state.IsSearching.Value)
                return (Visual)Widgets.Loader($"Searching {_state.StoreQuery.Value ?? string.Empty}…",
                    () => _state.IsSearching.Value, ControlTone.Primary);
            if (string.IsNullOrWhiteSpace(_state.StoreQuery.Value))
                return (Visual)new TextBlock("Enter a query and press Enter or SEARCH");
            return (Visual)new TextBlock($"{_state.SearchResults.Value.Count} result(s) for \"{_state.StoreQuery.Value}\"");
        });

        return new Group()
            .TopLeftText(" STORE — Search Package Registries ")
            .Padding(Layout.FieldPad)
            .Content(new VStack(
                    new HStack(
                            new Markup("[primary]>[/] "),
                            searchBox,
                            searchButton)
                        .Spacing(Layout.Item)
                        .HorizontalAlignment(Align.Stretch),
                    status)
                .Spacing(Layout.Section));
    }

    private void TriggerSearch()
    {
        var query = _state.StoreQuery.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query)) return;
        _state.IsSearching.Value = true;
        _packageService.BeginSearch(query);
    }

    private Visual BuildProviderGroups()
    {
        var query = _state.StoreQuery.Value ?? string.Empty;
        var showInstalled = string.IsNullOrWhiteSpace(query);

        var groups = _packageService.Providers
            .Where(p => p.IsInstalled() && HasContent(p, showInstalled))
            .Select(p => BuildProviderGroup(p, showInstalled))
            .ToArray<Visual>();

        if (groups.Length == 0)
        {
            return new Group()
                .Padding(Layout.GroupPad)
                .Content(new Markup("[dim]No package managers detected.[/]"));
        }

        return new VStack(groups).Spacing(Layout.Section).Stretch();
    }

    private Visual BuildProviderGroup(IPackageProvider provider, bool showInstalled)
    {
        var content = new ComputedVisual(() => BuildProviderContent(provider, showInstalled));

        return new Group()
            .TopLeftText($" {provider.DisplayName.ToUpper()} ")
            .Padding(Layout.GroupPad)
            .Content(content)
            .HorizontalAlignment(Align.Stretch);
    }

    private Visual BuildProviderContent(IPackageProvider provider, bool showInstalled)
    {
        if (!showInstalled && provider.Id == "pip")
        {
            return new Markup("[dim]pip search not supported on PyPI.[/]");
        }

        var key = $"{provider.Id}:{(showInstalled ? "inst" : "search")}";

        if (showInstalled)
        {
            return BuildInstalledList(provider, key);
        }
        else
        {
            return BuildSearchResultsList(provider, key);
        }
    }

    private Visual BuildPackageRow(string name, string versionText, ChipTone status, string? description, Visual actions, Action onOpen)
    {
        var nameButton = new Button(new Markup($"[primary]{Widgets.Truncate(name, Layout.NameColMax)}[/]"))
            .Style(ButtonStyle.Default with { Padding = Thickness.Zero })
            .Click(onOpen);

        var topLine = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(new HStack(nameButton, new Markup($"[dim]{versionText}[/]")).Spacing(Layout.Gap), 0, 0)
            .Cell(Widgets.StatusChip(status), 0, 1)
            .HorizontalAlignment(Align.Stretch);

        Visual descVisual = description is not null
            ? (Visual)new TextBlock(Widgets.Truncate(description, Layout.DescColMax)) { Wrap = false }
            : (Visual)new TextBlock("");

        var bottomLine = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(1) },
                new ColumnDefinition { Width = GridLength.Auto })
            .Rows(new RowDefinition { Height = GridLength.Auto })
            .Cell(descVisual, 0, 0)
            .Cell(actions, 0, 1)
            .HorizontalAlignment(Align.Stretch);

        return new VStack(topLine, bottomLine)
            .Spacing(0)
            .HorizontalAlignment(Align.Stretch)
            .Pad(new Thickness(1, 0, 0, 0));
    }

    private Visual BuildPackageList(IReadOnlyList<Visual> rows, Visual? toggle)
    {
        var items = new List<Visual>();
        for (int i = 0; i < rows.Count; i++)
        {
            items.Add(rows[i]);
            if (i < rows.Count - 1)
                items.Add(new Rule());
        }
        if (toggle is not null)
            items.Add(toggle);

        return new VStack([.. items]).Spacing(Layout.Item).HorizontalAlignment(Align.Stretch);
    }

    private Visual BuildInstalledList(IPackageProvider provider, string key)
    {
        var rows = _state.Packages.Value
            .Where(p => p.ManagerId.Equals(provider.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (rows.Count == 0)
        {
            return new Markup("[dim]No installed packages.[/]");
        }

        var exp = Expanded(key);
        var shown = exp.Value ? rows : rows.Take(RowLimit).ToList();

        var mapped = shown.Select(pkg =>
        {
            var prov = provider;
            var selected = pkg;
            var versionText = pkg.Outdated ? $"v{pkg.Installed} → v{pkg.Latest}" : $"v{pkg.Installed}";
            return BuildPackageRow(
                pkg.Name,
                versionText,
                StatusFor(selected),
                description: null,
                ActionCell(prov, pkg.Name, selected),
                () => { _state.SelectedPackage.Value = selected; _navigate(AppPage.PackageDetails); });
        }).ToList();

        Visual? toggle = rows.Count > RowLimit ? ToggleButton(exp, rows.Count) : null;
        return BuildPackageList(mapped, toggle);
    }

    private Visual BuildSearchResultsList(IPackageProvider provider, string key)
    {
        if (_state.IsSearching.Value)
        {
            return Widgets.Loader($"Searching {provider.DisplayName}…",
                () => _state.IsSearching.Value, ControlTone.Primary);
        }

        var results = _state.SearchResults.Value
            .Where(r => r.ManagerId.Equals(provider.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (results.Count == 0)
        {
            return new Markup("[dim]No results.[/]");
        }

        var exp = Expanded(key);
        var shown = exp.Value ? results : results.Take(RowLimit).ToList();

        var mapped = shown.Select(result =>
        {
            var prov = provider;
            var inst = FindInstalled(result.ManagerId, result.Name);
            var row = new PackageRow(
                Name: result.Name,
                Manager: result.Manager,
                ManagerId: result.ManagerId,
                Installed: "—",
                Latest: result.Version,
                Outdated: false,
                Source: result.Manager);
            var selected = row;
            return BuildPackageRow(
                result.Name,
                $"v{result.Version}",
                StatusFor(inst),
                result.Description,
                ActionCell(prov, result.Name, inst),
                () => { _state.SelectedPackage.Value = selected; _navigate(AppPage.PackageDetails); });
        }).ToList();

        Visual? toggle = results.Count > RowLimit ? ToggleButton(exp, results.Count) : null;
        return BuildPackageList(mapped, toggle);
    }
}
