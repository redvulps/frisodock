using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// The dock metrics, in logical pixels (96 DPI). Scaled at placement time.
///
/// It is a reference record, and not a record struct, on purpose: in a struct,
/// <c>new DockMetrics()</c> and <c>default</c> ignore the primary constructor and zero every
/// field, producing a dock of size zero. As a class, any <c>new()</c> goes through the
/// primary constructor and gets the default values.
/// </summary>
/// <param name="IconSize">Side of each item's button, the Start one included.</param>
/// <param name="ItemSpacing">Space before each item.</param>
/// <param name="Padding">Inner margin of the panel.</param>
/// <param name="EdgeMargin">Distance between the panel and the screen edge.</param>
/// <param name="BorderThickness">Thickness of the panel border.</param>
/// <param name="SeparatorWidth">Thickness of the line that separates the blocks.</param>
/// <param name="SeparatorSpacing">Space on each side of the separator.</param>
/// <param name="ClockWidth">Width reserved for the clock.</param>
public sealed record DockMetrics(
    int IconSize = 44,
    int ItemSpacing = 8,
    int Padding = 8,
    int EdgeMargin = 6,
    int BorderThickness = 1,
    int SeparatorWidth = 1,
    int SeparatorSpacing = 6,
    int ClockWidth = 62)
{
    public static DockMetrics Default { get; } = new();

    /// <summary>Side of the image inside the button, leaving room for the hover highlight.</summary>
    public int IconImageSize => (int)Math.Round(IconSize * 0.73);

    /// <summary>Separator length counting the space on both sides.</summary>
    public int SeparatorBlockLength => SeparatorWidth + (SeparatorSpacing * 2);

    /// <summary>Thickness of the visible panel (height, when anchored at the bottom).</summary>
    public int PanelThickness => IconSize + (Padding * 2) + (BorderThickness * 2);

    /// <summary>Thickness reserved on screen: the panel plus the gap to the edge.</summary>
    public int ReservedThickness => PanelThickness + EdgeMargin;

    /// <summary>
    /// Panel length for a given number of apps.
    ///
    /// The dock composition is fixed and this calculation is its single source:
    /// <c>[Start] [separator] [apps…] [separator] [tray] [clock]</c>, each app preceded by its
    /// spacing. The XAML reads the same metrics from here — if the two sides diverge, the panel
    /// comes out narrower than the content and the last element shows up clipped.
    /// </summary>
    public int CalculatePanelLength(int itemCount)
    {
        int items = Math.Max(itemCount, 0);

        int leading = IconSize + SeparatorBlockLength;
        int apps = items * (ItemSpacing + IconSize);
        int trailing = SeparatorBlockLength + IconSize + ItemSpacing + ClockWidth;

        return leading + apps + trailing + (Padding * 2) + (BorderThickness * 2);
    }
}

/// <summary>
/// Single responsibility: computing where the dock should sit. Pure geometry, no Win32 and no UI,
/// therefore unit testable.
/// </summary>
public sealed class DockLayoutCalculator
{
    /// <summary>
    /// Rectangle of the dock's visible panel, centred on the chosen edge.
    /// </summary>
    /// <param name="itemCount">
    /// Number of apps in the dock. The Start button and the separator are a fixed part of the dock and
    /// already enter the calculation — they must not be added here.
    /// </param>
    public PixelRect CalculatePanelRect(MonitorInfo monitor, DockEdge edge, int itemCount, DockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        // The metrics are logical and the length is scaled a single time, at the end. Scaling each
        // part separately would accumulate rounding error and produce a panel of a
        // different size from what WPF draws — which also scales the whole layout at once.
        int panelLength = Scale(metrics.CalculatePanelLength(itemCount), monitor.DpiScale);
        int panelThickness = Scale(metrics.PanelThickness, monitor.DpiScale);
        int edgeMargin = Scale(metrics.EdgeMargin, monitor.DpiScale);

        PixelRect bounds = monitor.Bounds;

        if (edge is DockEdge.Bottom or DockEdge.Top)
        {
            int maxLength = Math.Max(bounds.Width - (edgeMargin * 2), panelThickness);
            panelLength = Math.Min(panelLength, maxLength);

            int left = bounds.Left + ((bounds.Width - panelLength) / 2);
            int top = edge == DockEdge.Bottom
                ? bounds.Bottom - panelThickness - edgeMargin
                : bounds.Top + edgeMargin;

            return PixelRect.FromSize(left, top, panelLength, panelThickness);
        }

        int maxVertical = Math.Max(bounds.Height - (edgeMargin * 2), panelThickness);
        panelLength = Math.Min(panelLength, maxVertical);

        int verticalTop = bounds.Top + ((bounds.Height - panelLength) / 2);
        int verticalLeft = edge == DockEdge.Right
            ? bounds.Right - panelThickness - edgeMargin
            : bounds.Left + edgeMargin;

        return PixelRect.FromSize(verticalLeft, verticalTop, panelThickness, panelLength);
    }

    /// <summary>
    /// Band the appbar should reserve: it takes the whole edge, with the dock's thickness.
    /// The visible panel sits centred inside it.
    /// </summary>
    public PixelRect CalculateReservationRect(MonitorInfo monitor, DockEdge edge, DockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        int thickness = Scale(metrics.ReservedThickness, monitor.DpiScale);
        PixelRect bounds = monitor.Bounds;

        return edge switch
        {
            DockEdge.Bottom => new PixelRect(bounds.Left, bounds.Bottom - thickness, bounds.Right, bounds.Bottom),
            DockEdge.Top => new PixelRect(bounds.Left, bounds.Top, bounds.Right, bounds.Top + thickness),
            DockEdge.Left => new PixelRect(bounds.Left, bounds.Top, bounds.Left + thickness, bounds.Bottom),
            DockEdge.Right => new PixelRect(bounds.Right - thickness, bounds.Top, bounds.Right, bounds.Bottom),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    private static int Scale(int logicalPixels, double dpiScale)
    {
        double factor = dpiScale <= 0 ? 1.0 : dpiScale;
        return (int)Math.Round(logicalPixels * factor, MidpointRounding.AwayFromZero);
    }
}
