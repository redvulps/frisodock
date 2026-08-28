using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// Picks the battery icon in Segoe Fluent Icons. A pure function, therefore testable.
///
/// The font carries three series — discharging, charging and battery saver —, each one
/// going from empty to full. Picking the icon is finding the right step in the right series, and
/// that is all that lives here.
///
/// **The series are not the same size, and they are not the ones the documentation describes.**
/// The public Segoe MDL2 table says eleven icons per series (Battery0..Battery10 in E850..E85A),
/// and in this font it is not so. Rendered and measured glyph by glyph: the ink box changes from
/// 26.0x14.0 to 28.8x17.7 already at E85A, which is the bolt series, and again to 30.0x16.0 at
/// E863, which is the leaf one. From E86C on they are already signal bars.
///
/// Following the documentation cost the worst possible error: at 97% charge the step gave 10, and
/// E850+10 = E85A, which is the **empty battery with the bolt**. The icon said the opposite of the state.
/// </summary>
public static class BatteryGlyphs
{
    /// <summary>First glyph of a series and how many steps it has, from empty to full.</summary>
    private readonly record struct Series(int First, int Levels);

    private static readonly Series Discharging = new(0xE850, 10);
    private static readonly Series Charging = new(0xE85A, 9);
    private static readonly Series Saver = new(0xE863, 9);

    public static string For(BatteryStatus battery)
    {
        // The bolt stands for "plugged in", and not for "charging right now" — it is what the native
        // panel does, and it is what BatteryStatus carries.
        Series series = battery switch
        {
            { IsPluggedIn: true } => Charging,
            { IsSaverOn: true } => Saver,
            _ => Discharging,
        };

        return char.ConvertFromUtf32(series.First + StepFor(battery.Percent, series));
    }

    /// <summary>
    /// Step within the series. The calculation is over the number of steps in the series, and not over
    /// tenths: the series have different sizes, and a fixed tenth would run past the end of the smaller ones.
    ///
    /// Rounding is away from zero, and not the .NET default, which ties to even —
    /// with that, 50% of a ten-step series would land on step 4, and not on 5.
    /// </summary>
    private static int StepFor(int percent, Series series)
    {
        int last = series.Levels - 1;
        var step = (int)Math.Round(percent / 100.0 * last, MidpointRounding.AwayFromZero);

        return Math.Clamp(step, 0, last);
    }
}
