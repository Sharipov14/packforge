using PackForge.Core;

namespace PackForge.Application;

public sealed class PackageService : IPackageService
{
    private readonly IProviderRegistry _registry;

    public PackageService(IProviderRegistry registry)
    {
        _registry = registry;
    }

    public IReadOnlyList<IPackageProvider> Providers => _registry.Providers;

    // Volatile result slot — written by background Task, read on Update() thread
    private volatile RefreshResult? _pendingResult;
    private bool _refreshInFlight;

    // Volatile search slot — written by background Task, read on Update() thread
    private volatile IReadOnlyList<SearchResult>? _pendingSearch;
    private bool _searchInFlight;

    // Volatile doc slot — written by background Task, read on Update() thread
    // Using a reference type so volatile is valid (value tuples cannot be volatile).
    private volatile DocResult? _pendingDoc;

    public bool HasPendingResult => _pendingResult is not null;
    public bool HasPendingSearch => _pendingSearch is not null;
    public bool HasPendingDoc => _pendingDoc is not null;

    // Reference-type wrapper so it can be held in a volatile field
    private sealed class DocResult(string name, PackageDoc? doc)
    {
        internal readonly string Name = name;
        internal readonly PackageDoc? Doc = doc;
    }

    /// <summary>
    /// Takes the pending result off the slot (call once per Update() tick after HasPendingResult).
    /// </summary>
    public RefreshResult TakePendingResult()
    {
        var r = _pendingResult!;
        _pendingResult = null;
        _refreshInFlight = false;
        return r;
    }

    /// <summary>
    /// Takes the pending search results off the slot (call once per Update() tick after HasPendingSearch).
    /// </summary>
    public IReadOnlyList<SearchResult> TakePendingSearch()
    {
        var r = _pendingSearch!;
        _pendingSearch = null;
        _searchInFlight = false;
        return r;
    }

    /// <summary>
    /// Takes the pending doc off the slot (call once per Update() tick after HasPendingDoc).
    /// </summary>
    public (string name, PackageDoc? doc) TakePendingDoc()
    {
        var r = _pendingDoc!;
        _pendingDoc = null;
        return (r.Name, r.Doc);
    }

    /// <summary>
    /// Starts a background doc fetch for the given package.
    /// When done, result lands in HasPendingDoc.
    /// Safe to call from Update() thread.
    /// </summary>
    public void BeginDocFetch(PackageRow pkg)
    {
        var pkgName = pkg.Name;
        var managerId = pkg.ManagerId;

        Task.Run(async () =>
        {
            try
            {
                var provider = Providers.FirstOrDefault(
                    p => p.Id.Equals(managerId, StringComparison.OrdinalIgnoreCase));
                PackageDoc? doc = null;
                if (provider is not null)
                {
                    try { doc = await provider.GetDocumentationAsync(pkgName); }
                    catch { doc = null; }
                }
                _pendingDoc = new DocResult(pkgName, doc);
            }
            catch
            {
                _pendingDoc = new DocResult(pkgName, null);
            }
        });
    }

    /// <summary>
    /// Directly awaitable: fetches installed packages for all (or a specific) manager.
    /// </summary>
    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync(string? managerId = null)
    {
        var result = await RefreshCoreAsync(managerId);
        return result.Packages;
    }

    /// <summary>
    /// Directly awaitable: searches across all installed (or a specific) provider.
    /// </summary>
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string? managerId = null)
    {
        var providers = Providers
            .Where(p => p.IsInstalled() &&
                        (managerId is null || p.Id.Equals(managerId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var tasks = providers
            .Select(async p =>
            {
                try { return await p.SearchAsync(query); }
                catch { return (IReadOnlyList<SearchResult>)[]; }
            })
            .ToList();

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    /// <summary>
    /// Starts a background search across all installed providers.
    /// When done, result lands in HasPendingSearch.
    /// Safe to call from Update() thread.
    /// </summary>
    public void BeginSearch(string query)
    {
        if (_searchInFlight) return;
        _searchInFlight = true;

        Task.Run(async () =>
        {
            try
            {
                _pendingSearch = await SearchAsync(query);
            }
            catch
            {
                _pendingSearch = [];
            }
        });
    }

    /// <summary>
    /// Starts a background refresh. When done, result lands in HasPendingResult.
    /// Safe to call from Update() thread.
    /// </summary>
    public void BeginRefresh(string? filterManagerId = null)
    {
        if (_refreshInFlight) return;
        _refreshInFlight = true;

        Task.Run(async () =>
        {
            try
            {
                var result = await RefreshCoreAsync(filterManagerId);
                _pendingResult = result;
            }
            catch
            {
                _pendingResult = new RefreshResult([], GetInstalledFlags());
            }
        });
    }

    private async Task<RefreshResult> RefreshCoreAsync(string? filterManagerId)
    {
        var installedFlags = GetInstalledFlags();

        var tasks = Providers
            .Where(p => p.IsInstalled() && (filterManagerId is null || p.Id.Equals(filterManagerId, StringComparison.OrdinalIgnoreCase)))
            .Select(async p =>
            {
                try { return await p.GetInstalledAsync(); }
                catch { return (IReadOnlyList<PackageRow>)[]; }
            })
            .ToList();

        var results = await Task.WhenAll(tasks);
        var allRows = results.SelectMany(r => r).ToList();
        return new RefreshResult(allRows, installedFlags);
    }

    public Dictionary<string, bool> GetInstalledFlags()
        => Providers.ToDictionary(p => p.Id, p => p.IsInstalled());

    public static IReadOnlyList<PackageRow> FilterByManager(
        IReadOnlyList<PackageRow> rows, string managerId)
        => rows.Where(r => r.ManagerId.Equals(managerId, StringComparison.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyList<PackageRow> FilterBySearch(
        IReadOnlyList<PackageRow> rows, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return rows;
        return rows.Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }
}
