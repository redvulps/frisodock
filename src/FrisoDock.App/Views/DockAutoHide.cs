using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Views;

/// <summary>
/// Hides and reveals the dock. That is all (SRP): the decision belongs to <see cref="DockVisibilityPolicy"/>,
/// which is a pure function, and the offsetting to <see cref="DockPlacementService"/>. What lives here
/// is timing — when to ask, how long to wait and over how many frames to slide.
///
/// There is no global mouse hook, which the architecture forbids: the cursor position is read by
/// sampling. The dock never leaves the screen entirely — a sliver of a few pixels stays at the edge,
/// and touching it is what brings it back.
/// </summary>
public sealed class DockAutoHide : IDisposable
{
    /// <summary>Animation frames. At 16 ms per frame that gives about 190 ms of slide.</summary>
    private const int AnimationFrames = 12;

    /// <summary>
    /// Consecutive evaluations asking to hide before the dock leaves. Crossing the dock with the
    /// mouse on the way to something else must not trigger the animation.
    /// </summary>
    private const int HideConfirmations = 2;

    private readonly DockPlacementService _placement;
    private readonly IWindowEnumerator _windows;
    private readonly DockSettingsService _settings;
    private readonly DockVisibilityPolicy _policy;
    private readonly ICursorProvider _cursor;
    private readonly Func<bool> _hasOpenFlyout;
    private readonly DispatcherTimer _watchTimer;
    private readonly DispatcherTimer _animationTimer;

    private bool _revealed = true;
    private int _hideVotes;
    private int _frame = AnimationFrames;
    private int _targetFrame = AnimationFrames;
    private bool _disposed;

    public DockAutoHide(
        DockPlacementService placement,
        IWindowEnumerator windows,
        DockSettingsService settings,
        DockVisibilityPolicy policy,
        ICursorProvider cursor,
        Func<bool> hasOpenFlyout)
    {
        _placement = placement;
        _windows = windows;
        _settings = settings;
        _policy = policy;
        _cursor = cursor;
        _hasOpenFlyout = hasOpenFlyout;

        // Not everything that changes the decision raises an event: a window dragged over the dock
        // raises no creation or focus WinEvent. Hence the periodic check.
        _watchTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _watchTimer.Tick += OnWatchTick;

        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _animationTimer.Tick += OnAnimationTick;
    }

    public void Start()
    {
        _watchTimer.Start();
        Evaluate();
    }

    /// <summary>Re-evaluates now, without waiting for the next timer cycle.</summary>
    public void Evaluate()
    {
        DockSettings settings = _settings.Current;

        var input = new DockVisibilityInput(
            settings.HideMode,
            IsPointerInZone(),
            _hasOpenFlyout(),
            DetectOverlap(settings));

        if (_policy.ShouldReveal(input))
        {
            _hideVotes = 0;
            SetRevealed(true);
            return;
        }

        if (_revealed && ++_hideVotes < HideConfirmations)
        {
            return;
        }

        SetRevealed(false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watchTimer.Stop();
        _watchTimer.Tick -= OnWatchTick;
        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;

        _disposed = true;
    }

    /// <summary>
    /// Whether the cursor is in the zone that keeps the dock on screen.
    ///
    /// The zone depends on the current state — small with the dock hidden, large with it in view.
    /// It is that difference that keeps the dock from flickering between the two states.
    /// </summary>
    private bool IsPointerInZone()
    {
        PixelRect zone = _revealed ? _placement.HoverZone : _placement.RevealZone;

        return zone.Contains(_cursor.GetPosition());
    }

    /// <summary>
    /// Whether some window occupies the dock's place. Only intellihide needs to know; in the other
    /// modes enumerating the windows four times a second would be work thrown away.
    /// </summary>
    private bool DetectOverlap(DockSettings settings)
    {
        if (settings.HideMode != DockHideMode.WhenWindowOverlaps)
        {
            return false;
        }

        return _policy.AnyWindowOverlaps(_windows.GetWindows(), _placement.PanelRect);
    }

    private void SetRevealed(bool revealed)
    {
        if (_revealed == revealed && _targetFrame == (revealed ? AnimationFrames : 0))
        {
            return;
        }

        _revealed = revealed;
        _targetFrame = revealed ? AnimationFrames : 0;

        if (_frame == _targetFrame)
        {
            return;
        }

        _animationTimer.Start();
    }

    private void OnWatchTick(object? sender, EventArgs e)
    {
        Evaluate();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        _frame += Math.Sign(_targetFrame - _frame);
        _placement.SetReveal(Easing.Smoothstep(_frame / (double)AnimationFrames));

        if (_frame == _targetFrame)
        {
            _animationTimer.Stop();
        }
    }
}
