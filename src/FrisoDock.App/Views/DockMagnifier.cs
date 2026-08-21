using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Views;

/// <summary>
/// Applies magnification to the icons according to the cursor position. That is all (SRP): the effect's
/// curve belongs to <see cref="MagnificationCurve"/>, a pure function living in Core.
///
/// The growth is done with <see cref="ScaleTransform"/>, which does not participate in layout. It is
/// deliberate: this way the icon overflows out of the panel, as in the macOS dock, instead of
/// pushing the neighbours and making the whole bar jump on every mouse move. The headroom that
/// takes that overflow is reserved in the window by <c>DockLayoutCalculator.CalculateWindowRect</c>.
/// </summary>
public sealed class DockMagnifier
{
    private readonly MagnificationCurve _curve = new();

    /// <summary>
    /// Updates each item's scale.
    /// </summary>
    /// <param name="items">List of dock icons.</param>
    /// <param name="cursor">Cursor position in <paramref name="items"/> coordinates, or null when the mouse left.</param>
    /// <param name="iconSize">Icon side, in WPF units.</param>
    /// <param name="magnification">Maximum scale; 1.0 turns the effect off.</param>
    public void Apply(ItemsControl items, Point? cursor, double iconSize, double magnification)
    {
        ArgumentNullException.ThrowIfNull(items);

        for (int index = 0; index < items.Items.Count; index++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            double scale = CalculateScaleFor(container, items, cursor, iconSize, magnification);
            ApplyScale(container, scale);
        }
    }

    private double CalculateScaleFor(
        FrameworkElement container,
        ItemsControl items,
        Point? cursor,
        double iconSize,
        double magnification)
    {
        if (cursor is not Point position || magnification <= 1.0)
        {
            return 1.0;
        }

        if (container.ActualWidth <= 0)
        {
            return 1.0;
        }

        // The item's centre in the ItemsControl space, which is the same space as the cursor position.
        Point topLeft = container.TranslatePoint(new Point(0, 0), items);
        double center = topLeft.X + (container.ActualWidth / 2);

        return _curve.CalculateScale(position.X - center, iconSize, magnification);
    }

    /// <summary>
    /// The transform is created per item on first application, and not in the XAML, because a
    /// <see cref="ScaleTransform"/> declared in a Setter would be the same instance for every
    /// item — they would all grow together.
    /// </summary>
    private static void ApplyScale(FrameworkElement container, double scale)
    {
        if (container.RenderTransform is not ScaleTransform transform)
        {
            transform = new ScaleTransform();
            container.RenderTransform = transform;

            // Origin at the base and centre: the icon grows upwards, seated on the same line.
            container.RenderTransformOrigin = new Point(0.5, 1.0);
        }

        transform.ScaleX = scale;
        transform.ScaleY = scale;
    }
}
