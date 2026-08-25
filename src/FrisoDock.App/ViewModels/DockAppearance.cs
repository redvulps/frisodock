using System.Windows;
using System.Windows.Controls;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// Translates <see cref="DockMetrics"/> into the types the XAML consumes. That is all (SRP).
///
/// It exists so the layout has a single source. Before, the XAML carried hard-coded sizes
/// and the window calculation used the metrics: the two diverged, the panel came out narrower than
/// the content and the last icon showed up clipped. Now both read from here.
///
/// The edge comes in here because it is what decides every orientation: the direction of the icon
/// stack, the side where the panel meets the window, where the magnified icon overflows and on
/// which face of the button the app indicator sits. The XAML reads the finished result instead of
/// deciding — and since switching edge rebuilds the dock set, the values are stable for the window's life.
/// </summary>
public sealed class DockAppearance
{
    public DockAppearance(DockMetrics metrics, double magnification, DockEdge edge)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        Edge = edge;
        IsVertical = edge.IsVertical();

        IconOverflow = metrics.CalculateMagnificationHeadroom(magnification);
        IconSize = metrics.IconSize;
        IconImageSize = metrics.IconImageSize;
        QuickStatusWidth = metrics.QuickStatusWidth;
        ClockWidth = metrics.ClockWidth;

        ItemSpacing = metrics.ItemSpacing;
        PanelPadding = new Thickness(metrics.Padding);
        PanelBorderThickness = new Thickness(metrics.BorderThickness);

        // The radius follows the panel thickness so the panel keeps its pill shape
        // regardless of the configured icon size.
        PanelCornerRadius = new CornerRadius(metrics.PanelThickness / 4.0);
        ItemCornerRadius = new CornerRadius(metrics.IconSize / 4.0);

        StripOrientation = IsVertical ? Orientation.Vertical : Orientation.Horizontal;
        ItemMargin = IsVertical
            ? new Thickness(0, metrics.ItemSpacing, 0, 0)
            : new Thickness(metrics.ItemSpacing, 0, 0, 0);

        // The separator is a line across the stack: upright on the horizontal dock, lying down on the
        // vertical one. Its length is always a fraction of the icon's side.
        double separatorLength = Math.Round(metrics.IconSize * 0.64);
        SeparatorWidth = IsVertical ? separatorLength : metrics.SeparatorWidth;
        SeparatorHeight = IsVertical ? metrics.SeparatorWidth : separatorLength;
        SeparatorMargin = IsVertical
            ? new Thickness(0, metrics.SeparatorSpacing, 0, metrics.SeparatorSpacing)
            : new Thickness(metrics.SeparatorSpacing, 0, metrics.SeparatorSpacing, 0);

        // Blocks with a length of their own (network/sound/battery and clock): on the vertical dock the
        // length becomes height, and the width goes back to that of an ordinary icon.
        QuickStatusButtonWidth = IsVertical ? metrics.IconSize : metrics.QuickStatusWidth;
        QuickStatusButtonHeight = IsVertical ? metrics.QuickStatusWidth : metrics.IconSize;
        QuickStatusOrientation = IsVertical ? Orientation.Vertical : Orientation.Horizontal;
        QuickStatusGlyphMargin = IsVertical ? new Thickness(0, 4, 0, 0) : new Thickness(5, 0, 0, 0);
        ClockBlockWidth = IsVertical ? metrics.ContentThickness : metrics.ClockWidth;
        ClockBlockHeight = IsVertical ? metrics.ClockWidth : metrics.IconSize;

        // On the vertical dock the date comes on top and the time below; on the horizontal one, the
        // opposite. Each line keeps its own font and color, so what swaps is the grid row, not the text.
        ClockTimeRow = IsVertical ? 1 : 0;
        ClockDateRow = IsVertical ? 0 : 1;

        // The gap always sits between the two lines, that is, on the bottom one.
        ClockTimeMargin = IsVertical ? new Thickness(0, 1, 0, 0) : default;
        ClockDateMargin = IsVertical ? default : new Thickness(0, 1, 0, 0);

        // The panel meets the edge the dock is anchored to; the rest of the window is the transparent
        // headroom that takes the magnified icon and the widening of the bar.
        PanelHorizontalAlignment = edge switch
        {
            DockEdge.Left => HorizontalAlignment.Left,
            DockEdge.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        PanelVerticalAlignment = edge switch
        {
            DockEdge.Top => VerticalAlignment.Top,
            DockEdge.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };

        // The app indicator sits on the button face turned towards the screen edge, as on the
        // native taskbar. The negative margin pushes it out of the image, over the padding.
        IndicatorWidth = IsVertical ? 3 : 6;
        IndicatorHeight = IsVertical ? 6 : 3;
        IndicatorActiveWidth = IsVertical ? 3 : 16;
        IndicatorActiveHeight = IsVertical ? 16 : 3;
        IndicatorHorizontalAlignment = edge switch
        {
            DockEdge.Left => HorizontalAlignment.Left,
            DockEdge.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        IndicatorVerticalAlignment = edge switch
        {
            DockEdge.Top => VerticalAlignment.Top,
            DockEdge.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };
        IndicatorMargin = edge switch
        {
            DockEdge.Bottom => new Thickness(0, 0, 0, -5),
            DockEdge.Top => new Thickness(0, -5, 0, 0),
            DockEdge.Left => new Thickness(-5, 0, 0, 0),
            DockEdge.Right => new Thickness(0, 0, -5, 0),
            _ => new Thickness(0, 0, 0, -5),
        };
    }

    /// <summary>Edge the dock is anchored to.</summary>
    public DockEdge Edge { get; }

    /// <summary>Whether the dock is on a side edge, with the icon stack standing up.</summary>
    public bool IsVertical { get; }

    /// <summary>
    /// How far the magnified icon overflows past its own button. It is what the app label
    /// uses to float outside it instead of sitting underneath.
    /// </summary>
    public double IconOverflow { get; }

    /// <summary>Side of each item's button.</summary>
    public double IconSize { get; }

    /// <summary>Side of the image inside the button.</summary>
    public double IconImageSize { get; }

    /// <summary>Space before each item.</summary>
    public double ItemSpacing { get; }

    /// <summary>The same space, in the shape the XAML consumes — before the item, along the stack axis.</summary>
    public Thickness ItemMargin { get; }

    /// <summary>Direction of the panel's item stack.</summary>
    public Orientation StripOrientation { get; }

    /// <summary>Horizontal side of the window the panel meets.</summary>
    public HorizontalAlignment PanelHorizontalAlignment { get; }

    /// <summary>Vertical side of the window the panel meets.</summary>
    public VerticalAlignment PanelVerticalAlignment { get; }

    /// <summary>Inner margin of the panel.</summary>
    public Thickness PanelPadding { get; }

    /// <summary>Thickness of the panel border.</summary>
    public Thickness PanelBorderThickness { get; }

    /// <summary>Corner radius of the panel.</summary>
    public CornerRadius PanelCornerRadius { get; }

    /// <summary>Corner radius of each item's hover highlight.</summary>
    public CornerRadius ItemCornerRadius { get; }

    /// <summary>Width of the line that separates the blocks.</summary>
    public double SeparatorWidth { get; }

    /// <summary>Height of the separator line.</summary>
    public double SeparatorHeight { get; }

    /// <summary>Space on each side of the separator, along the stack axis.</summary>
    public Thickness SeparatorMargin { get; }

    /// <summary>Length of the network, volume and battery block, along the stack axis.</summary>
    public double QuickStatusWidth { get; }

    /// <summary>Width of the network, volume and battery button.</summary>
    public double QuickStatusButtonWidth { get; }

    /// <summary>Height of the same button.</summary>
    public double QuickStatusButtonHeight { get; }

    /// <summary>Direction of the glyph row inside the button.</summary>
    public Orientation QuickStatusOrientation { get; }

    /// <summary>Space before each glyph, except the first.</summary>
    public Thickness QuickStatusGlyphMargin { get; }

    /// <summary>Length reserved for the clock, along the stack axis.</summary>
    public double ClockWidth { get; }

    /// <summary>Width of the clock block.</summary>
    public double ClockBlockWidth { get; }

    /// <summary>Height of the clock block.</summary>
    public double ClockBlockHeight { get; }

    /// <summary>Clock grid row the time sits in.</summary>
    public int ClockTimeRow { get; }

    /// <summary>Clock grid row the date sits in.</summary>
    public int ClockDateRow { get; }

    /// <summary>Gap above the time, when it is the bottom line.</summary>
    public Thickness ClockTimeMargin { get; }

    /// <summary>Gap above the date, when it is the bottom line.</summary>
    public Thickness ClockDateMargin { get; }

    /// <summary>Width of the running app indicator, at rest.</summary>
    public double IndicatorWidth { get; }

    /// <summary>Height of the indicator at rest.</summary>
    public double IndicatorHeight { get; }

    /// <summary>Width of the indicator when the app is focused.</summary>
    public double IndicatorActiveWidth { get; }

    /// <summary>Height of the indicator when the app is focused.</summary>
    public double IndicatorActiveHeight { get; }

    /// <summary>Horizontal face of the button the indicator sits on.</summary>
    public HorizontalAlignment IndicatorHorizontalAlignment { get; }

    /// <summary>Vertical face of the button the indicator sits on.</summary>
    public VerticalAlignment IndicatorVerticalAlignment { get; }

    /// <summary>Margin that pushes the indicator to the button face turned towards the screen edge.</summary>
    public Thickness IndicatorMargin { get; }
}
