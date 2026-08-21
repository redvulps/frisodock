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

    /// <summary>Whether the point falls inside this rectangle.</summary>
    public bool Contains(PixelPoint point)
    {
        return point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
    }

    /// <summary>Whether this rectangle and the other share any area.</summary>
    public bool IntersectsWith(PixelRect other)
    {
        // Exclusive edges: two rectangles that merely touch do not overlap. That is what makes an
        // empty rectangle — the default for whoever never had its bounds read — never match anything.
        return Left < other.Right
            && Right > other.Left
            && Top < other.Bottom
            && Bottom > other.Top;
    }

    /// <summary>Area shared with the other rectangle; zero when they do not touch.</summary>
    public long IntersectionArea(PixelRect other)
    {
        long width = Math.Min(Right, other.Right) - Math.Max(Left, other.Left);
        long height = Math.Min(Bottom, other.Bottom) - Math.Max(Top, other.Top);

        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        return width * height;
    }

    public static PixelRect FromSize(int left, int top, int width, int height)
    {
        return new PixelRect(left, top, left + width, top + height);
    }
}

/// <summary>Point in physical pixels.</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public readonly record struct PixelPoint(int X, int Y);

/// <summary>Dimensions in physical pixels.</summary>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct PixelSize(int Width, int Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// The largest rectangle with this aspect ratio that fits the given bound, centred in it.
    /// It keeps the thumbnail from distorting the window it mirrors.
    /// </summary>
    public PixelRect FitCentered(PixelRect bounds)
    {
        if (IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        double scale = Math.Min(bounds.Width / (double)Width, bounds.Height / (double)Height);
        int width = Math.Max(1, (int)Math.Round(Width * scale));
        int height = Math.Max(1, (int)Math.Round(Height * scale));

        return PixelRect.FromSize(
            bounds.Left + ((bounds.Width - width) / 2),
            bounds.Top + ((bounds.Height - height) / 2),
            width,
            height);
    }
}

/// <summary>A monitor's area: full bounds and work area (appbars discounted).</summary>
/// <param name="Bounds">The monitor's full rectangle.</param>
/// <param name="WorkArea">The monitor's work area.</param>
/// <param name="IsPrimary">Whether it is the primary monitor.</param>
/// <param name="DpiScale">DPI scale factor (1.0 = 96 DPI).</param>
public readonly record struct MonitorInfo(PixelRect Bounds, PixelRect WorkArea, bool IsPrimary, double DpiScale);
