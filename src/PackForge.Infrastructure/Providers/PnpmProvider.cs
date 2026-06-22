using System.Text.Json.Nodes;
using PackForge.Core;

namespace PackForge.Infrastructure;

/// <summary>pnpm — global package list via <c>pnpm list -g --depth=0 --json</c>.</summary>
public sealed class PnpmProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public PnpmProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "pnpm";
    public string DisplayName => "pnpm";
    public string Command => "pnpm";
    public string InstallCommand => "npm install -g pnpm";
    public string InfoUrl => "https://pnpm.io";

    public bool IsInstalled() => _runner.Exists("pnpm");

    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
    {
        try
        {
            var json = await _runner.RunAsync("pnpm", "list -g --depth=0 --json");
            if (string.IsNullOrWhiteSpace(json)) return [];

            var array = JsonNode.Parse(json)?.AsArray();
            var deps = array?[0]?["dependencies"]?.AsObject();
            if (deps is null) return [];

            var rows = new List<PackageRow>();
            foreach (var (name, node) in deps)
            {
                var installed = node?["version"]?.GetValue<string>() ?? "—";
                rows.Add(new PackageRow(
                    Name: name,
                    Manager: DisplayName,
                    ManagerId: Id,
                    Installed: installed,
                    Latest: installed,
                    Outdated: false,
                    Source: "npmjs.org"));
            }
            return rows;
        }
        catch
        {
            return [];
        }
    }

    public Task<IReadOnlySet<string>> GetOutdatedAsync()
        => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    public string InstallPackageCommand(string packageName) => $"pnpm add -g {packageName}";
    public string UpdatePackageCommand(string packageName) => $"pnpm add -g {packageName}@latest";
    public string RemovePackageCommand(string packageName) => $"pnpm remove -g {packageName}";
    public string? UpdateAllCommand() => "pnpm update -g";

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

    public string PackageUrl(string packageName)
        => $"https://www.npmjs.com/package/{Uri.EscapeDataString(packageName)}";

    public Task<PackageDoc?> GetDocumentationAsync(string packageName)
        => NpmProvider.GetNpmDocAsync(_runner, packageName);
}
