using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal sealed class UpdateLogsPage
{
    private readonly AppState _state;
    private readonly UpdateService _updates;

    internal UpdateLogsPage(AppState state, UpdateService updates)
    {
        _state = state;
        _updates = updates;
    }

    internal Visual Build()
    {
        var cpuSparkline = new ComputedVisual(() =>
            (Visual)new Sparkline(_state.CpuHistory).Maximum(1.0).Minimum(0.0));

        var ramSparkline = new ComputedVisual(() =>
            (Visual)new Sparkline(_state.RamHistory).Maximum(1.0).Minimum(0.0));

        var stats = Widgets.Panel(" session_stats ", PanelAccent.Primary,
            new VStack(
                    new TextBlock(() => $"Total Upgradable     {_state.Packages.Value.Count(p => p.Outdated),8}"),
                    new TextBlock(() => $"Log Lines            {_state.LogLineCount.Value,8}"),
                    new TextBlock(() => $"Elapsed Time        {_state.UpdateClock.Elapsed:hh\\:mm\\:ss}"),
                    new Rule(),
                    Widgets.MeterRow("cpu_usage", () => _state.CpuUsage.Value),
                    cpuSparkline,
                    Widgets.MeterRow("ram_alloc", () => _state.RamUsage.Value,
                        () => $"{_state.RamUsage.Value * 16:F1} / 16 GB"),
                    ramSparkline)
                .Spacing(Layout.Section));

        var diskPanel = new ComputedVisual(() =>
        {
            var (fraction, label) = SystemInfo.DiskUsage();
            return (Visual)Widgets.Panel(" disk_usage ", PanelAccent.Store,
                new VStack(
                        new TextBlock(label),
                        Widgets.Meter(fraction, Layout.MeterWidth, reverse: true))
                    .Spacing(Layout.Section));
        });

        var controls = new VStack(
                new Button("ABORT_PROCESS")
                    .Tone(ControlTone.Error)
                    .HorizontalAlignment(Align.Stretch)
                    .IsEnabled(_state.IsCommandRunning)
                    .Click(_updates.AbortUpdate),
                new Button("EXPORT_LOGS")
                    .Tone(ControlTone.Primary)
                    .HorizontalAlignment(Align.Stretch)
                    .Click(_updates.ExportLogs),
                diskPanel)
            .Spacing(Layout.Section);

        bool narrow = _state.Viewport.Value.Columns < Layout.NarrowWidth;
        if (narrow)
        {
            return Widgets.PanelTabs(_state.PanelTab,
                ("log",   () => Widgets.Panel(" root@system:~/updates/logs ", PanelAccent.Danger, _state.UpdateLog.Stretch()).Stretch()),
                ("stats", () => new VStack(stats, controls).Spacing(Layout.Section)));
        }

        var right = new DockLayout()
            .Top(stats)
            .Content(new Group().Content(new TextBlock(string.Empty)))
            .Bottom(controls);

        var grid = new Grid()
            .Columns(
                new ColumnDefinition { Width = GridLength.Star(3) },
                new ColumnDefinition { Width = GridLength.Star(1) }.MinWidth(Layout.RightColMin))
            .Rows(new RowDefinition { Height = GridLength.Star(1) })
            .Cell(Widgets.Panel(" root@system:~/updates/logs ", PanelAccent.Danger,
                    _state.UpdateLog.Stretch())
                .Stretch(), 0, 0)
            .Cell(right.Stretch(), 0, 1)
            .Stretch();

        return grid;
    }
}
