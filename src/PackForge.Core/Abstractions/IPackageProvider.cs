namespace PackForge.Core;

public interface IPackageProvider
{
    string Id { get; }
    string DisplayName { get; }
    string Command { get; }
    string InstallCommand { get; }
    string InfoUrl { get; }
    bool IsInstalled();
    Task<IReadOnlyList<PackageRow>> GetInstalledAsync();
    Task<IReadOnlySet<string>> GetOutdatedAsync();
    string InstallPackageCommand(string packageName);
    string UpdatePackageCommand(string packageName);
    string RemovePackageCommand(string packageName);
    /// <summary>Returns the shell command to update all packages for this manager, or null if not supported.</summary>
    string? UpdateAllCommand();
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query);
    Task<PackageDoc?> GetDocumentationAsync(string packageName);
    /// <summary>Official registry/documentation page for a specific package.</summary>
    string PackageUrl(string packageName);
}
