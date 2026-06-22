namespace PackForge.Cli;

/// <summary>
/// Parsed global flags stripped from argv before command dispatch.
/// </summary>
internal sealed class CliOptions
{
    public bool Json { get; private set; }
    public bool Plain { get; private set; }
    public bool Yes { get; private set; }
    public string? Manager { get; private set; }

    /// <summary>
    /// Strips well-known global flags from <paramref name="args"/> and returns the
    /// remaining positional tokens.
    /// </summary>
    public static (string[] remaining, CliOptions opts) Parse(string[] args)
    {
        var opts = new CliOptions();
        var remaining = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--json":
                    opts.Json = true;
                    break;
                case "--plain":
                    opts.Plain = true;
                    break;
                case "--yes":
                case "-y":
                    opts.Yes = true;
                    break;
                case "--manager":
                case "-m":
                    if (i + 1 < args.Length)
                        opts.Manager = args[++i];
                    break;
                default:
                    if (arg.StartsWith("--manager=", StringComparison.Ordinal))
                        opts.Manager = arg["--manager=".Length..];
                    else
                        remaining.Add(arg);
                    break;
            }
        }

        return (remaining.ToArray(), opts);
    }
}
