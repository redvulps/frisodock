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

    /// <summary>Time without the seconds, as the taskbar shows it.</summary>
    public string FormatTime(DateTimeOffset instant)
    {
        return instant.ToString(_culture.DateTimeFormat.ShortTimePattern, _culture);
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
    /// How long until the minute rolls over.
    ///
    /// The clock shows no seconds, so waking every second would be waste; waking every
    /// exact minute would delay the rollover by up to a second. Aligning the next tick to the
    /// minute rollover hits the time and keeps one tick per minute.
    /// </summary>
    public TimeSpan TimeUntilNextMinute(DateTimeOffset instant)
    {
        TimeSpan remaining = TimeSpan.FromMinutes(1)
            - TimeSpan.FromSeconds(instant.Second)
            - TimeSpan.FromMilliseconds(instant.Millisecond);

        if (remaining <= TimeSpan.Zero)
        {
            return TimeSpan.FromMinutes(1);
        }

        return remaining;
    }
}
