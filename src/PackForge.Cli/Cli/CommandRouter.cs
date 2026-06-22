using Microsoft.Extensions.DependencyInjection;
using PackForge.Core;
using PackForge.Infrastructure;

namespace PackForge.Cli;

/// <summary>
/// Parses argv and dispatches to the appropriate handler in <see cref="Commands"/>.
/// </summary>
internal static class CommandRouter
{
    // Alias map: lower-case input → provider Id
    private static readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["brew"] = "homebrew",
    };

    public static async Task<int> RunAsync(string[] args, IServiceProvider sp)
    {
        var svc      = sp.GetRequiredService<IPackageService>();
        var runner   = sp.GetRequiredService<IProcessRunner>();
        var cfgStore = sp.GetRequiredService<IConfigStore>();

        // ConfigService.ConfigPath is a static field on the Infrastructure type
        var configPath = ConfigService.ConfigPath;

        // Strip global flags
        var (remaining, opts) = CliOptions.Parse(args);

        if (remaining.Length == 0)
            return await Commands.HelpAsync();

        var verb = remaining[0].ToLowerInvariant();

        // ── Top-level meta ───────────────────────────────────────────────────
        switch (verb)
        {
            case "help":
            case "--help":
            case "-h":
                return await Commands.HelpAsync();

            case "version":
            case "--version":
                return await Commands.VersionAsync();

            case "tui":
                // handled in Program.cs; if somehow we get here, explain
                Console.Error.WriteLine("Use 'packforge' (no args) or 'packforge tui' to launch the TUI.");
                return 0;
        }

        // ── Config ───────────────────────────────────────────────────────────
        if (verb == "config")
        {
            return await DispatchConfig(remaining, opts, cfgStore, configPath, svc);
        }

        // ── Global query / mutate commands ───────────────────────────────────
        switch (verb)
        {
            case "list":
                return await Commands.ListManagersAsync(opts, svc);

            case "outdated":
                return await Commands.OutdatedAsync(opts, svc);

            case "update":
                return await Commands.UpdateAllManagersAsync(opts, svc, runner);

            case "search":
            {
                var query = remaining.Length > 1 ? remaining[1] : string.Empty;
                var mgr   = opts.Manager;
                return await Commands.SearchAsync(query, mgr, opts, svc);
            }

            case "info":
            {
                var pkg = remaining.Length > 1 ? remaining[1] : string.Empty;
                return await Commands.InfoAsync(pkg, null, opts, svc);
            }
        }

        // ── Try to resolve verb as a manager id ──────────────────────────────
        var provider = ResolveProvider(verb, svc.Providers);
        if (provider is null)
        {
            Console.Error.WriteLine($"error: unknown command or manager '{remaining[0]}'.");
            Console.Error.WriteLine("Run 'packforge help' for usage.");
            return 2;
        }

        // packforge <mgr> <sub-command> [args...]
        var sub = remaining.Length > 1 ? remaining[1].ToLowerInvariant() : "list";

        switch (sub)
        {
            case "list":
                return await Commands.ListPackagesAsync(provider.Id, opts, svc);

            case "outdated":
                return await Commands.OutdatedForManagerAsync(provider.Id, opts, svc);

            case "update":
                return await Commands.UpdateManagerAsync(provider, opts, runner);

            case "install":
            {
                var pkg = remaining.Length > 2 ? remaining[2] : string.Empty;
                return await Commands.InstallAsync(provider, pkg, opts, runner);
            }

            case "remove":
            case "uninstall":
            {
                var pkg = remaining.Length > 2 ? remaining[2] : string.Empty;
                return await Commands.RemoveAsync(provider, pkg, opts, runner);
            }

            case "info":
            {
                var pkg = remaining.Length > 2 ? remaining[2] : string.Empty;
                return await Commands.InfoAsync(pkg, provider.Id, opts, svc);
            }

            case "search":
            {
                var query = remaining.Length > 2 ? remaining[2] : string.Empty;
                return await Commands.SearchAsync(query, provider.Id, opts, svc);
            }

            default:
                Console.Error.WriteLine($"error: unknown sub-command '{remaining[1]}' for '{provider.Id}'.");
                Console.Error.WriteLine("Run 'packforge help' for usage.");
                return 2;
        }
    }

    // ── Config dispatch ───────────────────────────────────────────────────────

    private static async Task<int> DispatchConfig(
        string[] remaining,
        CliOptions opts,
        IConfigStore cfgStore,
        string configPath,
        IPackageService svc)
    {
        var sub = remaining.Length > 1 ? remaining[1].ToLowerInvariant() : "show";

        switch (sub)
        {
            case "show":
            case "":
                return await Commands.ConfigShowAsync(opts, cfgStore, configPath);

            case "path":
                return await Commands.ConfigPathAsync(configPath);

            case "reset":
                return await Commands.ConfigResetAsync(cfgStore);

            case "enable":
            case "disable":
            {
                if (remaining.Length < 3)
                {
                    Console.Error.WriteLine($"error: 'config {sub}' requires a manager id.");
                    return 2;
                }
                var mgr = remaining[2].ToLowerInvariant();
                // Normalise alias
                if (_aliases.TryGetValue(mgr, out var aliased)) mgr = aliased;
                return await Commands.ConfigSetVisibilityAsync(mgr, sub == "enable", cfgStore);
            }

            default:
                Console.Error.WriteLine($"error: unknown config sub-command '{remaining[1]}'.");
                return 2;
        }
    }

    // ── Manager resolution ────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a token to a provider by Id (exact, case-insensitive),
    /// then by Command, then by the alias table.
    /// Returns null when no match found.
    /// </summary>
    private static IPackageProvider? ResolveProvider(
        string token, IReadOnlyList<IPackageProvider> providers)
    {
        // Normalise alias first
        if (_aliases.TryGetValue(token, out var aliased)) token = aliased;

        return providers.FirstOrDefault(p =>
            p.Id.Equals(token, StringComparison.OrdinalIgnoreCase) ||
            p.Command.Equals(token, StringComparison.OrdinalIgnoreCase));
    }
}
