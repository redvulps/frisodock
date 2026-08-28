using System.Globalization;
using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// Builds the month grid the calendar flyout shows. A pure function, no UI and no Win32,
/// therefore testable.
///
/// Everything comes from the user's culture, not from hand-written patterns: which day starts
/// the week, the weekday letters and the caption format all differ between cultures, and
/// writing them by hand would impose the developer's taste.
/// </summary>
public sealed class CalendarMonthBuilder
{
    /// <summary>
    /// Six weeks, like the native flyout. A fixed grid keeps the panel height stable while
    /// the user navigates: a month spanning four rows and one spanning six would otherwise
    /// resize the window under the cursor.
    /// </summary>
    private const int WeeksShown = 6;
    private const int DaysPerWeek = 7;

    /// <summary>Fixed culture, for tests; null follows the OS.</summary>
    private readonly CultureInfo? _culture;

    /// <summary>Follows the user's culture of the moment.</summary>
    public CalendarMonthBuilder()
    {
    }

    public CalendarMonthBuilder(CultureInfo culture)
    {
        _culture = culture;
    }

    /// <summary>
    /// Read on every call, not captured at construction: it is what lets a regional format
    /// change apply with the dock running, once the .NET culture cache is cleared
    /// (RegionalFormatService). A captured culture would survive the cache clear.
    /// </summary>
    private CultureInfo Culture => _culture ?? CultureInfo.CurrentCulture;

    /// <summary>
    /// Header of the flyout: weekday plus day and month, without the year — the year already
    /// lives in the month caption. "dddd" and the culture's month-day pattern reproduce the
    /// native header in any culture: "sexta-feira, 28 de agosto" here, "Friday, August 28"
    /// in en-US.
    /// </summary>
    public string FormatHeader(DateTimeOffset instant)
    {
        CultureInfo culture = Culture;

        return instant.ToString("dddd, " + culture.DateTimeFormat.MonthDayPattern, culture);
    }

    /// <summary>Builds the grid of the month containing <paramref name="month"/>.</summary>
    public CalendarMonth Build(DateOnly month, DateOnly today)
    {
        DateOnly first = new(month.Year, month.Month, 1);

        // One read for the whole grid: a culture change in the middle of a build would mix
        // a caption from one region with day letters from another.
        CultureInfo culture = Culture;

        string caption = first.ToString(culture.DateTimeFormat.YearMonthPattern, culture);

        // The grid starts on the culture's first day of the week, on or before the 1st: the
        // leading cells belong to the previous month and come dimmed, exactly like the native
        // flyout fills its first row.
        DayOfWeek weekStart = culture.DateTimeFormat.FirstDayOfWeek;
        int lead = (((int)first.DayOfWeek - (int)weekStart) + DaysPerWeek) % DaysPerWeek;
        DateOnly cursor = first.AddDays(-lead);

        var days = new CalendarDay[WeeksShown * DaysPerWeek];
        for (int i = 0; i < days.Length; i++)
        {
            days[i] = new CalendarDay(
                cursor.Day,
                cursor.Month == first.Month && cursor.Year == first.Year,
                cursor == today);
            cursor = cursor.AddDays(1);
        }

        string[] headers = new string[DaysPerWeek];
        for (int i = 0; i < DaysPerWeek; i++)
        {
            headers[i] = culture.DateTimeFormat.ShortestDayNames[(((int)weekStart) + i) % DaysPerWeek];
        }

        return new CalendarMonth(caption, headers, days);
    }
}
