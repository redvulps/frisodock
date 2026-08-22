using System.Diagnostics;
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
/// The cursor leaving is not a cut: the effect strength falls over 125 ms, and it is the same
/// strength that rises when the cursor comes back. Since what moves is strength, not size, coming
/// back mid-retraction continues from where it stopped instead of jumping to full size.
/// </summary>
public sealed class DockMagnifier : IDisposable
{
    /// <summary>
    /// Duration of the whole retraction, from full to rest.
    ///
    /// It is time, not a frame count: counting frames, a late frame stretches the
    /// animation, and the duration would depend on whatever else was happening on screen.
    /// </summary>
    private const double ReleaseMilliseconds = 125;

    private readonly MagnificationLayout _layout = new();
    private readonly DispatcherTimer _timer;

    /// <summary>Time since the previous frame. It is what gives the fixed duration.</summary>
    private readonly Stopwatch _clock = new();

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

    /// <summary>Effect strength right now, from 0 (rest) to 1 (full), before easing.</summary>
    private double _strength;

    private double _target;
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

        SetTarget(1.0);
        Render();
    }

    /// <summary>
    /// Retracts the magnification from the current size. Called when the cursor leaves the panel.
    /// </summary>
    public void Release()
    {
        if (_items is null)
        {
            return;
        }

        SetTarget(0.0);
    }

    /// <summary>
    /// Returns to rest at once, with no animation. It is for when the settings change: there the whole
    /// geometry is rebuilt, and animating a state about to be discarded would only delay the relayout.
    /// </summary>
    public void Reset(ItemsControl items)
    {
        ArgumentNullException.ThrowIfNull(items);

        _timer.Stop();
        _clock.Reset();
        _items = items;
        _strength = 0;
        _target = 0;

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

    private void SetTarget(double target)
    {
        _target = target;

        if (_strength == _target)
        {
            return;
        }

        // It restarts the count so the first step does not include the idle time since the end of the
        // previous animation, which would give a jump in place of the first frame.
        _clock.Restart();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        double elapsed = _clock.Elapsed.TotalMilliseconds;
        _clock.Restart();

        // The step comes from time that really passed, and not from a fixed per-frame value: this way the
        // total duration is the same with the dock full of icons or with three, and a dropped frame
        // advances the next instead of stretching the animation.
        double step = elapsed / ReleaseMilliseconds;
        _strength = _target > _strength
            ? Math.Min(_strength + step, _target)
            : Math.Max(_strength - step, _target);

        Render();

        if (_strength != _target)
        {
            return;
        }

        _timer.Stop();

        if (_strength == 0 && _items is ItemsControl items)
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
        double strength = Easing.Smoothstep(_strength);
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
