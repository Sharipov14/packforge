using System.Text.Json.Nodes;
using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class PipProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public PipProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "pip";
    public string DisplayName => "Pip";
    public string Command => "pip3";
    public string InstallCommand => "python3 -m ensurepip --upgrade";
    public string InfoUrl => "https://pip.pypa.io";

    public bool IsInstalled() => _runner.Exists("pip3");

    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
    {
        try
        {
            var installedJson = await _runner.RunAsync("pip3", "list --format=json");
            var array = JsonNode.Parse(installedJson)?.AsArray();
            if (array is null) return [];

            var outdatedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var outdatedJson = await _runner.RunAsync("pip3", "list --outdated --format=json");
                var outdatedArr = JsonNode.Parse(outdatedJson)?.AsArray();
                if (outdatedArr is not null)
                {
                    foreach (var item in outdatedArr)
                    {
                        var n = item?["name"]?.GetValue<string>();
                        var lv = item?["latest_version"]?.GetValue<string>();
                        if (n is not null && lv is not null) outdatedMap[n] = lv;
                    }
                }
            }
            catch { /* ignore */ }

            var rows = new List<PackageRow>();
            foreach (var item in array)
            {
                var name = item?["name"]?.GetValue<string>() ?? "—";
                var installed = item?["version"]?.GetValue<string>() ?? "—";
                var isOutdated = outdatedMap.ContainsKey(name);
                var latest = isOutdated ? outdatedMap[name] : installed;

                rows.Add(new PackageRow(
                    Name: name,
                    Manager: DisplayName,
                    ManagerId: Id,
                    Installed: installed,
                    Latest: latest,
                    Outdated: isOutdated,
                    Source: "pypi.org"));
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
            var json = await _runner.RunAsync("pip3", "list --outdated --format=json");
            var array = JsonNode.Parse(json)?.AsArray();
            if (array is null) return new HashSet<string>();
            return new HashSet<string>(
                array.Select(i => i?["name"]?.GetValue<string>() ?? "")
                     .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    public string InstallPackageCommand(string packageName) => $"pip3 install {packageName}";
    public string UpdatePackageCommand(string packageName) => $"pip3 install --upgrade {packageName}";
    public string RemovePackageCommand(string packageName) => $"pip3 uninstall -y {packageName}";
    public string? UpdateAllCommand() => null;

    public string PackageUrl(string packageName)
        => $"https://pypi.org/project/{Uri.EscapeDataString(packageName)}";

    public async Task<PackageDoc?> GetDocumentationAsync(string packageName)
    {
        try
        {
            var output = await _runner.RunAsync("pip3", $"show {packageName}");
            if (string.IsNullOrWhiteSpace(output)) return null;

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx < 0) continue;
                var key = line[..colonIdx].Trim();
                var value = line[(colonIdx + 1)..].Trim();
                fields[key] = value;
            }

            var description = fields.TryGetValue("Summary", out var s) ? s : "—";
            var homepage = fields.TryGetValue("Home-page", out var hp) ? hp : "—";
            var license = fields.TryGetValue("License", out var lic) ? lic : "—";
            var latestVersion = fields.TryGetValue("Version", out var v) ? v : "—";

            var deps = new List<string>();
            if (fields.TryGetValue("Requires", out var requires) && !string.IsNullOrWhiteSpace(requires))
                deps.AddRange(requires.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                      .Select(d => d.Trim())
                                      .Where(d => !string.IsNullOrEmpty(d)));

            var recentVersions = new List<string>();
            try
            {
                var indexOutput = await _runner.RunAsync("pip3", $"index versions {packageName}");
                foreach (var line in indexOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("Available versions:", StringComparison.OrdinalIgnoreCase))
                    {
                        var versionsPart = trimmed["Available versions:".Length..].Trim();
                        recentVersions.AddRange(
                            versionsPart.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(vv => vv.Trim())
                                        .Where(vv => !string.IsNullOrEmpty(vv))
                                        .Take(10));
                        break;
                    }
                }
            }
            catch { /* best-effort; ignore */ }

            return new PackageDoc(packageName, description, homepage, license, latestVersion, deps, recentVersions);
        }
        catch
        {
            return null;
        }
    }

    public Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
        => Task.FromResult<IReadOnlyList<SearchResult>>([]);
}
