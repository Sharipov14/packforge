using System.Text.Json.Nodes;
using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class NpmProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public NpmProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "npm";
    public string DisplayName => "NPM";
    public string Command => "npm";
    public string InstallCommand => "brew install node";
    public string InfoUrl => "https://nodejs.org";

    public bool IsInstalled() => _runner.Exists("npm");

    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
    {
        try
        {
            var installedJson = await _runner.RunAsync("npm", "list -g --depth=0 --json");
            var root = JsonNode.Parse(installedJson);
            var deps = root?["dependencies"]?.AsObject();
            if (deps is null) return [];

            var latestMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var outdatedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var outdatedJson = await _runner.RunAsync("npm", "outdated -g --json");
                if (!string.IsNullOrWhiteSpace(outdatedJson))
                {
                    var outdatedRoot = JsonNode.Parse(outdatedJson)?.AsObject();
                    if (outdatedRoot is not null)
                    {
                        foreach (var (name, node) in outdatedRoot)
                        {
                            outdatedSet.Add(name);
                            var latest = node?["latest"]?.GetValue<string>();
                            if (latest is not null) latestMap[name] = latest;
                        }
                    }
                }
            }
            catch { /* npm outdated returns non-zero exit; ignore failures */ }

            var rows = new List<PackageRow>();
            foreach (var (name, node) in deps)
            {
                var installed = node?["version"]?.GetValue<string>() ?? "—";
                var isOutdated = outdatedSet.Contains(name);
                var latest = latestMap.TryGetValue(name, out var lv) ? lv : installed;

                rows.Add(new PackageRow(
                    Name: name,
                    Manager: DisplayName,
                    ManagerId: Id,
                    Installed: installed,
                    Latest: latest,
                    Outdated: isOutdated,
                    Source: "npmjs.org"));
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
            var json = await _runner.RunAsync("npm", "outdated -g --json");
            if (string.IsNullOrWhiteSpace(json)) return new HashSet<string>();
            var root = JsonNode.Parse(json)?.AsObject();
            if (root is null) return new HashSet<string>();
            return new HashSet<string>(root.Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>();
        }
    }

    public string InstallPackageCommand(string packageName) => $"npm install -g {packageName}";
    public string UpdatePackageCommand(string packageName) => $"npm install -g {packageName}@latest";
    public string RemovePackageCommand(string packageName) => $"npm uninstall -g {packageName}";
    public string? UpdateAllCommand() => "npm update -g";

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        try
        {
            return await SearchNpmRegistryAsync(_runner, query, Id, DisplayName);
        }
        catch
        {
            return [];
        }
    }

    public string PackageUrl(string packageName)
        => $"https://www.npmjs.com/package/{Uri.EscapeDataString(packageName)}";

    public async Task<PackageDoc?> GetDocumentationAsync(string packageName)
    {
        try
        {
            return await GetNpmDocAsync(_runner, packageName);
        }
        catch
        {
            return null;
        }
    }

    internal static async Task<PackageDoc?> GetNpmDocAsync(IProcessRunner runner, string name)
    {
        try
        {
            var json = await runner.RunAsync("npm", $"view {name} --json");
            if (string.IsNullOrWhiteSpace(json)) return null;
            var root = JsonNode.Parse(json);
            if (root is null) return null;

            var description = root["description"]?.GetValue<string>() ?? "—";
            var homepage = root["homepage"]?.GetValue<string>() ?? "—";

            string license;
            var licenseNode = root["license"];
            if (licenseNode is JsonObject licenseObj)
                license = licenseObj["type"]?.GetValue<string>() ?? "—";
            else
                license = licenseNode?.GetValue<string>() ?? "—";

            var latestVersion = root["dist-tags"]?["latest"]?.GetValue<string>() ?? "—";

            var deps = new List<string>();
            if (root["dependencies"] is JsonObject depsObj)
                deps.AddRange(depsObj.Select(kv => kv.Key));

            var recentVersions = new List<string>();
            if (root["versions"] is JsonArray versionsArr)
            {
                var allVersions = versionsArr
                    .Select(v => v?.GetValue<string>() ?? "")
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                recentVersions.AddRange(
                    allVersions.Skip(Math.Max(0, allVersions.Count - 10)).Reverse());
            }

            string size = "—";
            try
            {
                var unpackedSize = root["dist"]?["unpackedSize"]?.GetValue<long>();
                if (unpackedSize.HasValue)
                    size = FormatBytes(unpackedSize.Value);
            }
            catch { /* best-effort */ }

            string lastModified = "—";
            try
            {
                var modified = root["time"]?["modified"]?.GetValue<string>();
                if (modified is not null && DateTimeOffset.TryParse(modified, out var dt))
                    lastModified = dt.ToString("yyyy.MM.dd");
            }
            catch { /* best-effort */ }

            return new PackageDoc(name, description, homepage, license, latestVersion, deps, recentVersions, size, lastModified);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

    internal static async Task<IReadOnlyList<SearchResult>> SearchNpmRegistryAsync(
        IProcessRunner runner, string query, string managerId, string managerName)
    {
        try
        {
            var json = await runner.RunAsync("npm", $"search {query} --json");
            if (string.IsNullOrWhiteSpace(json)) return [];
            var array = JsonNode.Parse(json)?.AsArray();
            if (array is null) return [];

            var results = new List<SearchResult>();
            foreach (var item in array)
            {
                var name = item?["name"]?.GetValue<string>() ?? "—";
                var version = item?["version"]?.GetValue<string>() ?? "—";
                var description = item?["description"]?.GetValue<string>() ?? "—";
                results.Add(new SearchResult(name, managerId, managerName, version, description));
            }
            return results;
        }
        catch
        {
            return [];
        }
    }
}
