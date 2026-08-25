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
    public void FormatTimeStacked_BreaksAtTheCultureSeparator()
    {
        // On the vertical dock the horizontal time does not fit the panel thickness; stacked, each
        // line has two characters.
        var formatter = new ClockFormatter(new CultureInfo("pt-BR"));

        Assert.Equal("21\n05", formatter.FormatTimeStacked(Instant));
        Assert.Equal("21\n05\n42", formatter.FormatTimeStacked(Instant, includeSeconds: true));
    }

    [Fact]
    public void FormatTimeStacked_GivesALineToTheTwelveHourSuffix()
    {
        // The "PM" comes separated by a space — and in ICU cultures by a hard space, which the
        // whitespace split also catches.
        var formatter = new ClockFormatter(new CultureInfo("en-US"));

        Assert.Equal("9\n05\nPM", formatter.FormatTimeStacked(Instant));
        Assert.Equal("9\n05\n42\nPM", formatter.FormatTimeStacked(Instant, includeSeconds: true));
    }

    [Fact]
    public void FormatTimeStacked_LeavesNoEmptyLine()
    {
        // An empty line would become a hole in the middle of the clock, and the block would grow for nothing.
        foreach (string name in new[] { "pt-BR", "en-US", "de-DE", "ja-JP" })
        {
            var formatter = new ClockFormatter(new CultureInfo(name));

            foreach (bool seconds in new[] { false, true })
            {
                string[] lines = formatter.FormatTimeStacked(Instant, seconds).Split('\n');

                Assert.All(lines, line => Assert.False(string.IsNullOrWhiteSpace(line)));
                Assert.NotEmpty(lines);
            }
        }
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
    public void FormatTime_WithSeconds_InATwentyFourHourCulture()
    {
        var formatter = new ClockFormatter(new CultureInfo("pt-BR"));

        Assert.Equal("21:05:42", formatter.FormatTime(Instant, includeSeconds: true));
    }

    [Fact]
    public void FormatTime_WithSeconds_InATwelveHourCulture()
    {
        // The suffix still comes from the culture: whoever shows "PM" keeps showing it with seconds.
        var formatter = new ClockFormatter(new CultureInfo("en-US"));

        Assert.Equal("9:05:42 PM", formatter.FormatTime(Instant, includeSeconds: true));
    }

    [Fact]
    public void TimeUntilNextTick_WithSeconds_WaitsOnlyWhatIsLeftOfTheSecond()
    {
        var formatter = new ClockFormatter(new CultureInfo("pt-BR"));
        var instant = new DateTimeOffset(2026, 8, 21, 21, 5, 33, 400, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromMilliseconds(600), formatter.TimeUntilNextTick(instant, includeSeconds: true));
    }

    [Fact]
    public void TimeUntilNextTick_WithSeconds_OnTheExactSecond_WaitsAWholeSecond()
    {
        var formatter = new ClockFormatter(new CultureInfo("pt-BR"));
        var exact = new DateTimeOffset(2026, 8, 21, 21, 5, 33, 0, TimeSpan.Zero);

        Assert.Equal(TimeSpan.FromSeconds(1), formatter.TimeUntilNextTick(exact, includeSeconds: true));
    }

    [Fact]
    public void TimeUntilNextTick_WithSeconds_NeverExceedsOneSecond()
    {
        // An interval longer than the period would leave the clock skipping seconds.
        var formatter = new ClockFormatter(new CultureInfo("pt-BR"));

        for (int millisecond = 0; millisecond < 1000; millisecond += 50)
        {
            var instant = new DateTimeOffset(2026, 8, 21, 21, 5, 33, millisecond, TimeSpan.Zero);

            Assert.InRange(formatter.TimeUntilNextTick(instant, includeSeconds: true), TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public void TimeUntilNextTick_SubtractsSecondsAndMilliseconds()
    {
        var formatter = new ClockFormatter(CultureInfo.InvariantCulture);

        TimeSpan remaining = formatter.TimeUntilNextTick(Instant);

        // 42.300s elapsed in the minute -> 17.700s to go.
        Assert.Equal(TimeSpan.FromMilliseconds(17700), remaining);
    }

    [Fact]
    public void TimeUntilNextTick_OnTheExactMinute_WaitsAWholeMinute()
    {
        var formatter = new ClockFormatter(CultureInfo.InvariantCulture);
        var exact = new DateTimeOffset(2026, 8, 21, 21, 5, 0, 0, TimeSpan.FromHours(-3));

        Assert.Equal(TimeSpan.FromMinutes(1), formatter.TimeUntilNextTick(exact));
    }

    [Fact]
    public void TimeUntilNextTick_NeverReturnsZeroOrNegative()
    {
        var formatter = new ClockFormatter(CultureInfo.InvariantCulture);

        // A zero interval would make the timer fire in a loop and burn CPU.
        for (int second = 0; second < 60; second++)
        {
            var instant = new DateTimeOffset(2026, 8, 21, 21, 5, second, TimeSpan.FromHours(-3));
            Assert.True(formatter.TimeUntilNextTick(instant) > TimeSpan.Zero);
        }
    }
}
