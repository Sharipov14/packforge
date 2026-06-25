using PackForge.Core;
using XenoAtom.Terminal.UI.Controls;

namespace PackForge.Tui;

internal sealed class CommandService
{
    private readonly AppState _state;
    private readonly Action<AppPage> _navigate;
    private readonly IProcessRunner _processRunner;
    private volatile CancellationTokenSource? _cts;
    private volatile string? _pendingFinalStatus;
    private volatile bool _completed;
    private DateTime _commandStartTime;
    private int _commandLogLinesCount;
    private volatile int _exitCode;

    internal CommandService(AppState state, Action<AppPage> navigate, IProcessRunner processRunner)
    {
        _state = state;
        _navigate = navigate;
        _processRunner = processRunner;
    }

    internal void Execute(string input)
    {
        input = input?.Trim() ?? string.Empty;
        if (input.Length == 0)
            return;
        if (_state.IsCommandRunning.Value)
        {
            _state.Notify("Command already running", ToastSeverity.Warning);
            return;
        }

        _commandStartTime = DateTime.Now;
        _commandLogLinesCount = 0;
        _state.CommandInput.Value = string.Empty;
        _state.IsCommandRunning.Value = true;
        _state.UpdateClock.Restart();

        _state.CommandLogQueue.Enqueue((FormatDivider(), false));
        _state.CommandLogQueue.Enqueue((FormatCommandHeader(input), false));
        _state.Status.Value = $"RUNNING | {input}";
        _navigate(AppPage.UpdateLogs);

        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                var exit = await _processRunner.RunStreamingAsync(
                    input,
                    l => _state.CommandLogQueue.Enqueue((l, false)),
                    l => _state.CommandLogQueue.Enqueue((FormatErrorLine(l), true)),
                    cts.Token);
                _exitCode = exit;
                var duration = DateTime.Now - _commandStartTime;
                _state.CommandLogQueue.Enqueue((FormatCommandFooter(duration, _commandLogLinesCount, exit), false));
                if (exit == 0)
                {
                    _pendingFinalStatus = $"DONE | {input}";
                }
                else
                {
                    _pendingFinalStatus = $"FAILED ({exit}) | {input}";
                }
            }
            catch (OperationCanceledException)
            {
                _state.CommandLogQueue.Enqueue(("[bold yellow]⋯[/] [yellow]Command cancelled[/]", false));
                _state.CommandLogQueue.Enqueue((FormatDivider(), false));
                _pendingFinalStatus = "ABORTED";
            }
            catch (Exception ex)
            {
                _state.CommandLogQueue.Enqueue(($"[bold red]✗ Error:[/] [red]{ex.Message}[/]", false));
                _state.CommandLogQueue.Enqueue((FormatDivider(), false));
                _pendingFinalStatus = "ERROR";
            }
            finally
            {
                _completed = true;
            }
        });
    }

    internal void Pump()
    {
        var linesAdded = 0;
        while (_state.CommandLogQueue.TryDequeue(out var entry))
        {
            _state.UpdateLog.AppendMarkupLine(entry.text);
            linesAdded++;
            if (!entry.isError)
                _commandLogLinesCount++;
        }
        if (linesAdded > 0)
            _state.LogLineCount.Value += linesAdded;

        if (_completed)
        {
            _completed = false;
            _state.IsCommandRunning.Value = false;
            _state.UpdateClock.Stop();
            _state.CommandLogQueue.Enqueue((FormatDivider(), false));
            if (_pendingFinalStatus is not null)
            {
                _state.Status.Value = _pendingFinalStatus;
                _pendingFinalStatus = null;
            }
        }
    }

    private string FormatDivider() =>
        "[cyan]━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━[/]";

    private string FormatCommandHeader(string command) =>
        $"[bold cyan]▶[/] [bold]{DateTime.Now:HH:mm:ss}[/] [dim]{command}[/]";

    private string FormatCommandFooter(TimeSpan duration, int lines, int exitCode)
    {
        var statusIcon = exitCode == 0 ? "[bold green]✓[/]" : "[bold red]✗[/]";
        var statusColor = exitCode == 0 ? "green" : "red";
        var formattedDuration = duration.TotalSeconds < 1
            ? $"{(int)duration.TotalMilliseconds}ms"
            : $"{(int)duration.TotalSeconds}s";
        return $"{statusIcon} [{statusColor}]Duration: {formattedDuration}[/] | Lines: {lines} | Exit: {exitCode}";
    }

    private string FormatErrorLine(string text) =>
        $"[bold red]✗ {text}[/]";

    internal void Abort() => _cts?.Cancel();
}
