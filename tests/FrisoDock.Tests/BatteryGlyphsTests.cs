using FrisoDock.App.ViewModels;
using FrisoDock.Core.Models;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Choice of the battery icon.
///
/// The ranges below were measured in the installed font, rendering glyph by glyph, and not taken
/// from the public table — which describes eleven icons per series and gets it wrong: at E85A the
/// bolt series already begins.
/// </summary>
public sealed class BatteryGlyphsTests
{
    private const int DischargingFirst = 0xE850;
    private const int DischargingLast = 0xE859;
    private const int ChargingFirst = 0xE85A;
    private const int ChargingLast = 0xE862;
    private const int SaverFirst = 0xE863;
    private const int SaverLast = 0xE86B;

    private static BatteryStatus Battery(int percent, bool charging = false, bool saver = false)
    {
        return new BatteryStatus(HasBattery: true, percent, charging, saver);
    }

    private static int Code(BatteryStatus battery)
    {
        return char.ConvertToUtf32(BatteryGlyphs.For(battery), 0);
    }

    [Theory]
    [InlineData(0, DischargingFirst)]
    [InlineData(50, 0xE855)]
    [InlineData(100, DischargingLast)]
    public void Discharging_GoesFromEmptyToFull(int percent, int expected)
    {
        Assert.Equal(expected, Code(Battery(percent)));
    }

    /// <summary>
    /// The error that prompted the fix: at nearly full charge the icon ran past the end of the series and
    /// landed on the empty battery of the bolt series, saying the opposite of the real state.
    /// </summary>
    [Theory]
    [InlineData(95)]
    [InlineData(97)]
    [InlineData(100)]
    public void NearlyFullCharge_DoesNotLeakIntoTheNextSeries(int percent)
    {
        Assert.Equal(DischargingLast, Code(Battery(percent)));
    }

    [Fact]
    public void Charging_HasItsOwnSeries()
    {
        Assert.Equal(0xE85E, Code(Battery(50, charging: true)));
    }

    [Fact]
    public void Charging_BeatsTheSaver()
    {
        // Plugged in, what matters is that it is charging; the saver stays on but the
        // icon that says the most is the charging one.
        Assert.Equal(0xE85E, Code(Battery(50, charging: true, saver: true)));
    }

    [Fact]
    public void Saver_HasItsOwnSeries()
    {
        Assert.Equal(0xE867, Code(Battery(50, saver: true)));
    }

    /// <summary>
    /// The guarantee that was missing: any charge, in any state, has to land inside the matching
    /// series. It is what keeps the icon from turning into another series'.
    /// </summary>
    [Theory]
    [InlineData(false, false, DischargingFirst, DischargingLast)]
    [InlineData(true, false, ChargingFirst, ChargingLast)]
    [InlineData(false, true, SaverFirst, SaverLast)]
    public void AnyCharge_StaysInsideTheSeries(bool charging, bool saver, int first, int last)
    {
        for (var percent = -20; percent <= 220; percent++)
        {
            Assert.InRange(Code(Battery(percent, charging, saver)), first, last);
        }
    }

    /// <summary>The ends of the range are the ends of the series, and not points in the middle of it.</summary>
    [Theory]
    [InlineData(false, false, DischargingFirst, DischargingLast)]
    [InlineData(true, false, ChargingFirst, ChargingLast)]
    [InlineData(false, true, SaverFirst, SaverLast)]
    public void EmptyAndFull_LandOnTheEndsOfTheSeries(bool charging, bool saver, int first, int last)
    {
        Assert.Equal(first, Code(Battery(0, charging, saver)));
        Assert.Equal(last, Code(Battery(100, charging, saver)));
    }

    /// <summary>The charge rises, the icon never falls — and it covers the whole series, with no dead step.</summary>
    [Fact]
    public void RisingCharge_NeverGoesBackAndCoversTheWholeSeries()
    {
        var seen = new HashSet<int>();
        int previous = int.MinValue;

        for (var percent = 0; percent <= 100; percent++)
        {
            int code = Code(Battery(percent));

            Assert.True(code >= previous);
            previous = code;
            seen.Add(code);
        }

        Assert.Equal(DischargingLast - DischargingFirst + 1, seen.Count);
    }
}
