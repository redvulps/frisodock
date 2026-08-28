using System.Windows;
using System.Windows.Interop;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// The switcher window: the row of apps that appears while Alt is held.
///
/// It listens to no keyboard. Recognizing the gesture belongs to the low-level hook, which sees the
/// keys before any window — including when Alt+Tab starts with the focus on another app, which is
/// always the case.
/// </summary>
public partial class WindowSwitcherWindow : Window
{
    private readonly IWindowBackdrop _backdrop;
    private readonly IWindowPositioner _positioner;
    private readonly IWindowSwitcherExclusion _switcherExclusion;
    private readonly MonitorInfo _monitor;

    /// <summary>
    /// Where the window is born, off any screen.
    ///
    /// The size depends on the content, and content is only measured after the first render.
    /// Lowering the opacity does not help: <c>Window.Opacity</c> requires AllowsTransparency, which is
    /// off so the DWM can draw the acrylic — the window would appear opaque in the screen corner.
    /// </summary>
    private const double OffscreenOrigin = -32000;

    public WindowSwitcherWindow(
        WindowSwitcherViewModel viewModel,
        MonitorInfo monitor,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IWindowSwitcherExclusion switcherExclusion)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _backdrop = backdrop;
        _positioner = positioner;
        _switcherExclusion = switcherExclusion;
        _monitor = monitor;

        Left = OffscreenOrigin;
        Top = OffscreenOrigin;
        MaxWidth = Math.Max(monitor.Bounds.Width / (monitor.DpiScale <= 0 ? 1.0 : monitor.DpiScale) - 80, 200);

        DataContext = viewModel;
    }

    /// <summary>Handle of this window, for whoever needs to bring it to the foreground through Win32.</summary>
    public nint Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        FlyoutChrome.ApplyAppearance(this, _backdrop, WindowBackdropMaterial.Flyout);

        // The switcher is not a switching destination. Without this it would show up in the native
        // Alt+Tab list itself, which stays active for anyone who turns the option off.
        _switcherExclusion.Exclude(new WindowInteropHelper(this).Handle);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        CenterOnMonitor();
    }

    /// <summary>
    /// Centres on the chosen monitor, in physical pixels.
    ///
    /// The WPF <c>WindowStartupLocation="CenterScreen"</c> always centres on the primary screen,
    /// and the switcher has to be born where the user is looking.
    /// </summary>
    private void CenterOnMonitor()
    {
        double scale = _monitor.DpiScale <= 0 ? 1.0 : _monitor.DpiScale;

        int width = (int)Math.Ceiling(ActualWidth * scale);
        int height = (int)Math.Ceiling(ActualHeight * scale);

        PixelRect bounds = _monitor.Bounds;
        int left = bounds.Left + ((bounds.Width - width) / 2);
        int top = bounds.Top + ((bounds.Height - height) / 2);

        _positioner.SetBounds(
            new WindowInteropHelper(this).Handle,
            PixelRect.FromSize(left, top, width, height),
            topMost: true);
    }
}
