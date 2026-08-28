using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// Derives the dock palette from an accent color. A pure function, no Win32 (SRP).
/// </summary>
public static class AccentPalettes
{
    /// <summary>
    /// It stands until the real color arrives, and when Windows does not deliver one. It is the default
    /// Windows 11 blue accent in its light variant — the same color that was hand-written in the XAML
    /// before the accent started being read from the system.
    /// </summary>
    public static AccentPalette Default { get; } = From(new AccentColor(0x4C, 0xC2, 0xFF));

    public static AccentPalette From(AccentColor accent)
    {
        return new AccentPalette(accent, ForegroundFor(accent));
    }

    /// <summary>
    /// Color written on top of the accent.
    ///
    /// The choice between light and dark comes from the WCAG contrast against black and against white,
    /// and not from a hand-written threshold: the tipping point is a consequence of the formula itself,
    /// and a hard-coded number would go wrong precisely on the mid-tone accents.
    ///
    /// The result is neither pure black nor pure white: it carries the accent's hue, which is what the
    /// native panel does — over the default blue the text is a bluish near-black, not a flat black.
    /// </summary>
    public static AccentColor ForegroundFor(AccentColor accent)
    {
        double luminance = RelativeLuminance(accent);

        double againstBlack = (luminance + 0.05) / 0.05;
        double againstWhite = 1.05 / (luminance + 0.05);

        if (againstBlack >= againstWhite)
        {
            return Scale(accent, DarkTextTint);
        }

        return TowardWhite(accent, LightTextTint);
    }

    /// <summary>
    /// WCAG relative luminance. It is the same calculation that decides legibility anywhere;
    /// a plain average of the channels would say green and blue weigh the same, and they do not.
    /// </summary>
    public static double RelativeLuminance(AccentColor color)
    {
        return (0.2126 * Linearize(color.R))
             + (0.7152 * Linearize(color.G))
             + (0.0722 * Linearize(color.B));
    }

    /// <summary>How much of the accent survives in the dark text — enough for the hue to show.</summary>
    private const double DarkTextTint = 0.10;

    /// <summary>How far the light text walks towards white.</summary>
    private const double LightTextTint = 0.90;

    private static AccentColor Scale(AccentColor color, double factor)
    {
        return new AccentColor(
            Clamp(color.R * factor),
            Clamp(color.G * factor),
            Clamp(color.B * factor));
    }

    private static AccentColor TowardWhite(AccentColor color, double amount)
    {
        return new AccentColor(
            Clamp(color.R + ((255 - color.R) * amount)),
            Clamp(color.G + ((255 - color.G) * amount)),
            Clamp(color.B + ((255 - color.B) * amount)));
    }

    private static byte Clamp(double value)
    {
        return (byte)Math.Clamp(Math.Round(value), 0, 255);
    }

    private static double Linearize(byte channel)
    {
        double value = channel / 255.0;

        if (value <= 0.03928)
        {
            return value / 12.92;
        }

        return Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
