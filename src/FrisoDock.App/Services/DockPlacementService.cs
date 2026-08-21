using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Orchestrates the placement of one dock: computes the rectangles with
/// <see cref="DockLayoutCalculator"/>, moves the window through <see cref="IWindowPositioner"/> and
/// reserves space through <see cref="IAppBarService"/>.
///
/// There is one per dock, and each knows only its own monitor — with a dock on each screen, two
/// docks sharing this service would fight over the same handle and the same appbar.
///
/// It computes no geometry and calls no Win32 directly (SRP): it only coordinates the collaborators.
/// </summary>
public sealed class DockPlacementService
{
    private readonly IWindowPositioner _positioner;
    private readonly IAppBarService _appBar;
    private readonly DockLayoutCalculator _layout;
    private readonly DockSettingsService _settings;
    private readonly DockMonitor _monitor;

    private nint _windowHandle;
    private int _itemCount;
    private double _revealProgress = 1.0;
    private double _dpiScale = 1.0;
    private PixelRect _windowRect;

    private DockSettings Settings => _settings.Current;

    /// <summary>
    /// Where the panel sits when it is in view, in physical pixels.
    ///
    /// It is always the revealed position, even with the dock hidden: intellihide asks whether
    /// some window occupies the dock's place, and using the hidden position — off screen — would
    /// always answer "no", revealing the dock only to hide it again on the next frame.
    /// </summary>
    public PixelRect PanelRect { get; private set; }

    /// <summary>Band at the screen edge that brings the hidden dock back.</summary>
    public PixelRect RevealZone { get; private set; }

    /// <summary>Area that keeps the already revealed dock on screen.</summary>
    public PixelRect HoverZone { get; private set; }

    public DockPlacementService(
        IWindowPositioner positioner,
        IAppBarService appBar,
        DockLayoutCalculator layout,
        DockSettingsService settings,
        DockMonitorHolder monitor)
    {
        _positioner = positioner;
        _appBar = appBar;
        _layout = layout;
        _settings = settings;
        _monitor = monitor.Monitor;
    }

    /// <summary>Binds the service to the dock window and registers the appbar, if configured.</summary>
    public void Attach(nint windowHandle, uint appBarCallbackMessage)
    {
        _windowHandle = windowHandle;

        if (Settings.ReservesScreenSpace)
        {
            _appBar.Register(windowHandle, appBarCallbackMessage);
        }
    }

    /// <summary>
    /// Repositions the dock for the current item count. Called on every list change and
    /// whenever the shell reports that the appbar layout changed.
    /// </summary>
    public void Update(int itemCount)
    {
        _itemCount = itemCount;

        if (_windowHandle == 0)
        {
            return;
        }

        MonitorInfo monitor = _monitor.Info;
        _dpiScale = monitor.DpiScale;

        if (Settings.ReservesScreenSpace)
        {
            PixelRect reservation = _layout.CalculateReservationRect(monitor, Settings.Edge, Settings.EffectiveMetrics);
            _appBar.SetPosition(Settings.Edge, reservation);
        }

        PanelRect = _layout.CalculatePanelRect(monitor, Settings.Edge, itemCount, Settings.EffectiveMetrics);

        // The window is larger than the panel on both axes: the margins take the magnified icon and
        // the widening of the bar. The rectangle is kept because the hide animation only offsets
        // this value, frame by frame — recomputing everything on every frame would be waste.
        _windowRect = _layout.CalculateWindowRect(
            monitor,
            Settings.Edge,
            itemCount,
            Settings.EffectiveMetrics,
            Settings.EffectiveMagnification);

        RevealZone = _layout.CalculateRevealZone(PanelRect, monitor, Settings.Edge, Settings.EffectiveMetrics);
        HoverZone = _layout.CalculateHoverZone(_windowRect, monitor, Settings.Edge);

        ApplyBounds();
    }

    /// <summary>
    /// Moves the dock between the revealed and the hidden position.
    /// </summary>
    /// <param name="revealProgress">1 is fully in view; 0, hidden at the screen edge.</param>
    public void SetReveal(double revealProgress)
    {
        _revealProgress = Math.Clamp(revealProgress, 0.0, 1.0);

        if (_windowHandle == 0)
        {
            return;
        }

        if (_windowRect.Width == 0)
        {
            // There has been no Update yet: with no base rectangle there is nothing to offset.
            Update(_itemCount);
            return;
        }

        ApplyBounds();
    }

    private void ApplyBounds()
    {
        PixelRect window = _layout.ApplyReveal(
            _windowRect,
            Settings.Edge,
            Settings.EffectiveMetrics,
            _revealProgress,
            _dpiScale);

        _positioner.SetBounds(_windowHandle, window, topMost: true);
    }

    /// <summary>
    /// Turns the space reservation on or off with the dock running, as currently configured.
    /// </summary>
    public void ApplyScreenReservation(uint appBarCallbackMessage)
    {
        if (_windowHandle == 0)
        {
            return;
        }

        if (Settings.ReservesScreenSpace)
        {
            _appBar.Register(_windowHandle, appBarCallbackMessage);

            // ABM_NEW only creates the appbar; what reserves the band is ABM_SETPOS, inside Update.
            // Without this call, turning the option on with the dock running takes space from nobody.
            Update(_itemCount);
            return;
        }

        _appBar.Unregister();
    }

    /// <summary>Reasserts the dock at the top of the Z order.</summary>
    public void BringToTop()
    {
        _positioner.BringToTop(_windowHandle);
    }

    /// <summary>Releases the reserved space. Must run before shutdown.</summary>
    public void Detach()
    {
        _appBar.Unregister();
        _windowHandle = 0;
    }
}
