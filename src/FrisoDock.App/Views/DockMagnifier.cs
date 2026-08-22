using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
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
///
/// The cursor leaving is not a cut: the effect strength falls over a few frames, and it is the same strength
/// that rises when the cursor comes back. Since what moves is the frame, not the size, coming back mid-
/// back mid-retraction continues from where it stopped instead of jumping to full size.
/// </summary>
public sealed class DockMagnifier : IDisposable
{
    /// <summary>
    /// Frames of the retraction, at the same cadence as the dock's hide slide (16 ms each).
    /// </summary>
    private const int ReleaseFrames = 12;

    private readonly MagnificationLayout _layout = new();
    private readonly DispatcherTimer _timer;

    private ItemsControl? _items;
    private double _iconSize;
    private double _spacing;
    private double _magnification = 1.0;

    /// <summary>
    /// Cursor position in the rest layout. Stored that way, and not in strip coordinates,
    /// because the strip changes width and origin while the effect rises and falls — the rest value
    /// is the only one that does not move on its own.
    /// </summary>
    private double _cursorAtRest;

    /// <summary>
    /// How much the strip is widened right now. Stored because the panel is centred: when it
    /// grows, the left edge moves left by half of that, and the cursor position measured
    /// inside the <see cref="ItemsControl"/> moves with it. Without subtracting, the calculation would feed back.
    /// </summary>
    private double _appliedExtra;

    private int _frame;
    private int _targetFrame;
    private bool _disposed;

    public DockMagnifier()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };

        _timer.Tick += OnTick;
    }

    /// <summary>
    /// Follows the cursor. Outside the icon strip, the retraction starts.
    /// </summary>
    /// <param name="items">List of dock icons.</param>
    /// <param name="cursor">Cursor position in <paramref name="items"/> coordinates.</param>
    /// <param name="iconSize">Icon side, in WPF units.</param>
    /// <param name="spacing">Space before each icon, in WPF units.</param>
    /// <param name="magnification">Maximum scale; 1.0 turns the effect off.</param>
    public void Apply(ItemsControl items, Point cursor, double iconSize, double spacing, double magnification)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (magnification <= 1.0)
        {
            Reset(items);
            return;
        }

        _items = items;
        _iconSize = iconSize;
        _spacing = spacing;
        _magnification = magnification;

        double cursorAtRest = cursor.X - (_appliedExtra / 2);

        if (!_layout.IsWithinStrip(items.Items.Count, iconSize, spacing, cursorAtRest))
        {
            Release();
            return;
        }

        _cursorAtRest = cursorAtRest;

        SetTarget(ReleaseFrames);
        Render();
    }

    /// <summary>
    /// Retracts the magnification over a few frames, from the current size. Called when the cursor
    /// leaves the panel.
    /// </summary>
    public void Release()
    {
        if (_items is null)
        {
            return;
        }

        SetTarget(0);
    }

    /// <summary>
    /// Returns to rest at once, with no animation. It is for when the settings change: there the whole
    /// geometry is rebuilt, and animating a state about to be discarded would only delay the relayout.
    /// </summary>
    public void Reset(ItemsControl items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _timer.Stop();
        _items = items;
        _frame = 0;
        _targetFrame = 0;

        double originX = CalculateOriginX(_iconSize, _spacing);

        for (int index = 0; index < items.Items.Count; index++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(index) is FrameworkElement container)
            {
                Apply(container, new MagnifiedItem(1.0, 0), originX);
            }
        }

        _appliedExtra = 0;
        items.ClearValue(FrameworkElement.WidthProperty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }

    private void SetTarget(int target)
    {
        _targetFrame = target;

        if (_frame != _targetFrame)
        {
            _timer.Start();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        // One frame at a time towards the target: changing direction midway continues from the
        // current frame, which is what makes the returning cursor catch the icon at the size it is.
        _frame += Math.Sign(_targetFrame - _frame);
        Render();

        if (_frame != _targetFrame)
        {
            return;
        }

        _timer.Stop();

        if (_frame == 0 && _items is ItemsControl items)
        {
            items.ClearValue(FrameworkElement.WidthProperty);
        }
    }

    /// <summary>
    /// Draws the current frame. The effect strength enters as a smaller magnification, and not as a
    /// scale applied over the result: this way the neighbours are pushed exactly as far as
    /// the current size demands, and the strip never has overlapping icons along the way.
    /// </summary>
    private void Render()
    {
        if (_items is not ItemsControl items)
        {
            return;
        }

        int count = items.Items.Count;
        double originX = CalculateOriginX(_iconSize, _spacing);
        double strength = Easing.Smoothstep(_frame / (double)ReleaseFrames);
        double magnification = 1.0 + ((_magnification - 1.0) * strength);

        MagnificationLayoutResult result = _layout.Calculate(count, _iconSize, _spacing, _cursorAtRest, magnification);

        for (int index = 0; index < count; index++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            Apply(container, result.Items[index], originX);
        }

        _appliedExtra = result.ExtraWidth;
        items.Width = (count * (_spacing + _iconSize)) + result.ExtraWidth;
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
