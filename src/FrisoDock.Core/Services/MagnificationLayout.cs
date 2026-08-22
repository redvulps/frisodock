namespace FrisoDock.Core.Services;

/// <summary>How an icon ends up under the magnification effect.</summary>
/// <param name="Scale">Scale factor, between 1.0 and the maximum magnification.</param>
/// <param name="OffsetX">
/// Horizontal offset from the rest position, measured inside the strip itself. The
/// strip is rebuilt from left to right, so this value is always positive; what
/// centres the effect is the whole strip, which grows to both sides because it is centred
/// in the panel. In screen coordinates the real offset is <c>OffsetX - ExtraWidth / 2</c>.
/// </param>
public readonly record struct MagnifiedItem(double Scale, double OffsetX);

/// <summary>Scales, offsets and how much the icon strip grew in total.</summary>
public sealed record MagnificationLayoutResult(IReadOnlyList<MagnifiedItem> Items, double ExtraWidth);

/// <summary>
/// Places the magnified icons side by side. A pure function, no UI, therefore testable.
///
/// Scaling each icon alone is not enough: an icon that doubles in size invades its neighbours' space,
/// and the result is a pile of overlapping icons instead of a wave. Here the neighbours are
/// pushed sideways exactly as far as the growth demands — it is what the macOS dock
/// does, and it is why its bar gets wider while the cursor is over it.
///
/// The spacing between icons is not scaled on purpose: scaled, the gap between two large icons
/// would be wider than a small icon itself and the strip would look torn in half.
/// </summary>
public sealed class MagnificationLayout
{
    /// <summary>Shared instance: the class has no state.</summary>
    public static MagnificationLayout Default { get; } = new();

    private readonly MagnificationCurve _curve = new();

    /// <summary>
    /// Whether the cursor is over the icon strip, the only place where the effect applies.
    ///
    /// The curve reaches 2.5 icons to each side, so without this limit a cursor resting
    /// past the separator — on the tray, on the clock — would still magnify the last icons. The strip
    /// runs from zero to the end of the last icon, and includes the spacing before each of them.
    /// </summary>
    /// <param name="cursorX">Cursor position along the strip axis, in the rest layout.</param>
    public bool IsWithinStrip(int itemCount, double iconSize, double spacing, double cursorX)
    {
        if (itemCount <= 0 || iconSize <= 0)
        {
            return false;
        }

        return cursorX >= 0 && cursorX <= itemCount * (spacing + iconSize);
    }

    /// <summary>
    /// Computes the whole strip for one cursor position.
    /// </summary>
    /// <param name="itemCount">Number of icons.</param>
    /// <param name="iconSize">Icon side at rest.</param>
    /// <param name="spacing">Space before each icon.</param>
    /// <param name="cursorX">
    /// Cursor position along the strip axis, measured in the <b>rest</b> layout. Using the position in
    /// the already deformed layout would feed the calculation back: the icon would move away from the
    /// cursor because it grew, shrink because of that, and come back — a permanent oscillation.
    /// </param>
    /// <param name="magnification">Maximum scale; 1.0 turns the effect off.</param>
    public MagnificationLayoutResult Calculate(
        int itemCount,
        double iconSize,
        double spacing,
        double cursorX,
        double magnification)
    {
        if (itemCount <= 0)
        {
            return new MagnificationLayoutResult(Array.Empty<MagnifiedItem>(), 0);
        }

        var items = new MagnifiedItem[itemCount];

        if (iconSize <= 0 || magnification <= 1.0)
        {
            Array.Fill(items, new MagnifiedItem(1.0, 0));
            return new MagnificationLayoutResult(items, 0);
        }

        double slot = spacing + iconSize;
        double cursor = 0;

        for (int index = 0; index < itemCount; index++)
        {
            double restCenter = spacing + (index * slot) + (iconSize / 2);
            double scale = _curve.CalculateScale(cursorX - restCenter, iconSize, magnification);
            double width = iconSize * scale;

            // The strip is rebuilt from left to right with the already magnified widths, and each
            // icon is moved from its rest position to the new one. This way none invades its neighbour.
            double center = cursor + spacing + (width / 2);
            cursor += spacing + width;

            items[index] = new MagnifiedItem(scale, center - restCenter);
        }

        return new MagnificationLayoutResult(items, cursor - (itemCount * slot));
    }

    /// <summary>
    /// The largest extra width the strip can reach. It is what the window has to reserve as side
    /// headroom so the bar can grow without being clipped.
    ///
    /// It only runs when the dock is repositioned, so sweeping the strip is cheap.
    /// </summary>
    public double CalculateMaxExtraWidth(int itemCount, double iconSize, double spacing, double magnification)
    {
        double slot = spacing + iconSize;

        if (itemCount <= 0 || slot <= 0)
        {
            return 0;
        }

        // By sampling: the peak does not fall on an icon, it falls between two — with the cursor in the
        // gap, both neighbours grow almost to the maximum at once, and the sum exceeds the centred
        // position. Testing only the centres would underestimate the headroom, and the panel would be clipped.
        double step = slot / 128;
        double max = 0;

        for (double cursorX = 0; cursorX <= itemCount * slot; cursorX += step)
        {
            double extra = Calculate(itemCount, iconSize, spacing, cursorX, magnification).ExtraWidth;

            max = Math.Max(max, extra);
        }

        return max;
    }
}
