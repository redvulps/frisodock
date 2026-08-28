namespace FrisoDock.Core.Models;

/// <summary>One cell of the calendar grid.</summary>
/// <param name="Number">Day of the month, as shown in the cell.</param>
/// <param name="IsCurrentMonth">False for the leading and trailing days of the neighbouring months, which are dimmed.</param>
/// <param name="IsToday">True only for today, and only while the displayed month is the current one.</param>
public sealed record CalendarDay(int Number, bool IsCurrentMonth, bool IsToday);

/// <summary>
/// A month ready to be displayed: caption, weekday letters and the 42-cell grid.
///
/// Always six weeks, like the native flyout: a fixed grid keeps the panel height stable
/// while the user navigates between months.
/// </summary>
/// <param name="Caption">Month and year in the user's culture, e.g. "agosto de 2026".</param>
/// <param name="DayHeaders">The seven weekday letters, starting on the culture's first day of the week.</param>
/// <param name="Days">The 42 cells, row by row.</param>
public sealed record CalendarMonth(
    string Caption,
    IReadOnlyList<string> DayHeaders,
    IReadOnlyList<CalendarDay> Days);
