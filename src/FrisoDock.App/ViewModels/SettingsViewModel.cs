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
    private bool _reserveScreenSpace;

    [ObservableProperty]
    private bool _enableMagnification;

    [ObservableProperty]
    private double _magnificationScale;

    [ObservableProperty]
    private bool _enableWindowPreviews;

    public SettingsViewModel(DockSettingsService settings)
    {
        _settings = settings;
        Load(_settings.Current);
    }

    /// <summary>Smallest magnification offered, where the effect is still noticeable.</summary>
    public double MinimumMagnification => 1.1;

    /// <summary>Largest magnification offered; beyond that the dock starts covering the screen.</summary>
    public double MaximumMagnification => 2.0;

    partial void OnHideNativeTaskbarChanged(bool value) => Apply();

    partial void OnReserveScreenSpaceChanged(bool value) => Apply();

    partial void OnEnableMagnificationChanged(bool value) => Apply();

    partial void OnMagnificationScaleChanged(double value) => Apply();

    partial void OnEnableWindowPreviewsChanged(bool value) => Apply();

    private void Load(DockSettings settings)
    {
        _applying = true;

        try
        {
            HideNativeTaskbar = settings.HideNativeTaskbar;
            ReserveScreenSpace = settings.ReserveScreenSpace;
            EnableMagnification = settings.EnableMagnification;
            MagnificationScale = settings.MagnificationScale;
            EnableWindowPreviews = settings.EnableWindowPreviews;
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
            ReserveScreenSpace = ReserveScreenSpace,
            EnableMagnification = EnableMagnification,
            MagnificationScale = Math.Round(MagnificationScale, 2),
            EnableWindowPreviews = EnableWindowPreviews,
        });
    }
}
