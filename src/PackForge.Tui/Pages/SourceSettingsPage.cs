using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal sealed class SourceSettingsPage
{
    private readonly AppState _state;
    private readonly Action<AppPage> _navigate;
    private readonly IPackageService _packageService;
    private readonly IConfigStore _configStore;
    private readonly ISystemInterop _systemInterop;

    // Per-provider State<bool> for Switch two-way binding
    private readonly Dictionary<string, State<bool>> _visStates;
    private readonly Dictionary<string, bool> _installedFlags;

    private readonly State<string?> _installHint = new(null);
    private readonly State<IReadOnlyList<(bool ok, string text)>> _diagResults = new([]);
    private volatile bool _diagRunning;

    private static readonly string _configId =
        $"0x{(uint)Environment.MachineName.GetHashCode() & 0xFFFFFF:X6}";

    internal SourceSettingsPage(AppState state, Action<AppPage> navigate, IPackageService packageService, IConfigStore configStore, ISystemInterop systemInterop)
    {
        _state = state;
        _navigate = navigate;
        _packageService = packageService;
        _configStore = configStore;
        _systemInterop = systemInterop;

        var visibility = state.ManagerVisibility.Value;
        _visStates = packageService.Providers.ToDictionary(
            p => p.Id,
            p => new State<bool>(visibility.TryGetValue(p.Id, out var v) ? v : true));

        _installedFlags = new Dictionary<string, bool>(state.InstalledFlags.Value);
    }

    internal Visual Build()
    {
        var flags = _installedFlags;

        var config = new Group()
            .TopLeftText($" ~/config/sources.yaml  ID: {_configId} ")
            .Padding(Layout.GroupPad)
            .Content(new Markup(() =>
            {
                var currentFlags = _state.InstalledFlags.Value;
                var activeSources = _packageService.Providers
                    .Where(p =>
                        (currentFlags.TryGetValue(p.Id, out var inst) && inst) &&
                        _visStates.TryGetValue(p.Id, out var st) && st.Value)
                    .Select(p => $"\"{p.Id}\"");
                var activeStr = string.Join(", ", activeSources);

                return $$"""
                       [success]pkg_mgr[/] {
                         [primary]id[/]: [warning]"{{_configId}}"[/],
                         [primary]status[/]: [success]"synchronized"[/],
                         [primary]active_sources[/]: [primary][{{activeStr}}][/],
                         [primary]last_sync[/]: [warning]"{{DateTime.Now:yyyy-MM-dd HH:mm:ss}}"[/],
                         [primary]encoding[/]: [success]"UTF-8"[/]
                       }
                       """;
            }));

        var rows = _packageService.Providers.Select(p =>
        {
            var isInstalled = flags.TryGetValue(p.Id, out var inst) && inst;
            var visState = _visStates[p.Id];
            var provider = p;
            return Widgets.ManagerSettingRow(
                provider,
                isInstalled,
                visState,
                onInstall: () => OnInstall(provider));
        }).ToArray();

        var installHintBlock = new ComputedVisual(() =>
        {
            var hint = _installHint.Value;
            if (hint is null) return (Visual)new TextBlock("") { Wrap = false };
            return (Visual)new Group()
                .TopLeftText(" INSTALL_HINT ")
                .Padding(1)
                .Content(new Markup($"[warning]$ {hint}[/]"));
        });

        var sources = new Group()
            .TopLeftText(" 01. ACTIVE_SOURCES ")
            .Padding(Layout.GroupPad)
            .Content(new VStack([.. rows, installHintBlock]).Spacing(Layout.Item));

        var addSource = new Group()
            .TopLeftText(" 02. CONFIG.SOURCES.ADD ")
            .Padding(Layout.GroupPad)
            .Content(new VStack(
                    "SOURCE NAME",
                    new HStack(
                        new Markup("[dim]> [/]"),
                        new TextBox(_state.SourceName).HorizontalAlignment(Align.Stretch))
                        .Spacing(0)
                        .HorizontalAlignment(Align.Stretch),
                    "ENDPOINT URL",
                    new HStack(
                        new Markup("[dim]> [/]"),
                        new TextBox(_state.Endpoint).HorizontalAlignment(Align.Stretch))
                        .Spacing(0)
                        .HorizontalAlignment(Align.Stretch),
                    "API KEY (AUTH)",
                    new HStack(
                        new Markup("[dim]> [/]"),
                        new TextBox(_state.ApiKey) { IsPassword = true }.HorizontalAlignment(Align.Stretch))
                        .Spacing(0)
                        .HorizontalAlignment(Align.Stretch),
                    Widgets.PrimaryButton("INITIALIZE ADD", AddSource))
                .Spacing(Layout.Section));

        var diagResultsBlock = new ComputedVisual(() =>
        {
            var results = _diagResults.Value;
            if (results.Count == 0)
            {
                return (Visual)new Markup(_diagRunning
                    ? "[dim]Running diagnostics…[/]"
                    : "[dim]Press RUN_DIAGNOSTICS to check all package managers.[/]");
            }
            var lines = results.Select(r =>
                r.ok
                    ? $"[success][OK][/]   {r.text}"
                    : $"[error][FAIL][/] {r.text}");
            return (Visual)new Markup(string.Join("\n", lines));
        });

        var diagnostics = new Group()
            .TopLeftText(" 03. SYSTEM_VERIFICATION ")
            .Padding(Layout.GroupPad)
            .Content(new VStack(
                    new HStack(
                            new TextBlock("TESTING CONNECTION TO ALL ENDPOINTS...").HorizontalAlignment(Align.Stretch),
                            new Button("RUN_DIAGNOSTICS").Click(RunDiagnostics))
                        .Spacing(Layout.Item),
                    diagResultsBlock)
                .Spacing(Layout.Section));

        var actions = new HStack(
                new Button("RESTORE_DEFAULTS").Click(RestoreDefaults),
                new Button("CANCEL").Click(() => _navigate(AppPage.Dashboard)),
                new Button("SAVE_CHANGES").Tone(ControlTone.Primary).Click(SaveSettings))
            .Spacing(1)
            .HorizontalAlignment(Align.End);

        var content = new Grid()
            .Rows(
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto })
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(3) },
                new ColumnDefinition { Width = GridLength.Star(2) }.MinWidth(Layout.RightColMin))
            .ColumnGap(Layout.Gap)
            .RowGap(Layout.Section)
            .Cell(new VStack(
                    new Markup("[bold primary]SOURCE SETTINGS[/]"),
                    new TextBlock("Manage package repository definitions and environment mapping."))
                .Spacing(Layout.Section), 0, 0, columnSpan: 2)
            .Cell(config, 1, 0, columnSpan: 2)
            .Cell(sources, 2, 0)
            .Cell(addSource, 2, 1)
            .Cell(new VStack(diagnostics, actions).Spacing(Layout.Section), 3, 0, columnSpan: 2)
            .HorizontalAlignment(Align.Stretch);

        return new ScrollViewer(content.Pad(Layout.PagePad)).Stretch();
    }

    private void OnInstall(IPackageProvider provider)
    {
        _installHint.Value = provider.InstallCommand;
        _state.Notify($"Install: {provider.InstallCommand}", ToastSeverity.Info);
        _state.UpdateLog.AppendLine($"$ {provider.InstallCommand}");
        _ = _systemInterop.CopyToClipboardAsync(provider.InstallCommand);
    }

    private void AddSource()
    {
        if (string.IsNullOrWhiteSpace(_state.SourceName.Value) || string.IsNullOrWhiteSpace(_state.Endpoint.Value))
        {
            _state.Notify("Source name and endpoint are required", ToastSeverity.Error);
            return;
        }

        _state.Notify($"Source {_state.SourceName.Value} initialized", ToastSeverity.Success);
    }

    private void RunDiagnostics()
    {
        if (_diagRunning) return;
        _diagRunning = true;
        _diagResults.Value = [];
        _state.Status.Value = "DIAGNOSTICS_RUNNING | Checking package managers…";

        _ = Task.Run(async () =>
        {
            var results = new List<(bool ok, string text)>();
            foreach (var provider in _packageService.Providers)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool ok;
                try { ok = provider.IsInstalled(); }
                catch { ok = false; }
                sw.Stop();
                results.Add(ok
                    ? (true, $"{provider.DisplayName} ready in {sw.ElapsedMilliseconds}ms")
                    : (false, $"{provider.DisplayName} not found"));
                _diagResults.Value = results.ToList();
                await Task.Delay(10);
            }

            _diagRunning = false;
            var ok2 = results.Count(r => r.ok);
            _state.Status.Value = $"DIAGNOSTICS_COMPLETE | {ok2}/{results.Count} managers available";
            _state.ToastHost.Show($"Diagnostics complete: {ok2}/{results.Count} ready", ToastSeverity.Info);
        });
    }

    private void RestoreDefaults()
    {
        foreach (var st in _visStates.Values)
            st.Value = true;
        var allVisible = _visStates.ToDictionary(kvp => kvp.Key, _ => true);
        _state.ManagerVisibility.Value = allVisible;
        _configStore.Save(new AppConfig { ManagerVisibility = new Dictionary<string, bool>(allVisible) });
        _state.SourceName.Value = "PyPI_Mirror";
        _state.Endpoint.Value = "https://pypi.org/simple";
        _state.ApiKey.Value = string.Empty;
        _state.Notify("Default source configuration restored", ToastSeverity.Info);
    }

    private void SaveSettings()
    {
        var dict = _visStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value);
        _state.ManagerVisibility.Value = dict;
        _configStore.Save(new AppConfig { ManagerVisibility = new Dictionary<string, bool>(dict) });
        _state.Notify("Settings saved to ~/.config/pkg_mgr/config.json", ToastSeverity.Success);
    }
}
