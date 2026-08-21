using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Orchestrates the dock placement: it asks <see cref="IScreenProvider"/> for the geometry,
/// computes the rectangles with <see cref="DockLayoutCalculator"/>, moves the window through
/// <see cref="IWindowPositioner"/> and reserves space through <see cref="IAppBarService"/>.
///
/// It computes no geometry and calls no Win32 directly (SRP): it only coordinates the collaborators.
/// </summary>
public sealed class DockPlacementService
{
    private readonly IScreenProvider _screenProvider;
    private readonly IWindowPositioner _positioner;
    private readonly IAppBarService _appBar;
    private readonly DockLayoutCalculator _layout;
    private readonly DockSettingsService _settings;

    private nint _windowHandle;

    private DockSettings Settings => _settings.Current;

    public DockPlacementService(
        IScreenProvider screenProvider,
        IWindowPositioner positioner,
        IAppBarService appBar,
        DockLayoutCalculator layout,
        DockSettingsService settings)
    {
        _screenProvider = screenProvider;
        _positioner = positioner;
        _appBar = appBar;
        _layout = layout;
        _settings = settings;
    }

    /// <summary>Binds the service to the dock window and registers the appbar, if configured.</summary>
    public void Attach(nint windowHandle, uint appBarCallbackMessage)
    {
        _windowHandle = windowHandle;

        if (Settings.ReserveScreenSpace)
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
        if (_windowHandle == 0)
        {
            return;
        }

        MonitorInfo monitor = _screenProvider.GetPrimaryMonitor();

        if (Settings.ReserveScreenSpace)
        {
            PixelRect reservation = _layout.CalculateReservationRect(monitor, Settings.Edge, Settings.Metrics);
            _appBar.SetPosition(Settings.Edge, reservation);
        }

        // The window is larger than the panel: the headroom takes the magnified icon, which overflows
        // out of the bar instead of pushing the neighbours.
        PixelRect window = _layout.CalculateWindowRect(
            monitor,
            Settings.Edge,
            itemCount,
            Settings.Metrics,
            Settings.EffectiveMagnification);

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

        if (Settings.ReserveScreenSpace)
        {
            _appBar.Register(_windowHandle, appBarCallbackMessage);
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
