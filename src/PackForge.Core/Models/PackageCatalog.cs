namespace PackForge.Core;

/// <summary>
/// Fallback / seed data used when no real package manager is available.
/// </summary>
public static class PackageCatalog
{
    public static readonly PackageItem[] Packages =
    [
        new("docker-core", "Homebrew", "24.0.7", "25.0.3", "MIT", "arm64", "452.8 MB", true),
        new("node-lts", "NPM", "20.10.0", "20.10.0", "MIT", "arm64", "118.3 MB", false),
        new("postgresql@15", "Homebrew", "15.4", "16.1", "PostgreSQL", "arm64", "63.1 MB", true),
        new("rust-compiler", "Cargo", "1.73.0", "1.73.0", "MIT/Apache-2.0", "arm64", "284.9 MB", false),
        new("zsh-autosuggestions", "Homebrew", "0.7.0", "0.7.1", "MIT", "universal", "92 KB", true),
    ];

    public static int OutdatedCount => Packages.Count(x => x.Outdated);

    /// <summary>Same data as PackageRow records, used as fallback when no managers installed.</summary>
    public static readonly IReadOnlyList<PackageRow> FallbackRows =
    [
        new PackageRow("docker-core",        "Homebrew", "homebrew", "24.0.7", "25.0.3", true,  "MIT",           "arm64",     "452.8 MB", "Homebrew"),
        new PackageRow("node-lts",           "NPM",      "npm",      "20.10.0","20.10.0",false, "MIT",           "arm64",     "118.3 MB", "npmjs.org"),
        new PackageRow("postgresql@15",      "Homebrew", "homebrew", "15.4",   "16.1",   true,  "PostgreSQL",    "arm64",     "63.1 MB",  "Homebrew"),
        new PackageRow("rust-compiler",      "Cargo",    "cargo",    "1.73.0", "1.73.0", false, "MIT/Apache-2.0","arm64",     "284.9 MB", "crates.io"),
        new PackageRow("zsh-autosuggestions","Homebrew", "homebrew", "0.7.0",  "0.7.1",  true,  "MIT",           "universal", "92 KB",    "Homebrew"),
    ];
}
