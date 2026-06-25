using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

/// <summary>Accent role assigned to a panel — drives border colour and gradient meters.</summary>
internal enum PanelAccent
{
    /// <summary>Cyan — navigation / primary panels.</summary>
    Primary,
    /// <summary>Amber — warnings / command bar.</summary>
    Warning,
    /// <summary>Green — success / healthy metrics.</summary>
    Success,
    /// <summary>Red — errors / danger.</summary>
    Danger,
    /// <summary>Magenta — store / discovery panels.</summary>
    Store,
    /// <summary>Blue — informational panels.</summary>
    Info,
}

/// <summary>Selects between the two supported gruvbox variants.</summary>
internal enum AppThemeVariant
{
    /// <summary>Warm dark background with pastel/retro accent tones.</summary>
    GruvboxDark,
    /// <summary>Warm light background with deeper accent tones for contrast.</summary>
    GruvboxLight,
}

/// <summary>
/// Terminal Core design tokens that the ANSI ColorScheme can't carry.
/// </summary>
internal static class Palette
{
    // ── Palette record ────────────────────────────────────────────────────────

    /// <summary>Immutable record that holds the full colour set for one theme variant.</summary>
    private sealed record PaletteSet(
        Color SurfaceLowest,
        Color SurfaceLow,
        Color SurfaceContainer,
        Color SurfaceHigh,
        Color SurfaceHighest,
        Color Outline,
        Color OutlineVariant,
        Color Foreground,
        Color Cyan,
        Color CyanBright,
        Color Amber,
        Color AmberBright,
        Color Green,
        Color GreenBright,
        Color Red,
        Color RedBright,
        Color Magenta,
        Color MagentaBright,
        Color Blue,
        Color BlueBright,
        Color OnFill);

    // ── Gruvbox Dark ─────────────────────────────────────────────────────────
    private static readonly PaletteSet GruvboxDarkSet = new(
        SurfaceLowest:    Color.Rgb(0x1d, 0x20, 0x21),
        SurfaceLow:       Color.Rgb(0x28, 0x28, 0x28),
        SurfaceContainer: Color.Rgb(0x3c, 0x38, 0x36),
        SurfaceHigh:      Color.Rgb(0x50, 0x49, 0x45),
        SurfaceHighest:   Color.Rgb(0x66, 0x5c, 0x54),
        Outline:          Color.Rgb(0x7c, 0x6f, 0x64),
        OutlineVariant:   Color.Rgb(0x3c, 0x38, 0x36),
        Foreground:       Color.Rgb(0xeb, 0xdb, 0xb2),
        Cyan:             Color.Rgb(0x68, 0x9d, 0x6a),
        CyanBright:       Color.Rgb(0x8e, 0xc0, 0x7c),
        Amber:            Color.Rgb(0xd7, 0x99, 0x21),
        AmberBright:      Color.Rgb(0xfa, 0xbd, 0x2f),
        Green:            Color.Rgb(0x98, 0x97, 0x1a),
        GreenBright:      Color.Rgb(0xb8, 0xbb, 0x26),
        Red:              Color.Rgb(0xcc, 0x24, 0x1d),
        RedBright:        Color.Rgb(0xfb, 0x49, 0x34),
        Magenta:          Color.Rgb(0xb1, 0x62, 0x86),
        MagentaBright:    Color.Rgb(0xd3, 0x86, 0x9b),
        Blue:             Color.Rgb(0x45, 0x85, 0x88),
        BlueBright:       Color.Rgb(0x83, 0xa5, 0x98),
        OnFill:           Color.Rgb(0x1d, 0x20, 0x21));

    // ── Gruvbox Light ────────────────────────────────────────────────────────
    private static readonly PaletteSet GruvboxLightSet = new(
        SurfaceLowest:    Color.Rgb(0xf9, 0xf5, 0xd7),
        SurfaceLow:       Color.Rgb(0xfb, 0xf1, 0xc7),
        SurfaceContainer: Color.Rgb(0xeb, 0xdb, 0xb2),
        SurfaceHigh:      Color.Rgb(0xd5, 0xc4, 0xa1),
        SurfaceHighest:   Color.Rgb(0xbd, 0xae, 0x93),
        Outline:          Color.Rgb(0x7c, 0x6f, 0x64),
        OutlineVariant:   Color.Rgb(0xd5, 0xc4, 0xa1),
        Foreground:       Color.Rgb(0x3c, 0x38, 0x36),
        Cyan:             Color.Rgb(0x42, 0x7b, 0x58),
        CyanBright:       Color.Rgb(0x68, 0x9d, 0x6a),
        Amber:            Color.Rgb(0xb5, 0x76, 0x14),
        AmberBright:      Color.Rgb(0xd7, 0x99, 0x21),
        Green:            Color.Rgb(0x79, 0x74, 0x0e),
        GreenBright:      Color.Rgb(0x98, 0x97, 0x1a),
        Red:              Color.Rgb(0x9d, 0x00, 0x06),
        RedBright:        Color.Rgb(0xcc, 0x24, 0x1d),
        Magenta:          Color.Rgb(0x8f, 0x3f, 0x71),
        MagentaBright:    Color.Rgb(0xb1, 0x62, 0x86),
        Blue:             Color.Rgb(0x07, 0x66, 0x78),
        BlueBright:       Color.Rgb(0x45, 0x85, 0x88),
        OnFill:           Color.Rgb(0xfb, 0xf1, 0xc7));

    // ── Active set (default = dark) ───────────────────────────────────────────
    private static PaletteSet _current = GruvboxDarkSet;

    /// <summary>Swaps the active palette to the specified variant.</summary>
    public static void Apply(AppThemeVariant v)
        => _current = v == AppThemeVariant.GruvboxLight ? GruvboxLightSet : GruvboxDarkSet;

    // ── Surfaces ──────────────────────────────────────────────────────────────
    public static Color SurfaceLowest    => _current.SurfaceLowest;
    public static Color SurfaceLow       => _current.SurfaceLow;
    public static Color SurfaceContainer => _current.SurfaceContainer;
    public static Color SurfaceHigh      => _current.SurfaceHigh;
    public static Color SurfaceHighest   => _current.SurfaceHighest;

    // ── Border / outline ──────────────────────────────────────────────────────
    public static Color Outline        => _current.Outline;
    public static Color OutlineVariant => _current.OutlineVariant;

    // ── Semantic fills (accent palette) ───────────────────────────────────────
    public static Color Cyan        => _current.Cyan;
    public static Color CyanBright  => _current.CyanBright;

    public static Color Amber       => _current.Amber;
    public static Color AmberBright => _current.AmberBright;

    public static Color Green       => _current.Green;
    public static Color GreenBright => _current.GreenBright;

    public static Color Red         => _current.Red;
    public static Color RedBright   => _current.RedBright;

    public static Color Magenta       => _current.Magenta;
    public static Color MagentaBright => _current.MagentaBright;

    public static Color Blue       => _current.Blue;
    public static Color BlueBright => _current.BlueBright;

    // ── Legacy aliases (kept so existing call sites compile) ──────────────────
    public static Color GreenFill      => Green;
    public static Color AmberFill      => Amber;
    public static Color CyanFill       => Cyan;
    public static Color CyanFillBright => CyanBright;
    public static Color RedFill        => Red;

    /// <summary>Foreground used on light/dark surfaces.</summary>
    public static Color Foreground => _current.Foreground;

    /// <summary>Foreground used on top of any solid fill.</summary>
    public static Color OnFill => _current.OnFill;

    // ── Accent lookups ─────────────────────────────────────────────────────────
    public static Color AccentColor(PanelAccent accent) => accent switch
    {
        PanelAccent.Primary => Cyan,
        PanelAccent.Warning => Amber,
        PanelAccent.Success => Green,
        PanelAccent.Danger  => Red,
        PanelAccent.Store   => Magenta,
        PanelAccent.Info    => Blue,
        _                   => Cyan,
    };

    public static Color AccentBright(PanelAccent accent) => accent switch
    {
        PanelAccent.Primary => CyanBright,
        PanelAccent.Warning => AmberBright,
        PanelAccent.Success => GreenBright,
        PanelAccent.Danger  => RedBright,
        PanelAccent.Store   => MagentaBright,
        PanelAccent.Info    => BlueBright,
        _                   => CyanBright,
    };
}

internal static class AppTheme
{
    internal static Theme Create()
    {
        // Determine brightness from the current surface colour: dark variant has a low
        // average luminance (<0x80), light variant has a high average luminance.
        var s = Palette.SurfaceLowest;
        var isLight = (s.R + s.G + s.B) / 3 > 0x80;

        var scheme = new ColorScheme
        {
            Name       = isLight ? "Gruvbox Light" : "Gruvbox Dark",
            Background = Palette.SurfaceLowest,
            Foreground = Palette.Foreground,
            Black      = Palette.SurfaceLowest,
            Red        = Palette.Red,
            Green      = Palette.Green,
            Yellow     = Palette.Amber,
            Blue       = Palette.Blue,
            Purple     = Palette.Magenta,
            Cyan       = Palette.Cyan,
            White      = Palette.Foreground,
            BrightBlack  = Palette.OutlineVariant,
            BrightRed    = Palette.RedBright,
            BrightGreen  = Palette.GreenBright,
            BrightYellow = Palette.AmberBright,
            BrightBlue   = Palette.BlueBright,
            BrightPurple = Palette.MagentaBright,
            BrightCyan   = Palette.CyanBright,
            BrightWhite  = isLight ? Color.Rgb(0x28, 0x28, 0x28) : Color.Rgb(0xFF, 0xFF, 0xFF),
        };

        var brightness = isLight ? ThemeSchemeBrightness.Light : ThemeSchemeBrightness.Dark;
        return Theme.FromScheme(scheme, brightness, ThemeAccentColor.Cyan);
    }
}
