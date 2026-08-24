using CommunityToolkit.Mvvm.ComponentModel;
using FrisoDock.App.Services;
using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// State of the settings screen.
///
/// Each change applies immediately, with no save button: it is what Windows does in its own
/// settings, and in a dock the effect of every option is visible right behind the window —
/// forcing a confirmation would hide exactly that visual feedback.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly DockSettingsService _settings;

    // While the values are being reloaded from the service, the setters must not fire
    // new writes — otherwise each reload would turn into a write to disk.
    private bool _applying;

    [ObservableProperty]
    private bool _hideNativeTaskbar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReserveScreenSpace))]
    [NotifyPropertyChangedFor(nameof(HideNever))]
    [NotifyPropertyChangedFor(nameof(HideAlways))]
    [NotifyPropertyChangedFor(nameof(HideWhenWindowOverlaps))]
    private DockHideMode _hideMode;

    [ObservableProperty]
    private bool _reserveScreenSpace;

    [ObservableProperty]
    private bool _enableMagnification;

    [ObservableProperty]
    private double _magnificationScale;

    [ObservableProperty]
    private bool _enableWindowPreviews;

    [ObservableProperty]
    private bool _showClockSeconds;

    [ObservableProperty]
    private bool _useGroupedWindowSwitcher;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanIsolateMonitorApps))]
    private bool _showOnAllMonitors;

    [ObservableProperty]
    private bool _isolateMonitorApps;

    public SettingsViewModel(DockSettingsService settings)
    {
        _settings = settings;
        Load(_settings.Current);
    }

    /// <summary>
    /// The three modes as exclusive switches, which is what the XAML can bind to a
    /// RadioButton. Only the true value chooses: the false one arrives when the other button is
    /// checked, and obeying it would erase the choice that was just made.
    /// </summary>
    public bool HideNever
    {
        get => HideMode == DockHideMode.Never;
        set => SelectMode(DockHideMode.Never, value);
    }

    public bool HideAlways
    {
        get => HideMode == DockHideMode.Always;
        set => SelectMode(DockHideMode.Always, value);
    }

    public bool HideWhenWindowOverlaps
    {
        get => HideMode == DockHideMode.WhenWindowOverlaps;
        set => SelectMode(DockHideMode.WhenWindowOverlaps, value);
    }

    /// <summary>
    /// Reserving space only makes sense with the dock always in view. In the other modes the option is
    /// disabled instead of accepting a value the dock would ignore.
    /// </summary>
    public bool CanReserveScreenSpace => HideMode == DockHideMode.Never;

    /// <summary>
    /// Separating apps per monitor only makes sense with a dock on each. With a single dock, isolating
    /// would hide the other screens' apps with nothing in return.
    /// </summary>
    public bool CanIsolateMonitorApps => ShowOnAllMonitors;

    /// <summary>Smallest magnification offered, where the effect is still noticeable.</summary>
    public double MinimumMagnification => 1.1;

    /// <summary>Largest magnification offered; beyond that the dock starts covering the screen.</summary>
    public double MaximumMagnification => 2.0;

    partial void OnHideModeChanged(DockHideMode value) => Apply();

    partial void OnHideNativeTaskbarChanged(bool value) => Apply();

    partial void OnReserveScreenSpaceChanged(bool value) => Apply();

    partial void OnEnableMagnificationChanged(bool value) => Apply();

    partial void OnMagnificationScaleChanged(double value) => Apply();

    partial void OnEnableWindowPreviewsChanged(bool value) => Apply();

    partial void OnShowClockSecondsChanged(bool value) => Apply();

    partial void OnUseGroupedWindowSwitcherChanged(bool value) => Apply();

    partial void OnShowOnAllMonitorsChanged(bool value) => Apply();

    partial void OnIsolateMonitorAppsChanged(bool value) => Apply();

    private void SelectMode(DockHideMode mode, bool selected)
    {
        if (selected)
        {
            HideMode = mode;
        }
    }

    private void Load(DockSettings settings)
    {
        _applying = true;

        try
        {
            HideNativeTaskbar = settings.HideNativeTaskbar;
            HideMode = settings.HideMode;
            ReserveScreenSpace = settings.ReserveScreenSpace;
            EnableMagnification = settings.EnableMagnification;
            MagnificationScale = settings.MagnificationScale;
            EnableWindowPreviews = settings.EnableWindowPreviews;
            ShowClockSeconds = settings.ShowClockSeconds;
            UseGroupedWindowSwitcher = settings.UseGroupedWindowSwitcher;
            ShowOnAllMonitors = settings.ShowOnAllMonitors;
            IsolateMonitorApps = settings.IsolateMonitorApps;
        }
        finally
        {
            _applying = false;
        }
    }

    private void Apply()
    {
        if (_applying)
        {
            return;
        }

        _settings.Update(_settings.Current with
        {
            HideNativeTaskbar = HideNativeTaskbar,
            HideMode = HideMode,
            ReserveScreenSpace = ReserveScreenSpace,
            EnableMagnification = EnableMagnification,
            MagnificationScale = Math.Round(MagnificationScale, 2),
            EnableWindowPreviews = EnableWindowPreviews,
            ShowClockSeconds = ShowClockSeconds,
            UseGroupedWindowSwitcher = UseGroupedWindowSwitcher,
            ShowOnAllMonitors = ShowOnAllMonitors,
            IsolateMonitorApps = IsolateMonitorApps,
        });
    }
}
