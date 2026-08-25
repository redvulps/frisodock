using System.Globalization;
using FrisoDock.App.Services;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Measuring the clock in the real font. It depends on WPF and on the machine's font, so what is
/// asserted here is what holds on any of them: that the measurement happens, that it is coherent, and
/// that a failure brings nothing down.
/// </summary>
public sealed class ClockWidthMeasurerTests
{
    [Theory]
    [InlineData("pt-BR")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("ja-JP")]
    public void Measure_WalksTheWholeYearWithoutBreaking(string culture)
    {
        // The samples sweep the twelve months; a 30th in February killed the dock at startup,
        // before any window existed.
        DockMetrics measured = ClockWidthMeasurer.Measure(
            DockMetrics.Default,
            new ClockFormatter(new CultureInfo(culture)));

        Assert.True(measured.ClockContentWidth > 0);
        Assert.True(measured.ClockContentWidthWithSeconds >= measured.ClockContentWidth);
    }

    [Fact]
    public void Measure_PreservesTheRestOfTheMetrics()
    {
        var original = new DockMetrics(IconSize: 40, Padding: 5);

        DockMetrics measured = ClockWidthMeasurer.Measure(original, new ClockFormatter(new CultureInfo("pt-BR")));

        Assert.Equal(original.IconSize, measured.IconSize);
        Assert.Equal(original.Padding, measured.Padding);
    }
}
