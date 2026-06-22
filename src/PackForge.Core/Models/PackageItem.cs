namespace PackForge.Core;

public sealed record PackageItem(
    string Name,
    string Manager,
    string Installed,
    string Latest,
    string License,
    string Architecture,
    string Size,
    bool Outdated);
