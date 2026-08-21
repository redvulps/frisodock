using System.Windows;
using FrisoDock.Core.Services;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// Translates <see cref="DockMetrics"/> into the types the XAML consumes. That is all (SRP).
///
/// It exists so the layout has a single source. Before, the XAML carried hard-coded sizes
/// and the window calculation used the metrics: the two diverged, the panel came out narrower than
/// the content and the last icon showed up clipped. Now both read from here.
/// </summary>
public sealed class DockAppearance
{
    public DockAppearance(DockMetrics metrics, double magnification)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        IconOverflow = metrics.CalculateMagnificationHeadroom(magnification);
        IconSize = metrics.IconSize;
        IconImageSize = metrics.IconImageSize;
        SeparatorWidth = metrics.SeparatorWidth;
        SeparatorHeight = Math.Round(metrics.IconSize * 0.64);
        ClockWidth = metrics.ClockWidth;

        ItemSpacing = metrics.ItemSpacing;
        ItemMargin = new Thickness(metrics.ItemSpacing, 0, 0, 0);
        PanelPadding = new Thickness(metrics.Padding);
        PanelBorderThickness = new Thickness(metrics.BorderThickness);
        SeparatorMargin = new Thickness(metrics.SeparatorSpacing, 0, metrics.SeparatorSpacing, 0);

        // The radius follows the panel thickness so the panel keeps its pill shape
        // regardless of the configured icon size.
        PanelCornerRadius = new CornerRadius(metrics.PanelThickness / 4.0);
        ItemCornerRadius = new CornerRadius(metrics.IconSize / 4.0);
    }

    /// <summary>
    /// How far the magnified icon rises above its own button. It is what the app label uses to
    /// float above it instead of sitting underneath.
    /// </summary>
    public double IconOverflow { get; }

    /// <summary>Side of each item's button.</summary>
    public double IconSize { get; }

    /// <summary>Side of the image inside the button.</summary>
    public double IconImageSize { get; }

    /// <summary>Space before each item.</summary>
    public double ItemSpacing { get; }

    /// <summary>The same space, in the shape the XAML consumes.</summary>
    public Thickness ItemMargin { get; }

    /// <summary>Inner margin of the panel.</summary>
    public Thickness PanelPadding { get; }

    /// <summary>Thickness of the panel border.</summary>
    public Thickness PanelBorderThickness { get; }

    /// <summary>Corner radius of the panel.</summary>
    public CornerRadius PanelCornerRadius { get; }

    /// <summary>Corner radius of each item's hover highlight.</summary>
    public CornerRadius ItemCornerRadius { get; }

    /// <summary>Thickness of the line that separates Start from the apps.</summary>
    public double SeparatorWidth { get; }

    /// <summary>Height of the separator line.</summary>
    public double SeparatorHeight { get; }

    /// <summary>Space on each side of the separator.</summary>
    public Thickness SeparatorMargin { get; }

    /// <summary>Width reserved for the clock.</summary>
    public double ClockWidth { get; }
}
