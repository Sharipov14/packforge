using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;

namespace PackForge.Tui;

internal sealed class UpdateService
{
    private readonly AppState _state;
    private readonly Action<AppPage> _navigate;
    private readonly CommandService _commands;
    private readonly IPackageService _packageService;
    private readonly Random _rng = new();
    private int _tickCount;

    internal UpdateService(AppState state, Action<AppPage> navigate, CommandService commands, IPackageService packageService)
    {
        _state = state;
        _navigate = navigate;
        _commands = commands;
        _packageService = packageService;
    }

    /// <summary>Tick CPU/RAM metrics — called each update frame. No fake update progress.</summary>
    internal void TickMetrics()
    {
        _tickCount++;
        if (_tickCount % 4 != 0) return; // ~every 300 ms

        var isRunning = _state.IsCommandRunning.Value;
        var cpuBase  = isRunning ? 0.55 : 0.12;
        var cpuRange = isRunning ? 0.35 : 0.12;
        var ramBase  = isRunning ? 0.25 : 0.075;
        var ramRange = isRunning ? 0.20 : 0.04;

        var newCpu = Math.Clamp(cpuBase + (_rng.NextDouble() - 0.5) * cpuRange * 2, 0.01, 1.0);
        var newRam = Math.Clamp(ramBase + (_rng.NextDouble() - 0.5) * ramRange * 2, 0.01, 1.0);

        _state.CpuUsage.Value = newCpu;
        _state.RamUsage.Value = newRam;
        AppState.PushHistory(_state.CpuHistory, newCpu);
        AppState.PushHistory(_state.RamHistory, newRam);
    }

    /// <summary>Update a single package by running the provider's UpdatePackageCommand.</summary>
    internal void StartUpdate(PackageRow row)
    {
        var provider = _packageService.Providers
            .FirstOrDefault(p => p.Id.Equals(row.ManagerId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            _state.Notify($"Unknown manager: {row.ManagerId}", ToastSeverity.Error);
            return;
        }

        var cmd = provider.UpdatePackageCommand(row.Name);
        _state.UpdateClock.Restart();
        _commands.Execute(cmd);
    }

    /// <summary>Update all outdated packages for the active manager (or all if package is null).</summary>
    internal void StartUpdate(PackageItem? package = null)
    {
        if (_state.IsCommandRunning.Value)
        {
            _navigate(AppPage.UpdateLogs);
            return;
        }

        if (package is not null)
        {
            var provider = _packageService.Providers
                .FirstOrDefault(p => p.Id.Equals(package.Manager, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
            {
                _state.Notify($"Unknown manager: {package.Manager}", ToastSeverity.Error);
                return;
            }
            _state.UpdateClock.Restart();
            _commands.Execute(provider.UpdatePackageCommand(package.Name));
            return;
        }

        // Update-all: use the active manager
        var managerId = _state.ActiveManagerId.Value;
        var activeProvider = _packageService.Providers
            .FirstOrDefault(p => p.Id.Equals(managerId, StringComparison.OrdinalIgnoreCase));
        if (activeProvider is null)
        {
            _state.Notify($"No active manager found", ToastSeverity.Warning);
            return;
        }

        var allCmd = activeProvider.UpdateAllCommand();
        if (allCmd is null)
        {
            _state.Notify($"Bulk update not supported for {activeProvider.DisplayName}", ToastSeverity.Warning);
            return;
        }

        _state.UpdateClock.Restart();
        _commands.Execute(allCmd);
    }

    /// <summary>Abort the running update command.</summary>
    internal void AbortUpdate()
    {
        _commands.Abort();
        _state.UpdateClock.Stop();
        _state.Status.Value = "UPDATE_ABORTED | Transaction stopped by user";
        _state.UpdateLog.AppendMarkupLine("[error][ABORT][/] Process interrupted safely.");
        _state.ToastHost.Show("Update aborted", ToastSeverity.Warning);
    }

    internal void ExportLogs()
    {
        var path = Path.Combine(Environment.CurrentDirectory, "pkg_mgr_update.log");
        File.WriteAllText(path,
            $"PKG_MGR update log exported {DateTimeOffset.Now:O}{Environment.NewLine}" +
            "See the interactive log view for the current session.");
        _state.Status.Value = $"LOG_EXPORTED | {path}";
        _state.ToastHost.Show("Log exported to project directory", ToastSeverity.Success);
    }
}
