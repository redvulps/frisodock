namespace FrisoDock.Core.Models;

/// <summary>Kind of radio the quick settings panel controls.</summary>
public enum RadioDeviceKind
{
    WiFi = 0,
    Bluetooth = 1,
}

/// <summary>
/// State of a system radio.
/// </summary>
/// <param name="Kind">Kind of radio.</param>
/// <param name="IsOn">Whether it is on.</param>
/// <param name="IsAvailable">Whether the machine has that radio and Windows allows touching it.</param>
public readonly record struct RadioStatus(RadioDeviceKind Kind, bool IsOn, bool IsAvailable)
{
    public static RadioStatus Unavailable(RadioDeviceKind kind) => new(kind, false, false);
}

/// <summary>
/// Battery state.
/// </summary>
/// <param name="HasBattery">False on desktops, where the tile must not appear.</param>
/// <param name="Percent">Remaining charge, from 0 to 100.</param>
/// <param name="IsCharging">Whether it is charging right now.</param>
/// <param name="IsSaverOn">Whether battery saver is on.</param>
public readonly record struct BatteryStatus(bool HasBattery, int Percent, bool IsCharging, bool IsSaverOn)
{
    public static BatteryStatus None => new(false, 0, false, false);
}

/// <summary>
/// Volume of the default output device.
/// </summary>
/// <param name="Level">Volume from 0 to 100.</param>
/// <param name="IsMuted">Whether it is muted.</param>
/// <param name="IsAvailable">Whether there is an output device.</param>
public readonly record struct VolumeStatus(int Level, bool IsMuted, bool IsAvailable)
{
    public static VolumeStatus Unavailable => new(0, false, false);
}

/// <summary>
/// Screen brightness.
/// </summary>
/// <param name="Level">Brightness from 0 to 100.</param>
/// <param name="IsAvailable">
/// False when there is no panel that accepts the command — external monitors without DDC/CI and most
/// desktops. The tile disappears in that case, instead of showing a control that does nothing.
/// </param>
public readonly record struct BrightnessStatus(int Level, bool IsAvailable)
{
    public static BrightnessStatus Unavailable => new(0, false);
}

/// <summary>
/// Network connection in use.
/// </summary>
/// <param name="Name">Network name, or null when there is no connection.</param>
/// <param name="IsWireless">Whether the connection is wireless.</param>
public readonly record struct NetworkStatus(string? Name, bool IsWireless)
{
    public static NetworkStatus Disconnected => new(null, false);
}
