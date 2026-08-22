using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// Behaviour shared by the dock flyouts: native Windows 11 finish, positioning
/// above the element that opened them and dismissal on focus loss.
///
/// It is composition and not inheritance on purpose: each flyout has its own XAML, and swapping
/// their root for a base class just to share this behaviour would complicate the markup with no
/// gain at all.
/// </summary>
public sealed class FlyoutChrome
{
    /// <summary>Gap between the flyout and the element that opened it, in the Windows standard.</summary>
    private const int AnchorGap = 8;

    /// <summary>
    /// Where the window is born, far from any real desktop area.
    ///
    /// The size depends on the content, and content is only measured after the first render —
    /// the final position is only known once the window exists. Lowering the opacity does not help:
    /// <c>Window.Opacity</c> requires AllowsTransparency, which is off so the DWM can
    /// draw the acrylic, and with it off the window would appear opaque in the screen corner.
    /// </summary>
    private const double OffscreenOrigin = -32000;

    private readonly Window _window;
    private readonly PixelRect _anchor;
    private readonly IWindowBackdrop _backdrop;
    private readonly IWindowPositioner _positioner;
    private readonly IWindowSwitcherExclusion _switcherExclusion;
    private readonly IScreenProvider _screens;

    public FlyoutChrome(
        Window window,
        PixelRect anchor,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IWindowSwitcherExclusion switcherExclusion,
        IScreenProvider screens)
    {
        _window = window;
        _anchor = anchor;
        _backdrop = backdrop;
        _positioner = positioner;
        _switcherExclusion = switcherExclusion;
        _screens = screens;

        _window.Left = OffscreenOrigin;
        _window.Top = OffscreenOrigin;
    }

    /// <summary>Asks the DWM for the flyout finish and clears the background so the material shows.</summary>
    public void ApplyAppearance()
    {
        ApplyAppearance(_window, _backdrop);

        // A flyout is not an Alt+Tab destination. The settings screen, which uses only the static
        // overload above, stays in the switcher: it is a window worth coming back to.
        _switcherExclusion.Exclude(new WindowInteropHelper(_window).Handle);
    }

    /// <summary>
    /// The same finish, for windows that are not anchored flyouts — the settings screen,
    /// for instance, which appears centred but uses the same system material.
    /// </summary>
    public static void ApplyAppearance(Window window, IWindowBackdrop backdrop)
    {
        // The order here is not style, it is a requirement: clear the background first, request the
        // material afterwards. Without this WPF paints an opaque background over the DWM material — and,
        // worse, touching the CompositionTarget with the material already applied rebuilds the composition
        // surface and the content disappears, leaving only acrylic in a correctly sized, empty window.
        if (PresentationSource.FromVisual(window) is HwndSource source && source.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        nint handle = new WindowInteropHelper(window).Handle;
        backdrop.ApplyFlyoutAppearance(handle, darkMode: true);
    }

    /// <summary>
    /// Moves the flyout above the anchor element. Since the window was born off screen, it is this
    /// call that makes it visible — which is why it can only happen after the measurement.
    /// </summary>
    public void PositionAboveAnchor()
    {
        MonitorInfo monitor = _screens.GetPrimaryMonitor();
        double scale = monitor.DpiScale <= 0 ? 1.0 : monitor.DpiScale;

        int width = (int)Math.Ceiling(_window.ActualWidth * scale);
        int height = (int)Math.Ceiling(_window.ActualHeight * scale);
        int gap = (int)Math.Round(AnchorGap * scale);

        int left = _anchor.Left + ((_anchor.Width - width) / 2);
        int top = _anchor.Top - height - gap;

        // Keeps the flyout from leaving the screen when the anchor is near one of the edges.
        left = Math.Clamp(left, monitor.Bounds.Left, Math.Max(monitor.Bounds.Right - width, monitor.Bounds.Left));
        top = Math.Max(top, monitor.Bounds.Top);

        _positioner.SetBounds(
            new WindowInteropHelper(_window).Handle,
            PixelRect.FromSize(left, top, width, height),
            topMost: true);
    }

    /// <summary>An element's rectangle in physical pixels, to serve as an anchor.</summary>
    public static PixelRect GetScreenRect(FrameworkElement element)
    {
        Point topLeft = element.PointToScreen(new Point(0, 0));
        Point bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));

        return new PixelRect(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X),
            (int)Math.Round(bottomRight.Y));
    }
}
