using Microsoft.Extensions.DependencyInjection;
using PackForge.Application;
using PackForge.Cli;
using PackForge.Core;
using PackForge.Infrastructure;
using PackForge.Tui;
using XenoAtom.Terminal;

var services = new ServiceCollection()
    .AddSingleton<IProcessRunner, ProcessRunner>()
    .AddSingleton<ISystemInterop, SystemInterop>()
    .AddSingleton<IConfigStore, ConfigService>()
    .AddSingleton<IProviderRegistry, ProviderRegistry>()
    .AddSingleton<IPackageService, PackageService>()
    .AddSingleton<PackageManagerApp>();
using var sp = services.BuildServiceProvider();

// No args (or explicit "tui") → launch the fullscreen TUI (unchanged behaviour).
// Any other argument → CLI command mode (no alt-screen).
if (args.Length == 0 || (args.Length == 1 && args[0].Equals("tui", StringComparison.OrdinalIgnoreCase)))
{
    using var session = Terminal.Open();
    sp.GetRequiredService<PackageManagerApp>().Run();
}
else
{
    Environment.ExitCode = await CommandRouter.RunAsync(args, sp);
}
