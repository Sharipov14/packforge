using XenoAtom.Terminal.UI.Geometry;

namespace PackForge.Tui;

/// <summary>Layout design tokens — single source of truth for spacing and sizing.</summary>
internal static class Layout
{
    public static readonly Thickness PagePad = new(2, 1, 2, 1);
    public static readonly Thickness GroupPad = new(1);
    public static readonly Thickness FieldPad = new(1, 0, 1, 0);

    public const int Section = 1;
    public const int Item = 1;
    public const int Gap = 2;
    public const int SidebarWidth = 26;
    public const int SearchMaxWidth = 40;
    public const int NameColMax = 28;
    public const int DescColMax = 48;
    public const int RightColMin = 22;
}
