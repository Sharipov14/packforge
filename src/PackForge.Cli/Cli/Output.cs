using System.Text.Json;
using PackForge.Core;
using XenoAtom.Terminal;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace PackForge.Cli;

/// <summary>
/// Rendering helpers for CLI output: rich table, --plain (tab-separated), --json.
/// Also wires streaming command execution to Console.
/// </summary>
internal static class Output
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = true };

    // ── Table output ────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a rich table to the terminal (requires a TTY; no alt-screen needed).
    /// </summary>
    public static void WriteTable(string[] headers, IReadOnlyList<string[]> rows, CliOptions opts)
    {
        if (opts.Json)
        {
            WriteJson(headers, rows);
            return;
        }

        if (opts.Plain)
        {
            WritePlain(headers, rows);
            return;
        }

        // Rich table via XenoAtom.Terminal.UI
        // Headers/AddRow take params Visual[]; strings implicitly convert one-by-one but not as an array.
        var headerVisuals = headers.Select(h => (Visual)new TextBlock(h)).ToArray();
        var table = new Table().Headers(headerVisuals);
        foreach (var row in rows)
        {
            var rowVisuals = row.Select(cell => (Visual)new TextBlock(cell)).ToArray();
            table.AddRow(rowVisuals);
        }
        Terminal.Write(table);
    }

    // ── JSON output ──────────────────────────────────────────────────────────

    private static void WriteJson(string[] headers, IReadOnlyList<string[]> rows)
    {
        var list = rows.Select(row =>
        {
            var dict = new Dictionary<string, string>();
            for (int i = 0; i < headers.Length && i < row.Length; i++)
                dict[headers[i]] = row[i];
            return dict;
        }).ToList();

        Console.WriteLine(JsonSerializer.Serialize(list, _jsonOpts));
    }

    // ── Plain (tab-separated) output ─────────────────────────────────────────

    private static void WritePlain(string[] headers, IReadOnlyList<string[]> rows)
    {
        Console.WriteLine(string.Join("\t", headers));
        foreach (var row in rows)
            Console.WriteLine(string.Join("\t", row));
    }

    // ── Single-object JSON ───────────────────────────────────────────────────

    public static void WriteJsonObject<T>(T obj)
        => Console.WriteLine(JsonSerializer.Serialize(obj, _jsonOpts));

    // ── Confirm prompt ────────────────────────────────────────────────────────

    /// <summary>
    /// Prints the command to run, then prompts [Y/n] unless <paramref name="yes"/> is true.
    /// Returns true if the user confirms (or --yes was passed).
    /// </summary>
    public static bool Confirm(string command, bool yes)
    {
        Console.WriteLine($"  $ {command}");
        if (yes) return true;

        Console.Write("Run? [Y/n] ");
        var answer = Console.ReadLine()?.Trim();
        // empty = default Y; "n"/"N" = no
        return string.IsNullOrEmpty(answer) || answer.Equals("y", StringComparison.OrdinalIgnoreCase);
    }

    // ── Streaming run ─────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <see cref="IProcessRunner.RunStreamingAsync"/>, piping stdout to Console.Out
    /// and stderr to Console.Error. Wires Ctrl+C to cancel. Returns the process exit code.
    /// </summary>
    public static async Task<int> RunStreamingAsync(string command, IProcessRunner runner)
    {
        using var cts = new CancellationTokenSource();

        // Wire Ctrl+C → cancel
        ConsoleCancelEventHandler? handler = null;
        handler = (_, e) =>
        {
            e.Cancel = true; // suppress default termination
            cts.Cancel();
            Console.Error.WriteLine("^C — cancelling…");
        };
        Console.CancelKeyPress += handler;

        try
        {
            return await runner.RunStreamingAsync(
                command,
                onStdout: line => Console.WriteLine(line),
                onStderr: line => Console.Error.WriteLine(line),
                ct: cts.Token);
        }
        catch (OperationCanceledException)
        {
            return 130; // standard Ctrl+C exit code
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    // ── Simple message helpers ────────────────────────────────────────────────

    public static void Warn(string message) => Console.Error.WriteLine($"warning: {message}");
    public static void Error(string message) => Console.Error.WriteLine($"error: {message}");

    // ── PackageRow / SearchResult formatters ─────────────────────────────────

    public static (string[] headers, IReadOnlyList<string[]> rows) FormatPackageRows(
        IReadOnlyList<PackageRow> packages)
    {
        var headers = new[] { "Manager", "Name", "Installed", "Latest", "Outdated" };
        var rows = packages.Select(p => new[]
        {
            p.Manager,
            p.Name,
            p.Installed,
            p.Latest,
            p.Outdated ? "Yes" : "No"
        }).ToList();
        return (headers, rows);
    }

    public static (string[] headers, IReadOnlyList<string[]> rows) FormatOutdatedRows(
        IReadOnlyList<PackageRow> packages)
    {
        var headers = new[] { "Manager", "Name", "Installed", "Latest" };
        var rows = packages
            .Where(p => p.Outdated)
            .Select(p => new[] { p.Manager, p.Name, p.Installed, p.Latest })
            .ToList();
        return (headers, rows);
    }

    public static (string[] headers, IReadOnlyList<string[]> rows) FormatSearchResults(
        IReadOnlyList<SearchResult> results,
        IReadOnlyList<IPackageProvider> providers)
    {
        var headers = new[] { "Manager", "Name", "Version", "Description", "Docs URL", "Install cmd" };
        var rows = results.Select(r =>
        {
            var prov = providers.FirstOrDefault(p => p.Id.Equals(r.ManagerId, StringComparison.OrdinalIgnoreCase));
            var docsUrl = prov?.PackageUrl(r.Name) ?? "—";
            var installCmd = prov?.InstallPackageCommand(r.Name) ?? "—";
            return new[] { r.Manager, r.Name, r.Version, r.Description, docsUrl, installCmd };
        }).ToList();
        return (headers, rows);
    }

    public static (string[] headers, IReadOnlyList<string[]> rows) FormatManagerList(
        IReadOnlyList<IPackageProvider> providers)
    {
        var headers = new[] { "Manager", "Command", "Installed", "Info URL" };
        var rows = providers.Select(p => new[]
        {
            p.DisplayName,
            p.Command,
            p.IsInstalled() ? "Yes" : $"No  (install: {p.InstallCommand})",
            p.InfoUrl
        }).ToList();
        return (headers, rows);
    }
}
