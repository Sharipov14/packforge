using System.Collections.Concurrent;
using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PackForge.Tui;

internal enum DetailsDocTab { Readme, Changelog }

internal sealed class AppState
{
    public AppState(IConfigStore configStore, IPackageService packageService)
    {
        var config = configStore.Load();
        var visibility = config.ManagerVisibility;
        // Fill in missing providers with default=true
        foreach (var provider in packageService.Providers)
        {
            if (!visibility.ContainsKey(provider.Id))
                visibility[provider.Id] = true;
        }
        ManagerVisibility = new State<Dictionary<string, bool>>(visibility);
        InstalledFlags = new State<Dictionary<string, bool>>(packageService.GetInstalledFlags());
    }

    // ── Navigation ───────────────────────────────────────────────────────────
    internal readonly State<AppPage> Page = new(AppPage.Dashboard);
    internal readonly State<bool> ExitRequested = new(false);
    internal readonly State<string> Status = new("SYSTEM_OK | Ready");

    // ── Update / log state ───────────────────────────────────────────────────
    internal readonly State<double> UpdateProgress = new(0);
    internal readonly State<int> ActiveQueue = new(0);
    internal readonly State<bool> IsUpdating = new(false);
    internal readonly Stopwatch UpdateClock = new();
    internal readonly LogControl UpdateLog = new() { MaxCapacity = 2000 };
    /// <summary>Count of log lines written; incremented by CommandService.Pump().</summary>
    internal readonly State<int> LogLineCount = new(0);

    // ── Source settings ──────────────────────────────────────────────────────
    internal readonly State<Dictionary<string, bool>> ManagerVisibility;
    internal readonly State<string?> SourceName = new("PyPI_Mirror");
    internal readonly State<string?> Endpoint = new("https://pypi.org/simple");
    internal readonly State<string?> ApiKey = new(string.Empty);

    // ── Package providers ────────────────────────────────────────────────────
    /// <summary>Currently selected manager in the sidebar (e.g. "homebrew").</summary>
    internal readonly State<string> ActiveManagerId = new("homebrew");

    /// <summary>All loaded packages (across all managers).</summary>
    internal readonly State<IReadOnlyList<PackageRow>> Packages =
        new(PackageCatalog.FallbackRows);

    /// <summary>True while provider refresh is in progress.</summary>
    internal readonly State<bool> IsLoading = new(false);

    /// <summary>Package selected for the details view.</summary>
    internal readonly State<PackageRow?> SelectedPackage = new(null);

    /// <summary>Search text entered in the header search box.</summary>
    internal readonly State<string?> SearchQuery = new(string.Empty);

    // ── Store ─────────────────────────────────────────────────────────────────
    /// <summary>Query text entered in the Store page search box.</summary>
    internal readonly State<string?> StoreQuery = new(string.Empty);

    /// <summary>Results from the last store search across all installed providers.</summary>
    internal readonly State<IReadOnlyList<SearchResult>> SearchResults = new([]);

    /// <summary>True while store search is in progress.</summary>
    internal readonly State<bool> IsSearching = new(false);

    /// <summary>Active tab in PackageDetails (README / CHANGELOG).</summary>
    internal readonly State<DetailsDocTab> DocTab = new(DetailsDocTab.Readme);

    /// <summary>Documentation fetched for the currently selected package.</summary>
    internal readonly State<PackageDoc?> SelectedDoc = new(null);

    /// <summary>True while doc fetch is in progress.</summary>
    internal readonly State<bool> IsDocLoading = new(false);

    /// <summary>Per-manager installation flags (updated after each refresh).</summary>
    internal readonly State<Dictionary<string, bool>> InstalledFlags;

    // ── Live metrics (CPU / RAM) ─────────────────────────────────────────────
    /// <summary>Current simulated CPU usage [0..1].</summary>
    internal readonly State<double> CpuUsage = new(0.12);

    /// <summary>Current simulated RAM usage [0..1].</summary>
    internal readonly State<double> RamUsage = new(0.075);

    /// <summary>Rolling history for the CPU sparkline (~24 data points).</summary>
    internal readonly List<double> CpuHistory = [0.12, 0.15, 0.10, 0.18, 0.22, 0.14, 0.11, 0.16, 0.20, 0.13,
                                                  0.09, 0.17, 0.21, 0.12, 0.15, 0.10, 0.19, 0.23, 0.14, 0.11,
                                                  0.16, 0.20, 0.13, 0.12];

    /// <summary>Rolling history for the RAM sparkline (~24 data points).</summary>
    internal readonly List<double> RamHistory = [0.075, 0.08, 0.07, 0.085, 0.09, 0.078, 0.072, 0.081, 0.088, 0.076,
                                                  0.071, 0.083, 0.09, 0.077, 0.08, 0.073, 0.086, 0.092, 0.079, 0.074,
                                                  0.082, 0.089, 0.077, 0.075];

    /// <summary>Push a new value to a rolling history, keeping the last 24 points.</summary>
    internal static void PushHistory(List<double> history, double value)
    {
        history.Add(value);
        if (history.Count > 24)
            history.RemoveAt(0);
    }

    // ── Shell ────────────────────────────────────────────────────────────────
    internal readonly State<string?> CommandInput = new(string.Empty);
    internal readonly State<bool> IsCommandRunning = new(false);
    internal readonly ConcurrentQueue<(string text, bool isError)> CommandLogQueue = new();
    internal readonly ToastHost ToastHost = new();
    internal long LastTick = Stopwatch.GetTimestamp();

    internal bool IsVisible(string id)
        => ManagerVisibility.Value.TryGetValue(id, out var v) ? v : true;

    internal void Notify(string message, ToastSeverity severity)
    {
        Status.Value = message;
        ToastHost.Show(message, severity);
    }
}
