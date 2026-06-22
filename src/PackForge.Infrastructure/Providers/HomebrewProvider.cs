using System.Text.Json.Nodes;
using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class HomebrewProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public HomebrewProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "homebrew";
    public string DisplayName => "Homebrew";
    public string Command => "brew";
    public string InstallCommand => @"/bin/bash -c ""$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)""";
    public string InfoUrl => "https://brew.sh";

    public bool IsInstalled() => _runner.Exists("brew");

    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
    {
        try
        {
            var installedOutput = await _runner.RunAsync("brew", "list --versions");

            var outdatedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var outdatedOutput = await _runner.RunAsync("brew", "outdated --verbose");
                foreach (var line in outdatedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    var ltIdx = trimmed.LastIndexOf('<');
                    if (ltIdx < 0) continue;
                    var name = trimmed.Split(' ')[0];
                    var latest = trimmed[(ltIdx + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(latest))
                        outdatedMap[name] = latest;
                }
            }
            catch { /* ignore outdated failures */ }

            var rows = new List<PackageRow>();
            foreach (var line in installedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Trim().Split(' ', 2);
                if (parts.Length < 2) continue;
                var name = parts[0];
                var installed = parts[1].Split(' ')[0];
                var isOutdated = outdatedMap.ContainsKey(name);
                var latest = isOutdated ? outdatedMap[name] : installed;

                rows.Add(new PackageRow(
                    Name: name,
                    Manager: DisplayName,
                    ManagerId: Id,
                    Installed: installed,
                    Latest: latest,
                    Outdated: isOutdated,
                    Source: "Homebrew"));
            }
            return rows;
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlySet<string>> GetOutdatedAsync()
    {
        try
        {
            var output = await _runner.RunAsync("brew", "outdated --verbose");
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = line.Trim().Split(' ')[0];
                if (!string.IsNullOrWhiteSpace(name))
                    set.Add(name);
            }
            return set;
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    public string InstallPackageCommand(string packageName) => $"brew install {packageName}";
    public string UpdatePackageCommand(string packageName) => $"brew upgrade {packageName}";
    public string RemovePackageCommand(string packageName) => $"brew uninstall {packageName}";
    public string? UpdateAllCommand() => "brew upgrade";

    public string PackageUrl(string packageName)
        => $"https://formulae.brew.sh/formula/{Uri.EscapeDataString(packageName)}";

    public async Task<PackageDoc?> GetDocumentationAsync(string packageName)
    {
        try
        {
            var info = await GetPackageInfoAsync(_runner, packageName);
            if (info is null) return null;

            var recentVersions = !string.IsNullOrEmpty(info.LatestStable) && info.LatestStable != "—"
                ? (IReadOnlyList<string>)[info.LatestStable]
                : (IReadOnlyList<string>)[];

            return new PackageDoc(
                Name: packageName,
                Description: info.Description,
                Homepage: info.Homepage,
                License: info.License,
                LatestVersion: info.LatestStable,
                Dependencies: info.Dependencies,
                RecentVersions: recentVersions);
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
            var output = await _runner.RunAsync("brew", $"search {query}");
            if (string.IsNullOrWhiteSpace(output)) return [];

            var results = new List<SearchResult>();
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("==>")) continue;
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                results.Add(new SearchResult(trimmed, Id, DisplayName));
            }
            return results;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Fetch rich metadata for a single package via brew info --json=v2.</summary>
    internal static async Task<BrewPackageInfo?> GetPackageInfoAsync(IProcessRunner runner, string name)
    {
        try
        {
            var json = await runner.RunAsync("brew", $"info --json=v2 {name}");
            var formula = JsonNode.Parse(json)?["formulae"]?[0];
            if (formula is null) return null;

            var license = formula["license"]?.GetValue<string>() ?? "—";
            var latestStable = formula["versions"]?["stable"]?.GetValue<string>() ?? "—";
            var homepage = formula["homepage"]?.GetValue<string>() ?? "—";
            var desc = formula["desc"]?.GetValue<string>() ?? "—";
            var deps = formula["dependencies"]?.AsArray()
                           .Select(d => d?.GetValue<string>() ?? "")
                           .Where(s => !string.IsNullOrEmpty(s))
                           .ToArray() ?? [];

            return new BrewPackageInfo(license, latestStable, homepage, desc, deps);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed record BrewPackageInfo(
    string License,
    string LatestStable,
    string Homepage,
    string Description,
    string[] Dependencies);
