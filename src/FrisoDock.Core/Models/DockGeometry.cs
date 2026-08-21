namespace FrisoDock.Core.Models;

/// <summary>Screen edge the dock is anchored to.</summary>
public enum DockEdge
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3,
}

/// <summary>Rectangle in physical pixels, independent of any UI framework.</summary>
/// <param name="Left">Left coordinate.</param>
/// <param name="Top">Top coordinate.</param>
/// <param name="Right">Right coordinate (exclusive).</param>
/// <param name="Bottom">Bottom coordinate (exclusive).</param>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public static PixelRect FromSize(int left, int top, int width, int height)
    {
        return new PixelRect(left, top, left + width, top + height);
    }
}

/// <summary>A monitor's area: full bounds and work area (appbars discounted).</summary>
/// <param name="Bounds">The monitor's full rectangle.</param>
/// <param name="WorkArea">The monitor's work area.</param>
/// <param name="IsPrimary">Whether it is the primary monitor.</param>
/// <param name="DpiScale">DPI scale factor (1.0 = 96 DPI).</param>
public readonly record struct MonitorInfo(PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, double DpiScale);
