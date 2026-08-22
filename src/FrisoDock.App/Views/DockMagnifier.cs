using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Views;

/// <summary>
/// Applies magnification to the icons according to the cursor position. That is all (SRP): the
/// effect's geometry belongs to <see cref="MagnificationLayout"/>, a pure function living in Core.
///
/// The growth is done by transform, not by layout: the icon overflows upwards, out of
/// the panel, as in the macOS dock. The horizontal offset is a transform too, but the whole strip
/// has to take more space — hence the explicit width on the <see cref="ItemsControl"/>,
/// which is what makes the panel widen with it and keeps the neighbours from being invaded.
/// </summary>
public sealed class DockMagnifier
{
    private readonly MagnificationLayout _layout = new();

    /// <summary>
    /// How much the strip is widened right now. Stored because the panel is centred: when it
    /// grows, the left edge moves left by half of that, and the cursor position measured
    /// inside the <see cref="ItemsControl"/> moves with it. Without subtracting, the calculation would feed back.
    /// </summary>
    private double _appliedExtra;

    /// <summary>
    /// Updates each item's scale and position.
    /// </summary>
    /// <param name="items">List of dock icons.</param>
    /// <param name="cursor">Cursor position in <paramref name="items"/> coordinates, or null when the mouse left.</param>
    /// <param name="iconSize">Icon side, in WPF units.</param>
    /// <param name="spacing">Space before each icon, in WPF units.</param>
    /// <param name="magnification">Maximum scale; 1.0 turns the effect off.</param>
    public void Apply(ItemsControl items, Point? cursor, double iconSize, double spacing, double magnification)
    {
        ArgumentNullException.ThrowIfNull(items);

        int count = items.Items.Count;

        double originX = CalculateOriginX(iconSize, spacing);

        if (cursor is not Point position || magnification <= 1.0)
        {
            Reset(items, count, originX);
            return;
        }

        double cursorAtRest = position.X - (_appliedExtra / 2);

        // What follows the cursor is the whole panel, to cover the gaps between the icons — but
        // the effect belongs to the strip only. Past the separator, the cursor is on the tray or the clock,
        // and magnifying from there magnifies an icon the mouse is not even touching.
        if (!_layout.IsWithinStrip(count, iconSize, spacing, cursorAtRest))
        {
            Reset(items, count, originX);
            return;
        }

        MagnificationLayoutResult result = _layout.Calculate(count, iconSize, spacing, cursorAtRest, magnification);

        for (int index = 0; index < count; index++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            Apply(container, result.Items[index], originX);
        }

        _appliedExtra = result.ExtraWidth;
        items.Width = (count * (spacing + iconSize)) + result.ExtraWidth;
    }

    private void Reset(ItemsControl items, int count, double originX)
    {
        for (int index = 0; index < count; index++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
            {
                Apply(container, new MagnifiedItem(1.0, 0), originX);
            }
        }

        _appliedExtra = 0;
        items.ClearValue(FrameworkElement.WidthProperty);
    }

    /// <summary>
    /// The transforms are created per item on first application, and not in the XAML, because a
    /// transform declared in a Setter would be the same instance for every item — they would all
    /// grow together.
    /// </summary>
    private static void Apply(FrameworkElement container, MagnifiedItem item, double originX)
    {
        // The origin sits at the icon's base and centre: this way it grows upwards, seated on the
        // same line, and without sliding sideways. It is not the container centre, which is wider
        // than the icon because of the spacing — scaling around it would displace the icon.
        container.RenderTransformOrigin = new Point(originX, 1.0);

        if (container.RenderTransform is not TransformGroup group)
        {
            group = new TransformGroup();

            // Scale first, offset afterwards: this way the offset is in layout units,
            // without being multiplied by the icon's own scale.
            group.Children.Add(new ScaleTransform());
            group.Children.Add(new TranslateTransform());

            container.RenderTransform = group;
        }

        ((ScaleTransform)group.Children[0]).ScaleX = item.Scale;
        ((ScaleTransform)group.Children[0]).ScaleY = item.Scale;
        ((TranslateTransform)group.Children[1]).X = item.OffsetX;

        // Without this the larger icon sits behind the right-hand neighbour, which is drawn later.
        Panel.SetZIndex(container, (int)Math.Round(item.Scale * 1000));
    }

    /// <summary>
    /// The icon's centre within the container, as a fraction of its width. The container includes the
    /// spacing that comes before the icon, so the two centres do not coincide.
    /// </summary>
    private static double CalculateOriginX(double iconSize, double spacing)
    {
        double slot = spacing + iconSize;

        if (slot <= 0)
        {
            return 0.5;
        }

        return (spacing + (iconSize / 2)) / slot;
    }
}
