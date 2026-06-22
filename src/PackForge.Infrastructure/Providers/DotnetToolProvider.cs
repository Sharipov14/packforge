using System.Text.RegularExpressions;
using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class DotnetToolProvider : IPackageProvider
{
    private readonly IProcessRunner _runner;

    public DotnetToolProvider(IProcessRunner runner) => _runner = runner;

    public string Id => "dotnet";
    public string DisplayName => ".NET Tools";
    public string Command => "dotnet";
    public string InstallCommand => "brew install dotnet";
    public string InfoUrl => "https://dotnet.microsoft.com";

    public bool IsInstalled() => _runner.Exists("dotnet");

    public async Task<IReadOnlyList<PackageRow>> GetInstalledAsync()
    {
        try
        {
            var output = await _runner.RunAsync("dotnet", "tool list -g");
            if (string.IsNullOrWhiteSpace(output)) return [];

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var rows = new List<PackageRow>();
            foreach (var line in lines.Skip(2))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (cols.Length < 2) continue;
                var name = cols[0];
                var version = cols[1];
                rows.Add(new PackageRow(
                    Name: name,
                    Manager: DisplayName,
                    ManagerId: Id,
                    Installed: version,
                    Latest: version,
                    Outdated: false,
                    Source: "nuget.org"));
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

    public string InstallPackageCommand(string packageName) => $"dotnet tool install -g {packageName}";
    public string UpdatePackageCommand(string packageName) => $"dotnet tool update -g {packageName}";
    public string RemovePackageCommand(string packageName) => $"dotnet tool uninstall -g {packageName}";
    public string? UpdateAllCommand() => null;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        try
        {
            var output = await _runner.RunAsync("dotnet", $"tool search {query}");
            if (string.IsNullOrWhiteSpace(output)) return [];

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var results = new List<SearchResult>();
            foreach (var line in lines.Skip(2))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cols = Regex.Split(line.Trim(), @"\s{2,}");
                if (cols.Length < 2) continue;
                var name = cols[0];
                var version = cols[1];
                results.Add(new SearchResult(name, Id, DisplayName, version, "—"));
            }
            return results;
        }
        catch
        {
            return [];
        }
    }

    public string PackageUrl(string packageName)
        => $"https://www.nuget.org/packages/{Uri.EscapeDataString(packageName)}";

    public async Task<PackageDoc?> GetDocumentationAsync(string packageName)
    {
        try
        {
            var output = await _runner.RunAsync("dotnet", $"tool search {packageName} --detail");
            if (string.IsNullOrWhiteSpace(output)) return null;

            var lines = output.Split('\n');
            var description = "—";
            var latestVersion = "—";
            var recentVersions = new List<string>();
            var inVersionsSection = false;

            foreach (var line in lines)
            {
                if (line.Contains("Description:"))
                {
                    var idx = line.IndexOf("Description:", StringComparison.Ordinal);
                    description = line[(idx + "Description:".Length)..].Trim();
                    inVersionsSection = false;
                }
                else if (line.Contains("Latest Version:"))
                {
                    var idx = line.IndexOf("Latest Version:", StringComparison.Ordinal);
                    latestVersion = line[(idx + "Latest Version:".Length)..].Trim();
                    inVersionsSection = false;
                }
                else if (line.Contains("Versions:"))
                {
                    inVersionsSection = true;
                }
                else if (inVersionsSection && !string.IsNullOrWhiteSpace(line))
                {
                    var token = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                    if (token is not null) recentVersions.Add(token);
                }
            }

            if (recentVersions.Count > 10)
                recentVersions = recentVersions.Skip(recentVersions.Count - 10).ToList();

            return new PackageDoc(
                Name: packageName,
                Description: description,
                Homepage: $"https://www.nuget.org/packages/{packageName}",
                License: "—",
                LatestVersion: latestVersion,
                Dependencies: [],
                RecentVersions: recentVersions);
        }
        catch
        {
            return null;
        }
    }
}
