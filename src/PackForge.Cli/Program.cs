using Microsoft.Extensions.DependencyInjection;
using PackForge.Application;
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
using var session = Terminal.Open();
sp.GetRequiredService<PackageManagerApp>().Run();
