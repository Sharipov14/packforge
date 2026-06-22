using PackForge.Core;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal enum ChipTone { Outdated, Stable, Inactive, Installed, Available }

internal static class Widgets
{
    internal static Visual StatCard(string label, string value, ControlTone tone)
    {
        var tag = tone switch
        {
            ControlTone.Success => "success",
            ControlTone.Warning => "warning",
            ControlTone.Error => "error",
            _ => "primary",
        };

        return new Group()
            .Padding(Layout.GroupPad)
            .Content(new VStack(
                    new Markup($"[dim]{label}[/]"),
                    new Markup($"[bold {tag}]{value}[/]"))
                .Spacing(Layout.Section));
    }

    internal static Visual SourceRow(string name, string url, State<bool> enabled)
    {
        return new Group()
            .Padding(Layout.FieldPad)
            .Content(new HStack(
                    new VStack(
                            new TextBlock(name),
                            new Markup($"[dim]{url}[/]"))
                        .HorizontalAlignment(Align.Stretch),
                    new Markup(() => enabled.Value ? "[success]STABLE[/]" : "[dim]INACTIVE[/]"),
                    new Switch().IsOn(enabled))
                .Spacing(Layout.Item)
                .HorizontalAlignment(Align.Stretch));
    }

    internal static string PageTitle(AppPage page, string? selectedName = null)
        => page switch
        {
            AppPage.Dashboard => "SYSTEM DASHBOARD",
            AppPage.PackageDetails => selectedName is not null ? $"/usr/bin/{selectedName}" : "PACKAGE",
            AppPage.UpdateLogs => "ACTIVE_UPDATE",
            AppPage.SourceSettings => "~/config/sources.yaml",
            AppPage.Store => "STORE",
            _ => "PKG_MGR",
        };

    internal static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..(max - 1)] + "…";

    internal static Visual Loader(string label, Func<bool> isActive, ControlTone tone)
        => new Spinner()
            .IsActive(isActive)
            .Label(new Markup($"[dim]{label}[/]"))
            .Tone(tone);

    private static Visual Chip(string text, string bgHex)
        => new Markup($"[bg:{bgHex} black] {text} [/]");

    internal static Visual StatusChip(ChipTone tone)
    {
        return tone switch
        {
            ChipTone.Outdated => Chip("OUTDATED", "#ffba20"),
            ChipTone.Stable => Chip("STABLE", "#00e639"),
            ChipTone.Installed => Chip($"{Glyphs.Check} INSTALLED", "#00e639"),
            ChipTone.Inactive => new Markup("[dim][[ INACTIVE ]][/]"),
            ChipTone.Available => new Markup("[dim][[ AVAILABLE ]][/]"),
            _ => new Markup("[dim] — [/]"),
        };
    }

    internal static Visual StatusChip(bool outdated)
        => StatusChip(outdated ? ChipTone.Outdated : ChipTone.Stable);

    internal static Visual Badge(string text, ControlTone tone)
    {
        var bg = tone switch
        {
            ControlTone.Success => "#00e639",
            ControlTone.Warning => "#ffba20",
            ControlTone.Error => "#ff6b6b",
            _ => "#00dddd",
        };
        return Chip(text, bg);
    }

    internal static Visual PrimaryButton(string label, Action onClick, string? icon = null)
    {
        var text = icon is null ? label : $"{icon}  {label}";
        return new Button(text)
            .HorizontalAlignment(Align.Stretch)
            .Style(ButtonStyle.Default with
            {
                Padding = Layout.FieldPad,
                Normal = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                Hovered = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                Pressed = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                Focused = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
            })
            .Click(onClick);
    }

    internal static string Histogram(IReadOnlyList<double> values, int width = 20)
    {
        if (values.Count == 0) return new string('░', width);
        var max = values.Max();
        if (max <= 0) return new string('░', width);
        var levels = " ▁▂▃▄▅▆▇█";
        var sb = new System.Text.StringBuilder();
        var perCell = (double)values.Count / width;
        for (var i = 0; i < width; i++)
        {
            var startIdx = (int)(i * perCell);
            var endIdx = Math.Min((int)((i + 1) * perCell), values.Count);
            double avg = startIdx < endIdx
                ? values.Skip(startIdx).Take(endIdx - startIdx).Average()
                : values[Math.Min(startIdx, values.Count - 1)];
            var level = (int)Math.Round(avg / max * 8);
            sb.Append(levels[Math.Clamp(level, 0, 8)]);
        }
        return sb.ToString();
    }

    internal static Visual ManagerNavItem(
        string managerId,
        string displayName,
        bool isActive,
        bool isInstalled,
        Action onClick,
        int outdatedCount = 0)
    {
        string label;
        if (!isInstalled)
        {
            label = $"[dim]  {displayName}[/]";
        }
        else if (isActive)
        {
            var badge = outdatedCount > 0 ? $"  [#690005]{Glyphs.Warning}{outdatedCount}[/]" : string.Empty;
            label = $"[black]{Glyphs.Active} {displayName}[/]{badge}";
        }
        else
        {
            var badge = outdatedCount > 0 ? $"  [warning]{Glyphs.Warning}{outdatedCount}[/]" : string.Empty;
            label = $"  {displayName}{badge}";
        }

        return new Button(new Markup(label))
            .HorizontalAlignment(Align.Stretch)
            .Style(() => isActive
                ? ButtonStyle.Default with
                {
                    ShowBorder = false,
                    Padding = Layout.FieldPad,
                    Normal = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    Hovered = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                    Pressed = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    Focused = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                }
                : ButtonStyle.Default with { Padding = Layout.FieldPad })
            .Click(onClick);
    }

    internal static Visual ManagerSettingRow(
        IPackageProvider provider,
        bool isInstalled,
        State<bool> visibilityState,
        Action onInstall)
    {
        Visual rightControl;
        if (isInstalled)
        {
            rightControl = new HStack(
                    StatusChip(ChipTone.Stable),
                    new Switch().IsOn(visibilityState))
                .Spacing(Layout.Item);
        }
        else
        {
            rightControl = new HStack(
                    StatusChip(ChipTone.Inactive),
                    new Button("INSTALL")
                        .Tone(ControlTone.Warning)
                        .Click(onInstall))
                .Spacing(Layout.Item);
        }

        return new Group()
            .Padding(Layout.FieldPad)
            .Content(new HStack(
                    new VStack(
                            new TextBlock(provider.DisplayName),
                            new Markup($"[dim]{provider.Command}[/]"))
                        .HorizontalAlignment(Align.Stretch),
                    rightControl)
                .Spacing(Layout.Item)
                .HorizontalAlignment(Align.Stretch));
    }

    internal static Visual TopTabs(AppPage activePage, Action<AppPage> onSelect)
    {
        Visual Tab(string label, AppPage target, Func<bool> isActive)
            => new Button(new Markup(() =>
                    isActive() ? $"[black]{label}[/]" : label))
                .Style(() => isActive()
                    ? ButtonStyle.Default with
                    {
                        ShowBorder = false,
                        Padding = Layout.FieldPad,
                        Normal = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Hovered = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                        Pressed = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Focused = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    }
                    : ButtonStyle.Default with { Padding = Layout.FieldPad })
                .Click(() => onSelect(target));

        return new HStack(
                Tab("system", AppPage.Dashboard,
                    () => activePage is AppPage.Dashboard or AppPage.PackageDetails),
                Tab("logs", AppPage.UpdateLogs, () => activePage == AppPage.UpdateLogs),
                Tab("settings", AppPage.SourceSettings, () => activePage == AppPage.SourceSettings),
                Tab("store", AppPage.Store, () => activePage == AppPage.Store))
            .Spacing(Layout.Gap);
    }
}
