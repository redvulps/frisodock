namespace FrisoDock.Core.Services;

/// <summary>
/// Computes how much each icon grows according to its distance from the cursor. A pure function, no UI,
/// therefore testable.
///
/// It is not only the icon under the cursor that grows: the neighbours grow less, and the falloff is smooth.
/// That is what gives the dash-to-dock "wave" feel — a hard step would look like a plain
/// hover highlight.
/// </summary>
public sealed class MagnificationCurve
{
    /// <summary>
    /// Reach of the effect, in multiples of the icon side. Two and a half icons to each side is what
    /// makes the wave feel continuous without involving the whole dock.
    /// </summary>
    private const double RangeInIcons = 2.5;

    /// <summary>
    /// Scale factor of one icon.
    /// </summary>
    /// <param name="distanceFromCursor">Distance between the icon centre and the cursor, in pixels.</param>
    /// <param name="iconSize">Icon side, in pixels.</param>
    /// <param name="magnification">Maximum scale, applied to the icon exactly under the cursor.</param>
    /// <returns>A factor between 1.0 and <paramref name="magnification"/>.</returns>
    public double CalculateScale(double distanceFromCursor, double iconSize, double magnification)
    {
        if (magnification <= 1.0 || iconSize <= 0)
        {
            return 1.0;
        }

        double range = iconSize * RangeInIcons;
        double distance = Math.Abs(distanceFromCursor);

        if (distance >= range)
        {
            return 1.0;
        }

        return 1.0 + ((magnification - 1.0) * CalculateInfluence(distance / range));
    }

    /// <summary>
    /// Weight of the effect for a distance normalized into [0, 1].
    ///
    /// Half a cosine wave: it is 1 at the cursor, 0 at the end of the reach, and arrives at both ends
    /// with zero slope. It is that slope which avoids the visible "jump" when an icon
    /// enters and leaves the reach.
    /// </summary>
    private static double CalculateInfluence(double normalizedDistance)
    {
        return (Math.Cos(Math.PI * normalizedDistance) + 1.0) / 2.0;
    }
}
