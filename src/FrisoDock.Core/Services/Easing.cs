namespace FrisoDock.Core.Services;

/// <summary>
/// Easing for the ends of an animation. A pure function, no UI.
///
/// It lives outside whoever animates because the dock has more than one animation — the hide slide and
/// the magnification retraction — and both need the same curve. Each with its own would give
/// movements of different character in the same dock.
/// </summary>
public static class Easing
{
    /// <summary>
    /// The classic S curve (<c>smoothstep</c>): it reaches both ends with zero slope.
    ///
    /// Linear, the movement starts and stops abruptly, and the eye reads that as a jump instead of
    /// a displacement.
    /// </summary>
    /// <param name="progress">Progress, from 0 to 1. Values outside the range are clamped into it.</param>
    public static double Smoothstep(double progress)
    {
        double clamped = Math.Clamp(progress, 0.0, 1.0);

        return clamped * clamped * (3.0 - (2.0 * clamped));
    }
}
