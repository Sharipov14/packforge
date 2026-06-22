namespace PackForge.Core;

public interface IPackageService
{
    IReadOnlyList<IPackageProvider> Providers { get; }
    Dictionary<string, bool> GetInstalledFlags();

    // Direct awaitable methods for CLI use
    Task<IReadOnlyList<PackageRow>> GetInstalledAsync(string? managerId = null);
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query, string? managerId = null);

    bool HasPendingResult { get; }
    RefreshResult TakePendingResult();
    void BeginRefresh(string? filterManagerId = null);

    bool HasPendingSearch { get; }
    IReadOnlyList<SearchResult> TakePendingSearch();
    void BeginSearch(string query);

    bool HasPendingDoc { get; }
    (string name, PackageDoc? doc) TakePendingDoc();
    void BeginDocFetch(PackageRow pkg);
}
