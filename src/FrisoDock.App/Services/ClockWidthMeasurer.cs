using System.Globalization;
using System.Windows;
using System.Windows.Media;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Measures the clock in the font it will be drawn with and returns the adjusted metrics. That is all (SRP).
///
/// It exists because the clock widths were hand-written constants, and a text width constant
/// is a measure that depends on three things we do not choose: the user's culture
/// (the time and date format), the system font and its size. The recorded value fitted
/// this machine; in a 12-hour culture, or with another system font, it would be
/// wrong — wasting space in the dock or clipping the text.
///
/// The width enters the panel length and, on a vertical dock, its thickness. Measuring for
/// real is what made the vertical dock stop thickening with seconds on: the recorded
/// value said 70 px, and "09:34:23" on this machine measures 48.
/// </summary>
public static class ClockWidthMeasurer
{
    // Enough samples to catch the widest digit in each place, and the widest months and days
    // where the culture spells the month out. Every combination would be work
    // thrown away: what changes the width is the glyph, not the date.
    private static readonly int[] SampleHours = [0, 1, 8, 10, 11, 20, 22, 23];
    private static readonly int[] SampleMinutes = [0, 8, 11, 48, 58];
    private static readonly int[] SampleDays = [1, 8, 11, 28, 30];

    /// <summary>
    /// The same metrics, with the clock widths swapped for the ones the real font gives.
    /// </summary>
    public static DockMetrics Measure(DockMetrics metrics, ClockFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(formatter);

        // Measuring is tuning, not a requirement: this runs before any window exists, and an exception
        // here would kill the dock at startup. Without the measurement, the metrics that came in stand.
        try
        {
            Typeface typeface = ResolveTypeface();

            double date = 0;
            double time = 0;
            double timeWithSeconds = 0;

            foreach (DateTimeOffset sample in EnumerateSamples())
            {
                date = Math.Max(date, Measure(formatter.FormatDate(sample), metrics.ClockDateFontSize, typeface));
                time = Math.Max(time, Measure(formatter.FormatTime(sample), metrics.ClockTimeFontSize, typeface));
                timeWithSeconds = Math.Max(
                    timeWithSeconds,
                    Measure(formatter.FormatTime(sample, includeSeconds: true), metrics.ClockTimeFontSize, typeface));
            }

            if (date <= 0 || time <= 0 || timeWithSeconds <= 0)
            {
                return metrics;
            }

            // The recorded measure is that of the widest line, which is what the clock block has to
            // fit — sometimes the date, sometimes the time, depending on the culture.
            return metrics with
            {
                ClockContentWidth = Round(Math.Max(date, time)),
                ClockContentWidthWithSeconds = Round(Math.Max(date, timeWithSeconds)),
            };
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return metrics;
        }
    }

    /// <summary>
    /// The font WPF will use. The clock text blocks declare no family at all,
    /// so they inherit the system one — the same this method resolves.
    /// </summary>
    private static Typeface ResolveTypeface()
    {
        return new Typeface(
            SystemFonts.MessageFontFamily,
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
    }

    private static double Measure(string text, int fontSize, Typeface typeface)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.White,
            1.0);

        return formatted.Width;
    }

    /// <summary>
    /// Rounds, instead of rounding up.
    ///
    /// Rounding up, a date measuring exactly 56 px would become 57 and the vertical panel would be
    /// 1 px thicker than the horizontal one for no reason. The fraction lost the other way does not
    /// show: the text is centred and the panel padding takes up the slack.
    /// </summary>
    private static int Round(double width)
    {
        return (int)Math.Round(width, MidpointRounding.AwayFromZero);
    }

    private static IEnumerable<DateTimeOffset> EnumerateSamples()
    {
        const int Year = 2026;

        foreach (int month in Enumerable.Range(1, 12))
        {
            // February has no 30th, and an invalid date here would kill the dock at startup —
            // before any window appears, which is the worst place for an exception.
            int lastDay = DateTime.DaysInMonth(Year, month);

            foreach (int day in SampleDays)
            {
                foreach (int hour in SampleHours)
                {
                    foreach (int minute in SampleMinutes)
                    {
                        yield return new DateTimeOffset(
                            Year,
                            month,
                            Math.Min(day, lastDay),
                            hour,
                            minute,
                            minute,
                            TimeSpan.Zero);
                    }
                }
            }
        }
    }
}
