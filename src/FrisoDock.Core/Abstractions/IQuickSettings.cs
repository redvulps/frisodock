using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>Turns the machine's radios on and off.</summary>
public interface IRadioController
{
    /// <summary>
    /// State of a radio. It returns unavailable when the machine has no such radio or Windows
    /// denies access — in that case the tile is disabled instead of lying.
    /// </summary>
    RadioStatus GetStatus(RadioDeviceKind kind);

    /// <summary>Turns the radio on or off. Returns the state that ended up in force.</summary>
    Task<RadioStatus> SetStateAsync(RadioDeviceKind kind, bool isOn);
}

/// <summary>Screen brightness.</summary>
public interface IBrightnessController
{
    BrightnessStatus GetStatus();

    void SetLevel(int level);
}

/// <summary>Volume of the default output device.</summary>
public interface IVolumeController
{
    VolumeStatus GetStatus();

    void SetLevel(int level);

    void SetMuted(bool isMuted);
}

/// <summary>Battery state.</summary>
public interface IBatteryProvider
{
    BatteryStatus GetStatus();
}

/// <summary>Network in use.</summary>
public interface INetworkProvider
{
    NetworkStatus GetStatus();
}

/// <summary>
/// Opens a page of the Windows Settings app.
///
/// It exists because not everything the panel shows has an API without administrator: airplane
/// mode and battery saver, for instance, only Windows itself can turn on. In those cases the dock
/// does what the native panel's arrow button does — it takes the user there.
/// </summary>
public interface ISettingsPageLauncher
{
    void Open(string page);
}
