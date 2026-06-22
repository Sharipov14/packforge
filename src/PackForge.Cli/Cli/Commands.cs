using PackForge.Core;

namespace PackForge.Cli;

/// <summary>
/// One static handler per command.
/// All handlers follow: (args, opts, sp) → Task&lt;int&gt; where 0=ok, 2=usage/error.
/// </summary>
internal static class Commands
{
    // ── list ────────────────────────────────────────────────────────────────

    /// <summary>packforge list  — show all managers and whether they are installed.</summary>
    public static async Task<int> ListManagersAsync(CliOptions opts, IPackageService svc)
    {
        var (headers, rows) = Output.FormatManagerList(svc.Providers);
        Output.WriteTable(headers, rows, opts);
        return await Task.FromResult(0);
    }

    /// <summary>packforge &lt;mgr&gt; list  — installed packages for one manager.</summary>
    public static async Task<int> ListPackagesAsync(
        string managerId, CliOptions opts, IPackageService svc)
    {
        var packages = await svc.GetInstalledAsync(managerId);
        var (headers, rows) = Output.FormatPackageRows(packages);
        Output.WriteTable(headers, rows, opts);
        return 0;
    }

    // ── outdated ─────────────────────────────────────────────────────────────

    /// <summary>packforge outdated  — all managers.</summary>
    public static async Task<int> OutdatedAsync(CliOptions opts, IPackageService svc)
    {
        var packages = await svc.GetInstalledAsync();
        var (headers, rows) = Output.FormatOutdatedRows(packages);
        if (rows.Count == 0)
            Console.WriteLine("All packages are up to date.");
        else
            Output.WriteTable(headers, rows, opts);
        return 0;
    }

    /// <summary>packforge &lt;mgr&gt; outdated  — single manager.</summary>
    public static async Task<int> OutdatedForManagerAsync(
        string managerId, CliOptions opts, IPackageService svc)
    {
        var packages = await svc.GetInstalledAsync(managerId);
        var (headers, rows) = Output.FormatOutdatedRows(packages);
        if (rows.Count == 0)
            Console.WriteLine("All packages are up to date.");
        else
            Output.WriteTable(headers, rows, opts);
        return 0;
    }

    // ── search ───────────────────────────────────────────────────────────────

    /// <summary>packforge search &lt;query&gt; [-m &lt;mgr&gt;]</summary>
    public static async Task<int> SearchAsync(
        string query, string? managerId, CliOptions opts, IPackageService svc)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Output.Error("search requires a query string.");
            return 2;
        }

        var results = await svc.SearchAsync(query, managerId);
        if (results.Count == 0)
        {
            Console.WriteLine("No results found.");
            return 0;
        }

        var (headers, rows) = Output.FormatSearchResults(results, svc.Providers);
        Output.WriteTable(headers, rows, opts);
        return 0;
    }

    // ── info ─────────────────────────────────────────────────────────────────

    /// <summary>packforge info &lt;pkg&gt; / packforge &lt;mgr&gt; info &lt;pkg&gt;</summary>
    public static async Task<int> InfoAsync(
        string packageName, string? managerId, CliOptions opts, IPackageService svc)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            Output.Error("info requires a package name.");
            return 2;
        }

        // Try each relevant provider until one returns a non-null doc
        var providers = svc.Providers
            .Where(p => p.IsInstalled() &&
                        (managerId is null || p.Id.Equals(managerId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        PackageDoc? doc = null;
        string? foundManagerId = null;
        foreach (var p in providers)
        {
            try
            {
                doc = await p.GetDocumentationAsync(packageName);
                if (doc is not null) { foundManagerId = p.Id; break; }
            }
            catch { /* try next */ }
        }

        if (doc is null)
        {
            Console.WriteLine($"No documentation found for '{packageName}'.");
            return 0;
        }

        if (opts.Json)
        {
            Output.WriteJsonObject(doc);
            return 0;
        }

        if (opts.Plain)
        {
            Console.WriteLine($"Name\t{doc.Name}");
            Console.WriteLine($"Description\t{doc.Description}");
            Console.WriteLine($"Homepage\t{doc.Homepage}");
            Console.WriteLine($"License\t{doc.License}");
            Console.WriteLine($"Latest\t{doc.LatestVersion}");
            Console.WriteLine($"Size\t{doc.Size}");
            Console.WriteLine($"LastModified\t{doc.LastModified}");
            Console.WriteLine($"Dependencies\t{string.Join(", ", doc.Dependencies)}");
            Console.WriteLine($"RecentVersions\t{string.Join(", ", doc.RecentVersions)}");
            return 0;
        }

        // Rich output
        var infoHeaders = new[] { "Field", "Value" };
        var infoRows = new List<string[]>
        {
            new[] { "Name",            doc.Name },
            new[] { "Description",     doc.Description },
            new[] { "Homepage",        doc.Homepage },
            new[] { "License",         doc.License },
            new[] { "Latest",          doc.LatestVersion },
            new[] { "Size",            doc.Size },
            new[] { "Last Modified",   doc.LastModified },
            new[] { "Dependencies",    string.Join(", ", doc.Dependencies) },
            new[] { "Recent Versions", string.Join(", ", doc.RecentVersions) },
        };
        Output.WriteTable(infoHeaders, infoRows, opts);
        return 0;
    }

    // ── update ───────────────────────────────────────────────────────────────

    /// <summary>packforge &lt;mgr&gt; update  — update all packages for one manager.</summary>
    public static async Task<int> UpdateManagerAsync(
        IPackageProvider provider, CliOptions opts, IProcessRunner runner)
    {
        var cmd = provider.UpdateAllCommand();
        if (cmd is null)
        {
            Output.Warn($"{provider.DisplayName} does not support global update-all. Skipping.");
            return 0;
        }

        if (!Output.Confirm(cmd, opts.Yes)) return 0;
        return await Output.RunStreamingAsync(cmd, runner);
    }

    /// <summary>packforge update  — update all installed managers in turn.</summary>
    public static async Task<int> UpdateAllManagersAsync(
        CliOptions opts, IPackageService svc, IProcessRunner runner)
    {
        var exitCode = 0;
        foreach (var provider in svc.Providers.Where(p => p.IsInstalled()))
        {
            var cmd = provider.UpdateAllCommand();
            if (cmd is null)
            {
                Output.Warn($"{provider.DisplayName}: UpdateAllCommand not supported — skipping.");
                continue;
            }

            Console.WriteLine($"\n── {provider.DisplayName} ──");
            if (!Output.Confirm(cmd, opts.Yes))
            {
                Console.WriteLine("Skipped.");
                continue;
            }

            var code = await Output.RunStreamingAsync(cmd, runner);
            if (code != 0) exitCode = code;
        }

        return exitCode;
    }

    // ── install ───────────────────────────────────────────────────────────────

    /// <summary>packforge &lt;mgr&gt; install &lt;pkg&gt;</summary>
    public static async Task<int> InstallAsync(
        IPackageProvider provider, string packageName, CliOptions opts, IProcessRunner runner)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            Output.Error("install requires a package name.");
            return 2;
        }

        var cmd = provider.InstallPackageCommand(packageName);
        if (!Output.Confirm(cmd, opts.Yes)) return 0;
        return await Output.RunStreamingAsync(cmd, runner);
    }

    // ── remove ────────────────────────────────────────────────────────────────

    /// <summary>packforge &lt;mgr&gt; remove &lt;pkg&gt;</summary>
    public static async Task<int> RemoveAsync(
        IPackageProvider provider, string packageName, CliOptions opts, IProcessRunner runner)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            Output.Error("remove requires a package name.");
            return 2;
        }

        var cmd = provider.RemovePackageCommand(packageName);
        if (!Output.Confirm(cmd, opts.Yes)) return 0;
        return await Output.RunStreamingAsync(cmd, runner);
    }

    // ── config ───────────────────────────────────────────────────────────────

    /// <summary>packforge config / config show  — display config path + visibility.</summary>
    public static async Task<int> ConfigShowAsync(CliOptions opts, IConfigStore configStore, string configPath)
    {
        var config = configStore.Load();

        if (opts.Json)
        {
            Output.WriteJsonObject(new { ConfigPath = configPath, config.ManagerVisibility });
            return await Task.FromResult(0);
        }

        if (opts.Plain)
        {
            Console.WriteLine($"path\t{configPath}");
            foreach (var kv in config.ManagerVisibility)
                Console.WriteLine($"{kv.Key}\t{(kv.Value ? "enabled" : "disabled")}");
            return await Task.FromResult(0);
        }

        Console.WriteLine($"Config path: {configPath}");
        var headers = new[] { "Manager", "Visibility" };
        var rows = config.ManagerVisibility
            .Select(kv => new[] { kv.Key, kv.Value ? "enabled" : "disabled" })
            .ToList();
        if (rows.Count == 0)
            Console.WriteLine("(no overrides — all managers visible)");
        else
            Output.WriteTable(headers, rows, opts);

        return await Task.FromResult(0);
    }

    /// <summary>packforge config path  — print config file path.</summary>
    public static Task<int> ConfigPathAsync(string configPath)
    {
        Console.WriteLine(configPath);
        return Task.FromResult(0);
    }

    /// <summary>packforge config enable/disable &lt;mgr&gt;</summary>
    public static Task<int> ConfigSetVisibilityAsync(
        string managerId, bool visible, IConfigStore configStore)
    {
        var config = configStore.Load();
        config.ManagerVisibility[managerId] = visible;
        configStore.Save(config);
        Console.WriteLine($"{managerId}: {(visible ? "enabled" : "disabled")}");
        return Task.FromResult(0);
    }

    /// <summary>packforge config reset  — clears all overrides.</summary>
    public static Task<int> ConfigResetAsync(IConfigStore configStore)
    {
        configStore.Save(new PackForge.Core.AppConfig());
        Console.WriteLine("Config reset to defaults.");
        return Task.FromResult(0);
    }

    // ── help / version ────────────────────────────────────────────────────────

    public static Task<int> HelpAsync()
    {
        Console.WriteLine("""
            PackForge — cross-manager package tool

            USAGE
              packforge                        Launch TUI
              packforge <command> [options]

            QUERY COMMANDS
              list                             Show all package managers
              <mgr> list                       Installed packages for <mgr>
              outdated                         Packages with updates (all managers)
              <mgr> outdated                   Outdated for <mgr>
              search <query> [-m <mgr>]        Search registries
              info <pkg>                       Package documentation
              <mgr> info <pkg>                 Package documentation from <mgr>

            MUTATING COMMANDS  (confirm by default; skip with -y/--yes)
              update                           Update all installed managers
              <mgr> update                     Update all packages for <mgr>
              <mgr> install <pkg>              Install a package
              <mgr> remove  <pkg>              Remove a package

            CONFIG
              config [show]                    Show config path + manager visibility
              config path                      Print config file path
              config enable <mgr>              Enable a manager
              config disable <mgr>             Disable a manager
              config reset                     Clear all overrides

            OPTIONS
              --json                           Output as JSON
              --plain                          Output as tab-separated text
              -y, --yes                        Skip confirmation prompts
              -m, --manager <mgr>              Restrict to one manager

            MANAGERS
              npm, pnpm, homebrew (brew), yarn, cargo, pip, dotnet

            EXIT CODES
              0  success
              2  usage / unknown command
              *  propagated from the package manager process
            """);
        return Task.FromResult(0);
    }

    public static Task<int> VersionAsync()
    {
        var version = typeof(Commands).Assembly.GetName().Version;
        Console.WriteLine($"PackForge {version?.ToString(3) ?? "0.1.0"}");
        return Task.FromResult(0);
    }
}
