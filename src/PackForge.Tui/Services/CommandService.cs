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

        _state.CommandInput.Value = string.Empty;
        _state.IsCommandRunning.Value = true;
        _state.UpdateClock.Restart();
        _state.CommandLogQueue.Enqueue(($"$ {input}", false));
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
                    l => _state.CommandLogQueue.Enqueue((l, true)),
                    cts.Token);
                if (exit == 0)
                {
                    _state.CommandLogQueue.Enqueue(("[OK] exit 0", false));
                    _pendingFinalStatus = $"DONE | {input}";
                }
                else
                {
                    _state.CommandLogQueue.Enqueue(($"exit {exit}", true));
                    _pendingFinalStatus = $"FAILED ({exit}) | {input}";
                }
            }
            catch (OperationCanceledException)
            {
                _state.CommandLogQueue.Enqueue(("[ABORT] command cancelled", true));
                _pendingFinalStatus = "ABORTED";
            }
            catch (Exception ex)
            {
                _state.CommandLogQueue.Enqueue(($"error: {ex.Message}", true));
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
            if (entry.isError)
                _state.UpdateLog.AppendLine("[ERR] " + entry.text);
            else
                _state.UpdateLog.AppendLine(entry.text);
            linesAdded++;
        }
        if (linesAdded > 0)
            _state.LogLineCount.Value += linesAdded;

        if (_completed)
        {
            _completed = false;
            _state.IsCommandRunning.Value = false;
            _state.UpdateClock.Stop();
            if (_pendingFinalStatus is not null)
            {
                _state.Status.Value = _pendingFinalStatus;
                _pendingFinalStatus = null;
            }
        }
    }

    internal void Abort() => _cts?.Cancel();
}
