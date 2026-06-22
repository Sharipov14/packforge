using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class ProviderRegistry : IProviderRegistry
{
    public IReadOnlyList<IPackageProvider> Providers { get; }

    public ProviderRegistry(IProcessRunner runner)
    {
        Providers =
        [
            new NpmProvider(runner),
            new PnpmProvider(runner),
            new HomebrewProvider(runner),
            new YarnProvider(runner),
            new CargoProvider(runner),
            new PipProvider(runner),
            new DotnetToolProvider(runner),
        ];
    }
}
