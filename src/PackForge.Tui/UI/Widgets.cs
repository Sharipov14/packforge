using PackForge.Core;
using System.Text;
using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Controls;
using XenoAtom.Terminal.UI.Geometry;
using XenoAtom.Terminal.UI.Input;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

internal enum ChipTone { Outdated, Stable, Inactive, Installed, Available }

internal static class Widgets
{
    // ── Shared control styles ─────────────────────────────────────────────────

    /// <summary>Pastel/soft red used for the low-emphasis "Exit" action.</summary>
    private static readonly Color PastelRed       = Color.Rgb(0xea, 0x6f, 0x6f);
    private static readonly Color PastelRedBright = Color.Rgb(0xf2, 0x92, 0x92);

    /// <summary>Filled-cyan "main color" button style — the app's primary action look.</summary>
    internal static ButtonStyle PrimaryButtonStyle => ButtonStyle.Default with
    {
        Padding = Layout.FieldPad,
        Normal  = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
        Hovered = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
        Pressed = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
        Focused = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
    };

    /// <summary>Filled pastel-red button style for the "Exit" action.</summary>
    internal static ButtonStyle PastelRedButtonStyle => ButtonStyle.Default with
    {
        Padding = Layout.FieldPad,
        Normal  = Style.None.WithBackground(PastelRed).WithForeground(Palette.OnFill),
        Hovered = Style.None.WithBackground(PastelRedBright).WithForeground(Palette.OnFill),
        Pressed = Style.None.WithBackground(PastelRed).WithForeground(Palette.OnFill),
        Focused = Style.None.WithBackground(PastelRedBright).WithForeground(Palette.OnFill),
    };

    private static readonly Color PastelBlue       = Color.Rgb(0x76, 0x9a, 0xcc);
    private static readonly Color PastelBlueBright = Color.Rgb(0x9a, 0xb7, 0xe0);

    internal static ButtonStyle PastelBlueButtonStyle => ButtonStyle.Default with
    {
        Padding = Layout.FieldPad,
        Normal  = Style.None.WithBackground(PastelBlue).WithForeground(Palette.OnFill),
        Hovered = Style.None.WithBackground(PastelBlueBright).WithForeground(Palette.OnFill),
        Pressed = Style.None.WithBackground(PastelBlue).WithForeground(Palette.OnFill),
        Focused = Style.None.WithBackground(PastelBlueBright).WithForeground(Palette.OnFill),
    };

    /// <summary>Switch (checkbox) styled with the cyan main color when on.</summary>
    internal static SwitchStyle CyanSwitchStyle => SwitchStyle.Default with
    {
        TrackOn         = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
        TrackOnActive   = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
        TrackOnInactive = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
        ThumbOn         = Style.None.WithForeground(Palette.CyanBright),
        TrackOff        = Style.None.WithForeground(Palette.OutlineVariant),
        ThumbOff        = Style.None.WithForeground(Palette.OutlineVariant),
    };

    /// <summary>A checkbox/<see cref="Switch"/> bound to <paramref name="on"/>, tinted cyan when on.</summary>
    internal static Switch CyanSwitch(State<bool> on)
        => new Switch().IsOn(on).Style(CyanSwitchStyle);

    // ── Panel factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a rounded-border <see cref="Group"/> with a per-accent coloured border and a
    /// btop-style lowercase title (brighter first letter).
    /// </summary>
    internal static Group Panel(string title, PanelAccent accent, Visual content)
    {
        var accentColor = Palette.AccentColor(accent);
        var brightColor = Palette.AccentBright(accent);
        var accentHex = $"#{accentColor.R:x2}{accentColor.G:x2}{accentColor.B:x2}";
        var brightHex = $"#{brightColor.R:x2}{brightColor.G:x2}{brightColor.B:x2}";
        var fgHex = $"#{Palette.Foreground.R:x2}{Palette.Foreground.G:x2}{Palette.Foreground.B:x2}";

        var labelMarkup = BuildTitleMarkup(title, fgHex, fgHex);

        return new Group()
            .TopLeftText(new Markup(labelMarkup))
            .Padding(Layout.GroupPad)
            .Content(content)
            .Style(GroupStyle.Rounded with
            {
                BorderCellStyle        = Style.None.WithForeground(accentColor),
                FocusedBorderCellStyle = Style.None.WithForeground(brightColor) | TextStyle.Bold,
                LabelBackgroundStyle   = Style.None | TextStyle.Bold,
            });
    }

    /// <summary>Overload accepting a computed-content factory.</summary>
    internal static Group Panel(string title, PanelAccent accent, Func<Visual> content)
        => Panel(title, accent, new ComputedVisual(content));

    private static string BuildTitleMarkup(string title, string accentHex, string brightHex)
    {
        // Lowercase the title; brighten the first non-space character (btop style).
        var lower = title.ToLowerInvariant();
        if (lower.Length == 0)
            return $"[{accentHex}]{lower}[/]";

        // Find first real character (skip leading spaces that are part of the title padding)
        int firstIdx = 0;
        while (firstIdx < lower.Length && lower[firstIdx] == ' ')
            firstIdx++;

        if (firstIdx >= lower.Length)
            return $"[{accentHex}]{lower}[/]";

        var before = lower[..firstIdx];             // leading spaces
        var first  = lower[firstIdx].ToString();    // first visible char (bright)
        var rest   = lower[(firstIdx + 1)..];       // remainder

        return $"[{accentHex}]{before}[/][bold {brightHex}]{first}[/][{accentHex}]{rest}[/]";
    }

    // ── Gradient meter ───────────────────────────────────────────────────────

    /// <summary>
    /// Builds a green→amber→red gradient meter as a <see cref="Markup"/> string.
    /// Each cell is coloured by a 3-stop ramp; filled cells render '█', empty cells '░'.
    /// </summary>
    /// <param name="fraction">Current fill fraction in [0, 1].</param>
    /// <param name="width">Width in terminal cells.</param>
    /// <param name="reverse">When true the ramp is reversed (green = full, e.g. "free space").</param>
    internal static Visual Meter(Func<double> fraction, int width = 0, bool reverse = false)
    {
        if (width <= 0) width = Layout.MeterWidth;
        return new ComputedVisual(() => BuildMeterMarkup(fraction(), width, reverse));
    }

    /// <summary>Static fraction overload.</summary>
    internal static Visual Meter(double fraction, int width = 0, bool reverse = false)
        => Meter(() => fraction, width, reverse);

    private static Visual BuildMeterMarkup(double fraction, int width, bool reverse)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        int filled = (int)Math.Round(fraction * width);

        // 3-stop green→amber→red ramp colours
        var green = Palette.Green;
        var amber = Palette.Amber;
        var red   = Palette.Red;
        var dimTrack = Palette.OutlineVariant;
        var dimHex = $"#{dimTrack.R:x2}{dimTrack.G:x2}{dimTrack.B:x2}";

        var sb = new StringBuilder();
        for (int i = 0; i < width; i++)
        {
            // t goes 0→1 across the bar width
            float t = width > 1 ? (float)i / (width - 1) : 0f;
            if (reverse) t = 1f - t;

            Color cellColor;
            if (t < 0.5f)
                cellColor = Color.Mix(green, amber, t * 2f);
            else
                cellColor = Color.Mix(amber, red, (t - 0.5f) * 2f);

            var hex = $"#{cellColor.R:x2}{cellColor.G:x2}{cellColor.B:x2}";

            if (i < filled)
                sb.Append($"[{hex}]█[/]");
            else
                sb.Append($"[{dimHex}]░[/]");
        }

        return new Markup(sb.ToString());
    }

    /// <summary>
    /// A label + percent line followed by a gradient meter bar — matches btop rows.
    /// </summary>
    internal static Visual MeterRow(string label, Func<double> fraction, string suffix = "", int width = 0)
        => MeterRow(label, fraction, suffix.Length > 0 ? () => suffix : null, width);

    /// <summary>
    /// A label + percent line followed by a gradient meter bar — matches btop rows (reactive suffix).
    /// </summary>
    internal static Visual MeterRow(string label, Func<double> fraction, Func<string>? suffix, int width = 0)
    {
        if (width <= 0) width = Layout.MeterWidth;
        return new VStack(
                new ComputedVisual(() =>
                {
                    var f = Math.Clamp(fraction(), 0.0, 1.0);
                    var pct = (int)(f * 100);
                    var suf = suffix?.Invoke() ?? string.Empty;
                    return (Visual)new Markup($"[dim]{label}[/]  [bold]{pct,3}%[/]{(suf.Length > 0 ? $"  [dim]{suf}[/]" : "")}");
                }),
                Meter(fraction, width))
            .Spacing(0);
    }

    // ── Existing widgets (unchanged logic, palette refs updated) ────────────

    /// <summary>
    /// Borderless, stretched stat cell used inside the metadata panel inner grid.
    /// Returns a <see cref="VStack"/> of label + coloured value with no outer border so
    /// the containing <see cref="Widgets.Panel"/> provides the single shared border.
    /// </summary>
    internal static Visual StatCard(string label, string value, ControlTone tone)
    {
        Visual valueVisual = tone switch
        {
            ControlTone.Success => new Markup($"[bold success]{value}[/]"),
            ControlTone.Warning => new Markup($"[bold warning]{value}[/]"),
            ControlTone.Error   => new Markup($"[bold error]{value}[/]"),
            ControlTone.Primary => new Markup($"[bold primary]{value}[/]"),
            _                   => (Visual)new TextBlock(value),
        };

        return new VStack(
                new Markup($"[dim]{label}[/]"),
                valueVisual)
            .Spacing(Layout.Section)
            .HorizontalAlignment(Align.Stretch);
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
                    CyanSwitch(enabled))
                .Spacing(Layout.Item)
                .HorizontalAlignment(Align.Stretch));
    }

    internal static string PageTitle(AppPage page, string? selectedName = null)
        => page switch
        {
            AppPage.Dashboard      => "SYSTEM DASHBOARD",
            AppPage.PackageDetails => selectedName is not null ? $"/usr/bin/{selectedName}" : "PACKAGE",
            AppPage.UpdateLogs     => "ACTIVE_UPDATE",
            AppPage.SourceSettings => "~/config/sources.yaml",
            AppPage.Store          => "STORE",
            _                      => "PKG_MGR",
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
        // Use Palette constants instead of hardcoded hex literals
        var amberHex   = $"#{Palette.Amber.R:x2}{Palette.Amber.G:x2}{Palette.Amber.B:x2}";
        var greenHex   = $"#{Palette.Green.R:x2}{Palette.Green.G:x2}{Palette.Green.B:x2}";

        return tone switch
        {
            ChipTone.Outdated  => Chip("OUTDATED",           amberHex),
            ChipTone.Stable    => new Markup($"[{greenHex}]STABLE[/]"),
            ChipTone.Installed => new Markup($"[{greenHex}]{Glyphs.Check} INSTALLED[/]"),
            ChipTone.Inactive  => new Markup("[dim][[ INACTIVE ]][/]"),
            ChipTone.Available => new Markup("[dim][[ AVAILABLE ]][/]"),
            _                  => new Markup("[dim] — [/]"),
        };
    }

    internal static Visual StatusChip(bool outdated)
        => StatusChip(outdated ? ChipTone.Outdated : ChipTone.Stable);

    internal static Visual Badge(string text, ControlTone tone)
    {
        var bg = tone switch
        {
            ControlTone.Success => $"#{Palette.Green.R:x2}{Palette.Green.G:x2}{Palette.Green.B:x2}",
            ControlTone.Warning => $"#{Palette.Amber.R:x2}{Palette.Amber.G:x2}{Palette.Amber.B:x2}",
            ControlTone.Error   => $"#{Palette.Red.R:x2}{Palette.Red.G:x2}{Palette.Red.B:x2}",
            _                   => $"#{Palette.Cyan.R:x2}{Palette.Cyan.G:x2}{Palette.Cyan.B:x2}",
        };
        return Chip(text, bg);
    }

    /// <summary>
    /// Narrow-mode panel tabs: a button tab-bar above a <see cref="ContentSwitcher"/> body.
    /// Only the selected tab's content is shown. <paramref name="selected"/> is the shared tab index.
    /// </summary>
    internal static Visual PanelTabs(State<int> selected, params (string label, Func<Visual> content)[] tabs)
    {
        Visual TabButton(string label, int index)
            => new Button(new Markup(() =>
                    selected.Value == index ? $"[bold]{label}[/]" : label))
                .Style(() => selected.Value == index
                    ? ButtonStyle.Default with
                    {
                        ShowBorder = false,
                        Padding    = Layout.FieldPad,
                        Normal     = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Hovered    = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                        Pressed    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Focused    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    }
                    : ButtonStyle.Default with { Padding = Layout.FieldPad })
                .Click(() => selected.Value = index);

        var tabBar = new HStack([.. tabs.Select((t, i) => (Visual)TabButton(t.label, i))])
            .Spacing(Layout.Gap);

        // Evaluate each tab's content ONCE so its visuals are attached to the switcher a single
        // time. Wrapping in a per-tab ComputedVisual would re-run the factory on every prepare
        // pass and re-attach the same (already-parented) panel objects → "visual already has a
        // parent". Tab switching just toggles which already-built child the switcher shows.
        var switcher = new ContentSwitcher([.. tabs.Select(t => t.content())])
            .SelectedIndex(() => Math.Clamp(selected.Value, 0, tabs.Length - 1));

        return new DockLayout()
            .Top(tabBar.Pad(new Thickness(1, 1, 1, 1)))
            .Content(switcher.Stretch())
            .Stretch();
    }

    internal static Visual PrimaryButton(string label, Action onClick, string? icon = null)
    {
        var text = icon is null ? label : $"{icon}  {label}";
        return new Button(text)
            .HorizontalAlignment(Align.Stretch)
            .Style(PrimaryButtonStyle)
            .Click(onClick);
    }

    internal static string Histogram(IReadOnlyList<double> values, int width = 20)
    {
        if (values.Count == 0) return new string('░', width);
        var max = values.Max();
        if (max <= 0) return new string('░', width);
        var levels = " ▁▂▃▄▅▆▇█";
        var sb = new StringBuilder();
        var perCell = (double)values.Count / width;
        for (var i = 0; i < width; i++)
        {
            var startIdx = (int)(i * perCell);
            var endIdx   = Math.Min((int)((i + 1) * perCell), values.Count);
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
        int outdatedCount = 0,
        bool isKeyboardFocused = false)
    {
        string label;
        if (!isInstalled)
        {
            label = $"[dim]  {displayName}[/]";
        }
        else if (isActive)
        {
            var badge = outdatedCount > 0 ? $"  [#690005]{Glyphs.Warning}{outdatedCount}[/]" : string.Empty;
            label = $"[bold]{Glyphs.Active} {displayName}[/]{badge}";
        }
        else
        {
            var badge = outdatedCount > 0 ? $"  [warning]{Glyphs.Warning}{outdatedCount}[/]" : string.Empty;
            var prefix = isKeyboardFocused ? "[primary]▶[/]" : " ";
            label = $"{prefix} {displayName}{badge}";
        }

        return new Button(new Markup(label))
            .HorizontalAlignment(Align.Stretch)
            .Style(() => isActive
                ? ButtonStyle.Default with
                {
                    ShowBorder = false,
                    Padding    = Layout.FieldPad,
                    Normal     = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    Hovered    = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                    Pressed    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    Focused    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
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
                    CyanSwitch(visibilityState))
                .Spacing(Layout.Item);
        }
        else
        {
            rightControl = new HStack(
                    StatusChip(ChipTone.Inactive),
                    new Button("INSTALL")
                        .Style(PrimaryButtonStyle)
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
                    isActive() ? $"[bold]{label}[/]" : label))
                .Style(() => isActive()
                    ? ButtonStyle.Default with
                    {
                        ShowBorder = false,
                        Padding    = Layout.FieldPad,
                        Normal     = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Hovered    = Style.None.WithBackground(Palette.CyanFillBright).WithForeground(Palette.OnFill),
                        Pressed    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                        Focused    = Style.None.WithBackground(Palette.CyanFill).WithForeground(Palette.OnFill),
                    }
                    : ButtonStyle.Default with { Padding = Layout.FieldPad })
                .Click(() => onSelect(target));

        return new HStack(
                Tab("system",   AppPage.Dashboard,
                    () => activePage is AppPage.Dashboard or AppPage.PackageDetails),
                Tab("logs",     AppPage.UpdateLogs,     () => activePage == AppPage.UpdateLogs),
                Tab("settings", AppPage.SourceSettings, () => activePage == AppPage.SourceSettings),
                Tab("store",    AppPage.Store,           () => activePage == AppPage.Store))
            .Spacing(Layout.Gap);
    }
}
