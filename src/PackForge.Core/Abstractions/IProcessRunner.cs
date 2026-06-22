namespace PackForge.Core;

public interface IProcessRunner
{
    Task<string> RunAsync(string fileName, string arguments, int timeoutMs = 15_000);
    Task<int> RunStreamingAsync(string command, Action<string> onStdout, Action<string> onStderr, CancellationToken ct);
    bool Exists(string command);
}
