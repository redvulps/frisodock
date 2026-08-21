using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// Picks the battery icon in Segoe Fluent Icons. A pure function, therefore testable.
///
/// The font carries three series of eleven icons each — discharging, charging and battery
/// saver —, one for each tenth of charge. Picking the icon is finding the right tenth in the right
/// series, and that is all that lives here.
/// </summary>
public static class BatteryGlyphs
{
    private const int Discharging = 0xE850;
    private const int Charging = 0xE85B;
    private const int Saver = 0xE866;

    /// <summary>Each series covers 0 to 10 tenths, that is, eleven icons.</summary>
    private const int StepsPerSeries = 10;

    public static string For(BatteryStatus battery)
    {
        int series = battery switch
        {
            { IsCharging: true } => Charging,
            { IsSaverOn: true } => Saver,
            _ => Discharging,
        };

        int step = Math.Clamp((int)Math.Round(battery.Percent / 10.0), 0, StepsPerSeries);

        return char.ConvertFromUtf32(series + step);
    }
}
