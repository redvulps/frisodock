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
/// <param name="ContentPadding">
/// Horizontal gap between an element's content and the edge of the highlight around it. It is the
/// same for all of them: app icon, network/sound/battery group and clock.
/// </param>
/// <param name="QuickStatusContentWidth">Width of the network, volume and battery glyphs, measured on screen.</param>
/// <param name="ClockContentWidth">Width of the clock's widest line — here, the date.</param>
/// <param name="ClockContentWidthWithSeconds">The same measure when the time carries the seconds.</param>
/// <param name="VerticalClockEdgeInset">
/// Gap between the clock text and the panel edge, on a vertical dock. Smaller than the panel's
/// inner margin on purpose: it is what lets the clock invade that margin instead of
/// forcing the panel to thicken.
/// </param>
/// <param name="PanelContentThickness">
/// Usable panel thickness, inside the margin, when it is not an icon's. Null is the
/// common case; what fills it in is <see cref="Models.DockSettings.EffectiveMetrics"/>, so the
/// clock fits horizontally on a vertical dock.
///
/// Null instead of repeating 44: a number written here would drift from <paramref name="IconSize"/>
/// the first time the metrics were built with another icon size.
/// </param>
/// <param name="HiddenSliver">Sliver of the panel that stays on screen with the dock hidden.</param>
public sealed record DockMetrics(
    int IconSize = 44,
    int ItemSpacing = 8,
    int Padding = 8,
    int EdgeMargin = 6,
    int BorderThickness = 1,
    int SeparatorWidth = 1,
    int SeparatorSpacing = 6,
    int ContentPadding = 6,
    int QuickStatusContentWidth = 52,
    int ClockContentWidth = 56,
    int ClockContentWidthWithSeconds = 70,
    int VerticalClockEdgeInset = 2,
    int? PanelContentThickness = null,
    int HiddenSliver = 2)
{
    public static DockMetrics Default { get; } = new();

    /// <summary>
    /// Side of the image inside the button, leaving the hover highlight's padding.
    ///
    /// It comes from the same padding as the other elements, and not from a ratio of its own: it was
    /// <c>IconSize * 0.73</c>, which gives exactly this same value — the ratio was hiding the padding.
    /// </summary>
    public int IconImageSize => IconSize - (ContentPadding * 2);

    /// <summary>Width of the network, volume and battery group, with the padding on both sides.</summary>
    public int QuickStatusWidth => QuickStatusContentWidth + (ContentPadding * 2);

    /// <summary>Width reserved for the clock, with the padding on both sides.</summary>
    public int ClockWidth => ClockContentWidth + (ContentPadding * 2);

    /// <summary>The same width when the clock shows the seconds.</summary>
    public int ClockWidthWithSeconds => ClockContentWidthWithSeconds + (ContentPadding * 2);

    /// <summary>Separator length counting the space on both sides.</summary>
    public int SeparatorBlockLength => SeparatorWidth + (SeparatorSpacing * 2);

    /// <summary>
    /// How far a vertical dock's clock advances into the panel margin, on each side.
    ///
    /// The margin exists to separate the icons from the edge, and an icon has width to spare. The
    /// clock does not: horizontally it is wider than an icon, and respecting the same margin would
    /// force the whole panel to thicken because of a single line of text.
    /// </summary>
    public int VerticalClockBleed => Math.Max(Padding - VerticalClockEdgeInset, 0);

    /// <summary>Clock width on a vertical dock, already counting the advance over the margin.</summary>
    public int VerticalClockWidth => ContentThickness + (VerticalClockBleed * 2);

    /// <summary>Usable panel thickness: an icon's, unless explicitly adjusted.</summary>
    public int ContentThickness => PanelContentThickness ?? IconSize;

    /// <summary>Thickness of the visible panel (height, when anchored at the bottom).</summary>
    public int PanelThickness => ContentThickness + (Padding * 2) + (BorderThickness * 2);

    /// <summary>Thickness reserved on screen: the panel plus the gap to the edge.</summary>
    public int ReservedThickness => PanelThickness + EdgeMargin;

    /// <summary>
    /// How far the dock has to move off screen to be hidden.
    ///
    /// It does not disappear entirely: the sliver that brings the dock back when the cursor touches the edge remains.
    /// Without it, a hidden dock could not be called back without a global mouse hook.
    /// </summary>
    public int HiddenDistance => Math.Max(PanelThickness + EdgeMargin - HiddenSliver, 0);

    /// <summary>
    /// Headroom the window needs beyond the panel so the magnified icon is not clipped.
    ///
    /// The icon grows from its base, so all the headroom sits outside the panel — it is
    /// transparent window area, which does not count towards the screen space reservation.
    /// </summary>
    public int CalculateMagnificationHeadroom(double magnification)
    {
        if (magnification <= 1.0)
        {
            return 0;
        }

        return (int)Math.Ceiling(IconSize * (magnification - 1.0));
    }

    /// <summary>
    /// Side headroom the window needs beyond the panel.
    ///
    /// The magnified icons push the neighbours sideways, so the app strip — and with it
    /// the whole panel — gets wider while the cursor is over the dock. The window is
    /// sized for the worst case and does not change size with the mouse: what grows and shrinks is
    /// the panel, inside it. What is left is transparent area, and does not count in the screen reservation.
    /// </summary>
    public int CalculateMagnificationWidthHeadroom(int itemCount, double magnification)
    {
        if (magnification <= 1.0)
        {
            return 0;
        }

        double extra = MagnificationLayout.Default.CalculateMaxExtraWidth(
            Math.Max(itemCount, 0),
            IconSize,
            ItemSpacing,
            magnification);

        return (int)Math.Ceiling(extra);
    }

    /// <summary>
    /// Panel length for a given number of apps.
    ///
    /// The dock composition is fixed and this calculation is its single source:
    /// <c>[Start] [separator] [apps…] [separator] [tray] [network/sound/battery] [clock]</c>, each app preceded by its
    /// spacing. The XAML reads the same metrics from here — if the two sides diverge, the panel
    /// comes out narrower than the content and the last element shows up clipped.
    /// </summary>
    public int CalculatePanelLength(int itemCount)
    {
        int items = Math.Max(itemCount, 0);

        int leading = IconSize + SeparatorBlockLength;
        int apps = items * (ItemSpacing + IconSize);
        int trailing = SeparatorBlockLength
            + IconSize
            + ItemSpacing + QuickStatusWidth
            + ItemSpacing + ClockWidth;

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
    /// Rectangle of the window that hosts the panel.
    ///
    /// It is larger than the panel on both axes: a transparent band is left above, for the magnified
    /// icon to overflow over the bar, and on the sides, so the panel can widen when the
    /// icons push the neighbours. Without that headroom the effect would be clipped at the window edge.
    /// </summary>
    public PixelRect CalculateWindowRect(
        MonitorInfo monitor,
        DockEdge edge,
        int itemCount,
        DockMetrics metrics,
        double magnification)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        PixelRect panel = CalculatePanelRect(monitor, edge, itemCount, metrics);
        int headroom = Scale(metrics.CalculateMagnificationHeadroom(magnification), monitor.DpiScale);

        // The side headroom is split between the two ends because the panel is centred: it
        // grows to both sides from the centre, which stays put.
        int widthHeadroom = metrics.CalculateMagnificationWidthHeadroom(itemCount, magnification);
        int side = Scale((widthHeadroom + 1) / 2, monitor.DpiScale);

        if (headroom == 0 && side == 0)
        {
            return panel;
        }

        return edge switch
        {
            DockEdge.Bottom => new PixelRect(panel.Left - side, panel.Top - headroom, panel.Right + side, panel.Bottom),
            DockEdge.Top => new PixelRect(panel.Left - side, panel.Top, panel.Right + side, panel.Bottom + headroom),
            DockEdge.Left => new PixelRect(panel.Left, panel.Top - side, panel.Right + headroom, panel.Bottom + side),
            DockEdge.Right => new PixelRect(panel.Left - headroom, panel.Top - side, panel.Right, panel.Bottom + side),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    /// <summary>
    /// Offsets the window according to whether the dock is in view, hidden, or halfway there.
    /// </summary>
    /// <param name="revealProgress">1 is fully in view; 0, hidden at the edge.</param>
    public PixelRect ApplyReveal(
        PixelRect window,
        DockEdge edge,
        DockMetrics metrics,
        double revealProgress,
        double dpiScale)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        int distance = Scale(metrics.HiddenDistance, dpiScale);
        int offset = (int)Math.Round(distance * (1.0 - Math.Clamp(revealProgress, 0.0, 1.0)));

        if (offset == 0)
        {
            return window;
        }

        // The dock leaves through the edge it is anchored to, never sideways.
        return edge switch
        {
            DockEdge.Bottom => Translate(window, 0, offset),
            DockEdge.Top => Translate(window, 0, -offset),
            DockEdge.Left => Translate(window, -offset, 0),
            DockEdge.Right => Translate(window, offset, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    /// <summary>
    /// Band that brings the hidden dock back: the sliver left at the screen edge, in the panel's
    /// width. Narrow on purpose — a tall zone would make the dock jump onto the screen whenever
    /// the cursor passed near the edge.
    /// </summary>
    public PixelRect CalculateRevealZone(PixelRect panel, MonitorInfo monitor, DockEdge edge, DockMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        int sliver = Scale(metrics.HiddenSliver, monitor.DpiScale);
        PixelRect bounds = monitor.Bounds;

        return edge switch
        {
            DockEdge.Bottom => new PixelRect(panel.Left, bounds.Bottom - sliver, panel.Right, bounds.Bottom),
            DockEdge.Top => new PixelRect(panel.Left, bounds.Top, panel.Right, bounds.Top + sliver),
            DockEdge.Left => new PixelRect(bounds.Left, panel.Top, bounds.Left + sliver, panel.Bottom),
            DockEdge.Right => new PixelRect(bounds.Right - sliver, panel.Top, bounds.Right, panel.Bottom),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    /// <summary>
    /// Area that keeps the already revealed dock on screen: the whole window, stretched to the edge.
    ///
    /// It is larger than the revealing zone, and that is what avoids the oscillation. Were they equal,
    /// the dock would rise when the edge was touched, slide out from under the cursor — which would sit
    /// in the gap between panel and edge — and go back down, to be touched again, endlessly.
    /// </summary>
    public PixelRect CalculateHoverZone(PixelRect window, MonitorInfo monitor, DockEdge edge)
    {
        PixelRect bounds = monitor.Bounds;

        return edge switch
        {
            DockEdge.Bottom => new PixelRect(window.Left, window.Top, window.Right, bounds.Bottom),
            DockEdge.Top => new PixelRect(window.Left, bounds.Top, window.Right, window.Bottom),
            DockEdge.Left => new PixelRect(bounds.Left, window.Top, window.Right, window.Bottom),
            DockEdge.Right => new PixelRect(window.Left, window.Top, bounds.Right, window.Bottom),
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    private static PixelRect Translate(PixelRect rect, int deltaX, int deltaY)
    {
        return new PixelRect(
            rect.Left + deltaX,
            rect.Top + deltaY,
            rect.Right + deltaX,
            rect.Bottom + deltaY);
    }

    /// <summary>
    /// Band the appbar should reserve: it takes the whole edge, with the dock's thickness.
    /// The visible panel sits centred inside it.
    /// </summary>
    /// <param name="nativeBand">
    /// Band the native taskbar already reserves, or <c>null</c> if it reserves nothing. The shell
    /// stacks the appbars of a single edge, so asking for the full thickness on top of the taskbar's
    /// band would reserve both — and the taskbar's sits under the dock, invisible. By discounting it,
    /// the total goes back to exactly the dock's thickness.
    /// </param>
    public PixelRect CalculateReservationRect(
        MonitorInfo monitor,
        DockEdge edge,
        DockMetrics metrics,
        PixelRect? nativeBand = null)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        int thickness = Math.Max(
            0,
            Scale(metrics.ReservedThickness, monitor.DpiScale) - MeasureNativeBand(monitor, edge, nativeBand));

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

    /// <summary>
    /// Thickness the native band already takes from this edge of this monitor.
    ///
    /// Only the band on the same edge of the same monitor counts: a taskbar on the side, or on the
    /// next screen, does not compete for space with the dock here. Touching the edge is not enough as a
    /// criterion — a taskbar on the left runs from top to bottom, and therefore also touches the
    /// bottom edge. What tells them apart is the shape: a horizontal band belongs to a horizontal
    /// edge, an upright band to a vertical one.
    /// </summary>
    private static int MeasureNativeBand(MonitorInfo monitor, DockEdge edge, PixelRect? nativeBand)
    {
        if (nativeBand is not PixelRect band)
        {
            return 0;
        }

        PixelRect bounds = monitor.Bounds;

        if (band.Right <= bounds.Left || band.Left >= bounds.Right || band.Bottom <= bounds.Top || band.Top >= bounds.Bottom)
        {
            return 0;
        }

        bool horizontal = band.Width >= band.Height;

        return edge switch
        {
            DockEdge.Bottom => horizontal && band.Bottom >= bounds.Bottom ? band.Height : 0,
            DockEdge.Top => horizontal && band.Top <= bounds.Top ? band.Height : 0,
            DockEdge.Left => !horizontal && band.Left <= bounds.Left ? band.Width : 0,
            DockEdge.Right => !horizontal && band.Right >= bounds.Right ? band.Width : 0,
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    private static int Scale(int logicalPixels, double dpiScale)
    {
        double factor = dpiScale <= 0 ? 1.0 : dpiScale;
        return (int)Math.Round(logicalPixels * factor, MidpointRounding.AwayFromZero);
    }
}
