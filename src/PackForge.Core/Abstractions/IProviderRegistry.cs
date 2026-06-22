namespace PackForge.Core;

public interface IProviderRegistry
{
    IReadOnlyList<IPackageProvider> Providers { get; }
}
