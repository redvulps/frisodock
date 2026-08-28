using System.Globalization;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The calendar grid. The culture is pinned in the tests so the result does not depend on the
/// machine running them. The pt-BR expectations come from the native Windows 11 flyout,
/// captured with this exact month on screen.
/// </summary>
public sealed class CalendarMonthBuilderTests
{
    private static readonly DateOnly Today = new(2026, 8, 28);

    [Fact]
    public void FormatHeader_ReproducesTheNativeHeader_WithoutTheYear()
    {
        var instant = new DateTimeOffset(2026, 8, 28, 9, 30, 0, TimeSpan.FromHours(-3));

        Assert.Equal(
            "sexta-feira, 28 de agosto",
            new CalendarMonthBuilder(new CultureInfo("pt-BR")).FormatHeader(instant));
        Assert.Equal(
            "Friday, August 28",
            new CalendarMonthBuilder(new CultureInfo("en-US")).FormatHeader(instant));
    }

    [Fact]
    public void Build_CaptionFollowsTheCultureYearMonthPattern()
    {
        CalendarMonth month = BuildAugust("pt-BR");

        Assert.Equal("agosto de 2026", month.Caption);
    }

    [Fact]
    public void Build_DayHeadersStartOnTheCultureFirstDayOfWeek()
    {
        // pt-BR starts on Sunday: D S T Q Q S S, as the native flyout shows.
        Assert.Equal(
            new[] { "D", "S", "T", "Q", "Q", "S", "S" },
            BuildAugust("pt-BR").DayHeaders);

        // de-DE starts on Monday, so the letters rotate.
        CalendarMonth german = BuildAugust("de-DE");
        Assert.Equal(new DateTimeFormatInfo().ShortestDayNames.Length, german.DayHeaders.Count);
        Assert.Equal(new CultureInfo("de-DE").DateTimeFormat.ShortestDayNames[1], german.DayHeaders[0]);
    }

    [Fact]
    public void Build_AlwaysProducesSixWeeks()
    {
        Assert.Equal(42, BuildAugust("pt-BR").Days.Count);
    }

    [Fact]
    public void Build_LeadingCellsComeFromThePreviousMonth_Dimmed()
    {
        // August 2026 starts on a Saturday; with the week starting on Sunday the first row is
        // July 26 to August 1, exactly as the native flyout fills it.
        CalendarMonth month = BuildAugust("pt-BR");

        Assert.Equal(26, month.Days[0].Number);
        Assert.False(month.Days[0].IsCurrentMonth);
        Assert.Equal(1, month.Days[6].Number);
        Assert.True(month.Days[6].IsCurrentMonth);
    }

    [Fact]
    public void Build_TrailingCellsComeFromTheNextMonth_Dimmed()
    {
        CalendarMonth month = BuildAugust("pt-BR");

        Assert.Equal(5, month.Days[41].Number);
        Assert.False(month.Days[41].IsCurrentMonth);
    }

    [Fact]
    public void Build_MarksTodayOnce_AndOnlyInTheCurrentMonth()
    {
        CalendarMonth current = BuildAugust("pt-BR");
        Assert.Single(current.Days, day => day.IsToday);
        Assert.Equal(28, Assert.Single(current.Days, day => day.IsToday).Number);

        // Navigating to another month leaves no circle behind: September has no "today".
        CalendarMonth next = new CalendarMonthBuilder(new CultureInfo("pt-BR"))
            .Build(new DateOnly(2026, 9, 1), Today);
        Assert.DoesNotContain(next.Days, day => day.IsToday);
    }

    [Fact]
    public void Build_MonthStartingOnTheWeekStart_HasNoLeadingCells()
    {
        // March 2026 starts on a Sunday, which is pt-BR's first day of the week: the grid
        // opens on the 1st and the six fixed weeks run into April.
        CalendarMonth month = new CalendarMonthBuilder(new CultureInfo("pt-BR"))
            .Build(new DateOnly(2026, 3, 1), Today);

        Assert.Equal(1, month.Days[0].Number);
        Assert.True(month.Days[0].IsCurrentMonth);
        Assert.Equal(42, month.Days.Count);
        Assert.False(month.Days[41].IsCurrentMonth);
    }

    [Fact]
    public void Build_LeapFebruary_KeepsTheTwentyNinth()
    {
        CalendarMonth month = new CalendarMonthBuilder(new CultureInfo("pt-BR"))
            .Build(new DateOnly(2028, 2, 1), Today);

        Assert.Contains(month.Days, day => day.Number == 29 && day.IsCurrentMonth);
    }

    [Fact]
    public void Build_AcceptsAnyDayOfTheMonth_AsTheMonthAnchor()
    {
        // The caller navigates by adding months to a date; the builder normalises to the 1st.
        CalendarMonth fromFirst = BuildAugust("pt-BR");
        CalendarMonth fromMiddle = new CalendarMonthBuilder(new CultureInfo("pt-BR"))
            .Build(new DateOnly(2026, 8, 15), Today);

        // Records holding lists compare by reference, so the comparison is element-wise.
        Assert.Equal(fromFirst.Caption, fromMiddle.Caption);
        Assert.Equal(fromFirst.Days, fromMiddle.Days);
    }

    private static CalendarMonth BuildAugust(string culture)
    {
        return new CalendarMonthBuilder(new CultureInfo(culture)).Build(new DateOnly(2026, 8, 1), Today);
    }

    [Fact]
    public void DefaultConstructor_FollowsTheCultureOfTheMoment()
    {
        // The region can change with the dock running (RegionalFormatService): the culture is
        // read per call, not captured at construction — the week restarts on the new first day.
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            var builder = new CalendarMonthBuilder();

            CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
            Assert.Equal("D", builder.Build(new DateOnly(2026, 8, 1), Today).DayHeaders[0]);

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal(
                new CultureInfo("de-DE").DateTimeFormat.ShortestDayNames[1],
                builder.Build(new DateOnly(2026, 8, 1), Today).DayHeaders[0]);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
