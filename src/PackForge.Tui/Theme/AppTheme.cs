using XenoAtom.Terminal.UI;
using XenoAtom.Terminal.UI.Styling;

namespace PackForge.Tui;

/// <summary>
/// Terminal Core design tokens that the ANSI ColorScheme can't carry.
/// </summary>
internal static class Palette
{
    public static readonly Color SurfaceLowest = Color.Rgb(0x0E, 0x0E, 0x0E);
    public static readonly Color SurfaceLow = Color.Rgb(0x1C, 0x1B, 0x1B);
    public static readonly Color SurfaceContainer = Color.Rgb(0x20, 0x1F, 0x1F);
    public static readonly Color SurfaceHigh = Color.Rgb(0x2A, 0x2A, 0x2A);
    public static readonly Color SurfaceHighest = Color.Rgb(0x35, 0x35, 0x34);

    public static readonly Color Outline = Color.Rgb(0x83, 0x94, 0x93);
    public static readonly Color OutlineVariant = Color.Rgb(0x3A, 0x4A, 0x49);

    public static readonly Color GreenFill = Color.Rgb(0x00, 0xE6, 0x39);
    public static readonly Color AmberFill = Color.Rgb(0xFF, 0xBA, 0x20);
    public static readonly Color CyanFill = Color.Rgb(0x00, 0xDD, 0xDD);
    public static readonly Color CyanFillBright = Color.Rgb(0x55, 0xFF, 0xFF);
    public static readonly Color RedFill = Color.Rgb(0xFF, 0x6B, 0x6B);

    /// <summary>Dark foreground used on top of any solid fill.</summary>
    public static readonly Color OnFill = Color.Rgb(0x0E, 0x0E, 0x0E);
}

internal static class AppTheme
{
    internal static Theme Create()
    {
        var scheme = new ColorScheme
        {
            Name = "Terminal Core",
            Background = Color.Rgb(0x13, 0x13, 0x13),
            Foreground = Color.Rgb(0xE5, 0xE2, 0xE1),
            Black = Color.Rgb(0x0E, 0x0E, 0x0E),
            Red = Color.Rgb(0xFF, 0x6B, 0x6B),
            Green = Color.Rgb(0x00, 0xE6, 0x39),
            Yellow = Color.Rgb(0xFF, 0xBA, 0x20),
            Blue = Color.Rgb(0x00, 0xDD, 0xDD),
            Purple = Color.Rgb(0xCA, 0x64, 0xF3),
            Cyan = Color.Rgb(0x00, 0xFB, 0xFB),
            White = Color.Rgb(0xB9, 0xCA, 0xC9),
            BrightBlack = Color.Rgb(0x3A, 0x4A, 0x49),
            BrightRed = Color.Rgb(0xFF, 0xB4, 0xAB),
            BrightGreen = Color.Rgb(0x72, 0xFF, 0x70),
            BrightYellow = Color.Rgb(0xFF, 0xDE, 0xA8),
            BrightBlue = Color.Rgb(0x55, 0xFF, 0xFF),
            BrightPurple = Color.Rgb(0xE4, 0xA0, 0xFF),
            BrightCyan = Color.Rgb(0xA0, 0xFF, 0xFF),
            BrightWhite = Color.Rgb(0xFF, 0xFF, 0xFF),
        };

        return Theme.FromScheme(scheme, ThemeSchemeBrightness.Dark, ThemeAccentColor.Cyan);
    }
}
