using System.Globalization;

namespace FrisoDock.Core.Services;

/// <summary>
/// Formats the current instant into the texts the dock shows. A pure function, no UI and no Win32,
/// therefore testable.
///
/// It follows the user's culture on purpose: whoever uses 12 hours expects "9:05 PM", whoever uses 24
/// expects "21:05". Writing the format by hand would impose the developer's taste.
/// </summary>
public sealed class ClockFormatter
{
    /// <summary>Fixed culture, for tests; null follows the OS.</summary>
    private readonly CultureInfo? _culture;

    /// <summary>Follows the user's culture of the moment.</summary>
    public ClockFormatter()
    {
    }

    public ClockFormatter(CultureInfo culture)
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
    /// Time, with or without the seconds.
    ///
    /// Both formats come from the culture, not from a hand-written pattern: it is the culture that
    /// decides where the seconds go and whether a "PM" is left at the end.
    /// </summary>
    public string FormatTime(DateTimeOffset instant, bool includeSeconds = false)
    {
        string pattern = includeSeconds
            ? Culture.DateTimeFormat.LongTimePattern
            : Culture.DateTimeFormat.ShortTimePattern;

        return instant.ToString(pattern, Culture);
    }

    /// <summary>Short date.</summary>
    public string FormatDate(DateTimeOffset instant)
    {
        return instant.ToString(Culture.DateTimeFormat.ShortDatePattern, Culture);
    }

    /// <summary>Long date, for the tooltip that appears on hover.</summary>
    public string FormatTooltip(DateTimeOffset instant)
    {
        return instant.ToString(Culture.DateTimeFormat.LongDatePattern, Culture);
    }

    /// <summary>
    /// How long until the clock text changes.
    ///
    /// With no seconds showing, waking up every second would be waste. In either
    /// case, waking at a fixed interval would delay the rollover by up to a whole period: aligning the
    /// next tick to the rollover hits the time with no extra ticks.
    /// </summary>
    public TimeSpan TimeUntilNextTick(DateTimeOffset instant, bool includeSeconds = false)
    {
        TimeSpan period = includeSeconds ? TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(1);

        TimeSpan elapsed = TimeSpan.FromMilliseconds(instant.Millisecond);
        if (!includeSeconds)
        {
            elapsed += TimeSpan.FromSeconds(instant.Second);
        }

        TimeSpan remaining = period - elapsed;

        if (remaining <= TimeSpan.Zero)
        {
            return period;
        }

        return remaining;
    }
}
