using System.Diagnostics;
using PackForge.Core;

namespace PackForge.Infrastructure;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<string> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 15_000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* ignore */ }
            throw new TimeoutException($"{fileName} {arguments} timed out after {timeoutMs}ms");
        }

        var stdout = await stdoutTask;
        // Non-zero exit is okay for some commands (e.g. npm outdated returns 1 when updates exist)
        return stdout;
    }

    public async Task<int> RunStreamingAsync(
        string command,
        Action<string> onStdout,
        Action<string> onStderr,
        CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/sh",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add(command);
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onStdout(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onStderr(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }
        return process.ExitCode;
    }

    public bool Exists(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            process!.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
