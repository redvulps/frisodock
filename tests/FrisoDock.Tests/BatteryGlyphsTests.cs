using FrisoDock.App.ViewModels;
using FrisoDock.Core.Models;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Choice of the battery icon. The font carries three series of eleven icons — discharging,
/// charging and saver —, and what is tested here is landing on the right series and tenth.
/// </summary>
public sealed class BatteryGlyphsTests
{
    [Theory]
    [InlineData(0, 0xE850)]
    [InlineData(50, 0xE855)]
    [InlineData(100, 0xE85A)]
    public void Discharging_UsesTheBaseSeries(int percent, int expected)
    {
        var battery = new BatteryStatus(HasBattery: true, percent, IsCharging: false, IsSaverOn: false);

        Assert.Equal(char.ConvertFromUtf32(expected), BatteryGlyphs.For(battery));
    }

    [Fact]
    public void Charging_HasItsOwnSeries()
    {
        var battery = new BatteryStatus(HasBattery: true, Percent: 50, IsCharging: true, IsSaverOn: false);

        Assert.Equal(char.ConvertFromUtf32(0xE860), BatteryGlyphs.For(battery));
    }

    [Fact]
    public void Charging_BeatsTheSaver()
    {
        // Plugged in, what matters is that it is charging; the saver stays on but the
        // icon that says the most is the charging one.
        var battery = new BatteryStatus(HasBattery: true, Percent: 50, IsCharging: true, IsSaverOn: true);

        Assert.Equal(char.ConvertFromUtf32(0xE860), BatteryGlyphs.For(battery));
    }

    [Fact]
    public void Saver_HasItsOwnSeries()
    {
        var battery = new BatteryStatus(HasBattery: true, Percent: 50, IsCharging: false, IsSaverOn: true);

        Assert.Equal(char.ConvertFromUtf32(0xE86B), BatteryGlyphs.For(battery));
    }

    [Theory]
    [InlineData(-20)]
    [InlineData(250)]
    public void PercentageOutOfRange_DoesNotLeaveTheSeries(int percent)
    {
        var battery = new BatteryStatus(HasBattery: true, percent, IsCharging: false, IsSaverOn: false);

        string glyph = BatteryGlyphs.For(battery);
        int code = char.ConvertToUtf32(glyph, 0);

        Assert.InRange(code, 0xE850, 0xE85A);
    }

    [Fact]
    public void RoundsToTheNearestTenth()
    {
        var battery = new BatteryStatus(HasBattery: true, Percent: 46, IsCharging: false, IsSaverOn: false);

        Assert.Equal(char.ConvertFromUtf32(0xE855), BatteryGlyphs.For(battery));
    }
}
