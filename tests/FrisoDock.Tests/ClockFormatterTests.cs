using System.Globalization;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Clock formatting. The culture is pinned in the tests so the result does not depend on the machine
/// running them.
/// </summary>
public sealed class ClockFormatterTests
{
    private static readonly DateTimeOffset Instant =
        new(2026, 8, 21, 21, 5, 42, 300, TimeSpan.FromHours(-3));

    [Fact]
    public void FormatTime_InATwentyFourHourCulture_DoesNotShowSeconds()
    {
        var formatter = new ClockFormatter(new CultureInfo("pt-BR"));

        Assert.Equal("21:05", formatter.FormatTime(Instant));
    }

    [Fact]
    public void FormatTime_InATwelveHourCulture_UsesTheCultureSuffix()
    {
        // Whoever uses 12 hours expects 9:05 PM; imposing 24 would impose the developer's taste.
        var formatter = new ClockFormatter(new CultureInfo("en-US"));

        Assert.Equal("9:05 PM", formatter.FormatTime(Instant));
    }

    [Fact]
    public void FormatDate_FollowsTheCultureOrder()
    {
        Assert.Equal("21/08/2026", new ClockFormatter(new CultureInfo("pt-BR")).FormatDate(Instant));
        Assert.Equal("8/21/2026", new ClockFormatter(new CultureInfo("en-US")).FormatDate(Instant));
    }

    [Fact]
    public void FormatTooltip_BringsTheLongDate()
    {
        string tooltip = new ClockFormatter(new CultureInfo("pt-BR")).FormatTooltip(Instant);

        Assert.Contains("2026", tooltip, StringComparison.Ordinal);
        Assert.Contains("21", tooltip, StringComparison.Ordinal);
        Assert.NotEqual("21/08/2026", tooltip);
    }

    [Fact]
    public void TimeUntilNextMinute_SubtractsSecondsAndMilliseconds()
    {
        var formatter = new ClockFormatter(CultureInfo.InvariantCulture);

        TimeSpan remaining = formatter.TimeUntilNextMinute(Instant);

        // 42.300s elapsed in the minute -> 17.700s to go.
        Assert.Equal(TimeSpan.FromMilliseconds(17700), remaining);
    }

    [Fact]
    public void TimeUntilNextMinute_OnTheExactMinute_WaitsAWholeMinute()
    {
        var formatter = new ClockFormatter(CultureInfo.InvariantCulture);
        var exact = new DateTimeOffset(2026, 8, 21, 21, 5, 0, 0, TimeSpan.FromHours(-3));

        Assert.Equal(TimeSpan.FromMinutes(1), formatter.TimeUntilNextMinute(exact));
    }

    [Fact]
    public void TimeUntilNextMinute_NeverReturnsZeroOrNegative()
    {
        var formatter = new ClockFormatter(CultureInfo.InvariantCulture);

        // A zero interval would make the timer fire in a loop and burn CPU.
        for (int second = 0; second < 60; second++)
        {
            var instant = new DateTimeOffset(2026, 8, 21, 21, 5, second, TimeSpan.FromHours(-3));
            Assert.True(formatter.TimeUntilNextMinute(instant) > TimeSpan.Zero);
        }
    }
}
