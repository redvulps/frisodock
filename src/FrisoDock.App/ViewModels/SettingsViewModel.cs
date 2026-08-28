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
    [NotifyPropertyChangedFor(nameof(EdgeBottom))]
    [NotifyPropertyChangedFor(nameof(EdgeTop))]
    [NotifyPropertyChangedFor(nameof(EdgeLeft))]
    [NotifyPropertyChangedFor(nameof(EdgeRight))]
    private DockEdge _edge;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageSystem))]
    [NotifyPropertyChangedFor(nameof(LanguagePortugueseBrazil))]
    [NotifyPropertyChangedFor(nameof(LanguageEnglish))]
    [NotifyPropertyChangedFor(nameof(LanguageSpanish))]
    private AppLanguage _language;

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
    private bool _useSameAppWindowSwitcher;

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
    /// <summary>
    /// The four edges as exclusive switches, in the same pattern as the hide modes:
    /// only the true value chooses, because the false one arrives when the other button is checked.
    /// </summary>
    public bool EdgeBottom
    {
        get => Edge == DockEdge.Bottom;
        set => SelectEdge(DockEdge.Bottom, value);
    }

    public bool EdgeTop
    {
        get => Edge == DockEdge.Top;
        set => SelectEdge(DockEdge.Top, value);
    }

    public bool EdgeLeft
    {
        get => Edge == DockEdge.Left;
        set => SelectEdge(DockEdge.Left, value);
    }

    public bool EdgeRight
    {
        get => Edge == DockEdge.Right;
        set => SelectEdge(DockEdge.Right, value);
    }

    /// <summary>
    /// The four languages as exclusive switches, in the same pattern as the edges and the hide
    /// modes: only the true value chooses, because the false one arrives when another button
    /// is checked.
    /// </summary>
    public bool LanguageSystem
    {
        get => Language == AppLanguage.System;
        set => SelectLanguage(AppLanguage.System, value);
    }

    public bool LanguagePortugueseBrazil
    {
        get => Language == AppLanguage.PortugueseBrazil;
        set => SelectLanguage(AppLanguage.PortugueseBrazil, value);
    }

    public bool LanguageEnglish
    {
        get => Language == AppLanguage.English;
        set => SelectLanguage(AppLanguage.English, value);
    }

    public bool LanguageSpanish
    {
        get => Language == AppLanguage.Spanish;
        set => SelectLanguage(AppLanguage.Spanish, value);
    }

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

    partial void OnEdgeChanged(DockEdge value) => Apply();

    partial void OnLanguageChanged(AppLanguage value) => Apply();

    partial void OnHideModeChanged(DockHideMode value) => Apply();

    partial void OnHideNativeTaskbarChanged(bool value) => Apply();

    partial void OnReserveScreenSpaceChanged(bool value) => Apply();

    partial void OnEnableMagnificationChanged(bool value) => Apply();

    partial void OnMagnificationScaleChanged(double value) => Apply();

    partial void OnEnableWindowPreviewsChanged(bool value) => Apply();

    partial void OnShowClockSecondsChanged(bool value) => Apply();

    partial void OnUseGroupedWindowSwitcherChanged(bool value) => Apply();

    partial void OnUseSameAppWindowSwitcherChanged(bool value) => Apply();

    partial void OnShowOnAllMonitorsChanged(bool value) => Apply();

    partial void OnIsolateMonitorAppsChanged(bool value) => Apply();

    private void SelectMode(DockHideMode mode, bool selected)
    {
        if (selected)
        {
            HideMode = mode;
        }
    }

    private void SelectEdge(DockEdge edge, bool selected)
    {
        if (selected)
        {
            Edge = edge;
        }
    }

    private void SelectLanguage(AppLanguage language, bool selected)
    {
        if (selected)
        {
            Language = language;
        }
    }

    private void Load(DockSettings settings)
    {
        _applying = true;

        try
        {
            HideNativeTaskbar = settings.HideNativeTaskbar;
            Edge = settings.Edge;
            Language = settings.Language;
            HideMode = settings.HideMode;
            ReserveScreenSpace = settings.ReserveScreenSpace;
            EnableMagnification = settings.EnableMagnification;
            MagnificationScale = settings.MagnificationScale;
            EnableWindowPreviews = settings.EnableWindowPreviews;
            ShowClockSeconds = settings.ShowClockSeconds;
            UseGroupedWindowSwitcher = settings.UseGroupedWindowSwitcher;
            UseSameAppWindowSwitcher = settings.UseSameAppWindowSwitcher;
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
            Edge = Edge,
            Language = Language,
            HideMode = HideMode,
            ReserveScreenSpace = ReserveScreenSpace,
            EnableMagnification = EnableMagnification,
            MagnificationScale = Math.Round(MagnificationScale, 2),
            EnableWindowPreviews = EnableWindowPreviews,
            ShowClockSeconds = ShowClockSeconds,
            UseGroupedWindowSwitcher = UseGroupedWindowSwitcher,
            UseSameAppWindowSwitcher = UseSameAppWindowSwitcher,
            ShowOnAllMonitors = ShowOnAllMonitors,
            IsolateMonitorApps = IsolateMonitorApps,
        });
    }
}
