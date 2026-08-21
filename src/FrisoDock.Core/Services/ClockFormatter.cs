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
    private readonly CultureInfo _culture;

    public ClockFormatter()
        : this(CultureInfo.CurrentCulture)
    {
    }

    public ClockFormatter(CultureInfo culture)
    {
        _culture = culture;
    }

    /// <summary>
    /// Time, with or without the seconds.
    ///
    /// Both formats come from the culture, not from a hand-written pattern: it is the culture that
    /// decides where the seconds go and whether a "PM" is left at the end.
    /// </summary>
    public string FormatTime(DateTimeOffset instant, bool includeSeconds = false)
    {
        string pattern = includeSeconds
            ? _culture.DateTimeFormat.LongTimePattern
            : _culture.DateTimeFormat.ShortTimePattern;

        return instant.ToString(pattern, _culture);
    }

    /// <summary>Short date.</summary>
    public string FormatDate(DateTimeOffset instant)
    {
        return instant.ToString(_culture.DateTimeFormat.ShortDatePattern, _culture);
    }

    /// <summary>Long date, for the tooltip that appears on hover.</summary>
    public string FormatTooltip(DateTimeOffset instant)
    {
        return instant.ToString(_culture.DateTimeFormat.LongDatePattern, _culture);
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
