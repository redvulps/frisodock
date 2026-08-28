namespace FrisoDock.Core.Models;

/// <summary>
/// Opaque color in 8-bit components.
///
/// Core does not know WPF, so it cannot use <c>System.Windows.Media.Color</c>; and the accent
/// color is never translucent, so an alpha channel would be a field born constant.
/// </summary>
public readonly record struct AccentColor(byte R, byte G, byte B);

/// <summary>
/// The two colors the Windows accent yields to the dock: the fill and what is
/// written on top of it.
///
/// There are two because one alone is not enough: the quick settings panel's active tile paints the
/// background with the accent, and the glyph on top needs a color that contrasts with it — which
/// swings from black to white depending on the chosen accent.
/// </summary>
public sealed record AccentPalette(AccentColor Accent, AccentColor OnAccent);
