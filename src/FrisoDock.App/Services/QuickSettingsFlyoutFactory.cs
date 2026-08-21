using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;

namespace FrisoDock.App.Services;

/// <summary>
/// Creates the quick settings panel model. That is all (SRP).
///
/// It exists because the panel is created and destroyed on every open — it has a timer of its own
/// that must only run while it is on screen —, so it cannot come ready from the container.
/// </summary>
public sealed class QuickSettingsFlyoutFactory
{
    private readonly IRadioController _radios;
    private readonly IBrightnessController _brightness;
    private readonly IVolumeController _volume;
    private readonly IBatteryProvider _battery;
    private readonly INetworkProvider _network;
    private readonly ISettingsPageLauncher _launcher;

    public QuickSettingsFlyoutFactory(
        IRadioController radios,
        IBrightnessController brightness,
        IVolumeController volume,
        IBatteryProvider battery,
        INetworkProvider network,
        ISettingsPageLauncher launcher)
    {
        _radios = radios;
        _brightness = brightness;
        _volume = volume;
        _battery = battery;
        _network = network;
        _launcher = launcher;
    }

    public QuickSettingsViewModel Create()
    {
        return new QuickSettingsViewModel(_radios, _brightness, _volume, _battery, _network, _launcher);
    }
}
