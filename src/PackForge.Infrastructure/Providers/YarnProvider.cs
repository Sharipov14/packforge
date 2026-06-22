using PackForge.Core;

namespace PackForge.Infrastructure;

/// <summary>Yarn — detection only; no package list (uses npm's global registry).</summary>
public sealed class YarnProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public YarnProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "yarn";
    public string DisplayName => "Yarn";
    public string Command => "yarn";
    public string InstallCommand => "npm install -g yarn";
    public string InfoUrl => "https://yarnpkg.com";

    public bool IsInstalled() => _runner.Exists("yarn");

    public Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
        => Task.FromResult<IReadOnlyList<PackageRow>>([]);

    public Task<IReadOnlySet<string>> GetOutdatedAsync()
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    public string InstallPackageCommand(string packageName) => $"yarn global add {packageName}";
    public string UpdatePackageCommand(string packageName) => $"yarn global upgrade {packageName}";
    public string RemovePackageCommand(string packageName) => $"yarn global remove {packageName}";
    public string? UpdateAllCommand() => "yarn global upgrade";

    public string PackageUrl(string packageName)
        => $"https://www.npmjs.com/package/{Uri.EscapeDataString(packageName)}";

    public Task<PackageDoc?> GetDocumentationAsync(string packageName)
        => NpmProvider.GetNpmDocAsync(_runner, packageName);

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        try
        {
            return await NpmProvider.SearchNpmRegistryAsync(_runner, query, Id, DisplayName);
        }
        catch
        {
            return [];
        }
    }
}
