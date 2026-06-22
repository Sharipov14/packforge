using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class CargoProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public CargoProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "cargo";
    public string DisplayName => "Cargo";
    public string Command => "cargo";
    public string InstallCommand => "curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh";
    public string InfoUrl => "https://rustup.rs";

    public bool IsInstalled() => _runner.Exists("cargo");

    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
    {
        try
        {
            var output = await _runner.RunAsync("cargo", "install --list");
            var rows = new List<PackageRow>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith(' ') || line.StartsWith('\t')) continue;
                var trimmed = line.Trim();
                if (!trimmed.Contains(" v")) continue;

                var spaceIdx = trimmed.IndexOf(" v", StringComparison.Ordinal);
                var name = trimmed[..spaceIdx].Trim();
                var versionPart = trimmed[(spaceIdx + 2)..].TrimEnd(':').Trim();
                var version = versionPart.Split(' ')[0].TrimEnd(':');

                rows.Add(new PackageRow(
                    Name: name,
                    Manager: DisplayName,
                    ManagerId: Id,
                    Installed: version,
                    Latest: version,
                    Outdated: false,
                    Source: "crates.io"));
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

    public string InstallPackageCommand(string packageName) => $"cargo install {packageName}";
    public string UpdatePackageCommand(string packageName) => $"cargo install {packageName} --force";
    public string RemovePackageCommand(string packageName) => $"cargo uninstall {packageName}";
    public string? UpdateAllCommand() => "cargo install-update -a";

    public string PackageUrl(string packageName)
        => $"https://crates.io/crates/{Uri.EscapeDataString(packageName)}";

    public async Task<PackageDoc?> GetDocumentationAsync(string packageName)
    {
        try
        {
            var output = await _runner.RunAsync("cargo", $"search {packageName} --limit 1");
            if (string.IsNullOrWhiteSpace(output)) return null;

            string description = "—";
            string latestVersion = "—";

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                var eqIdx = trimmed.IndexOf(" = \"", StringComparison.Ordinal);
                if (eqIdx < 0) continue;

                var foundName = trimmed[..eqIdx].Trim();
                if (!foundName.Equals(packageName, StringComparison.OrdinalIgnoreCase)) continue;

                var rest = trimmed[(eqIdx + 4)..];
                var closeQuote = rest.IndexOf('"');
                if (closeQuote >= 0)
                    latestVersion = rest[..closeQuote];

                var hashIdx = rest.IndexOf('#');
                if (hashIdx >= 0)
                    description = rest[(hashIdx + 1)..].Trim();

                break;
            }

            return new PackageDoc(
                Name: packageName,
                Description: description,
                Homepage: $"https://crates.io/crates/{packageName}",
                License: "—",
                LatestVersion: latestVersion,
                Dependencies: [],
                RecentVersions: []);
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        try
        {
            var output = await _runner.RunAsync("cargo", $"search {query}");
            if (string.IsNullOrWhiteSpace(output)) return [];

            var results = new List<SearchResult>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                var eqIdx = trimmed.IndexOf(" = \"", StringComparison.Ordinal);
                if (eqIdx < 0) continue;
                var name = trimmed[..eqIdx].Trim();
                var rest = trimmed[(eqIdx + 4)..];
                var closeQuote = rest.IndexOf('"');
                var version = closeQuote >= 0 ? rest[..closeQuote] : "—";
                var hashIdx = rest.IndexOf('#');
                var description = hashIdx >= 0 ? rest[(hashIdx + 1)..].Trim() : "—";
                results.Add(new SearchResult(name, Id, DisplayName, version, description));
            }
            return results;
        }
        catch
        {
            return [];
        }
    }
}
