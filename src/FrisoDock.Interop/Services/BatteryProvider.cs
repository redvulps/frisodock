using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Battery state, through <c>GetSystemPowerStatus</c>. That is all (SRP).
///
/// Battery saver comes in as a read only: Windows reports that it is on, but offers no
/// way to turn it on other than through its own panel.
/// </summary>
public sealed class BatteryProvider : IBatteryProvider
{
    public BatteryStatus GetStatus()
    {
        if (!NativeMethods.GetSystemPowerStatus(out SYSTEM_POWER_STATUS status))
        {
            return BatteryStatus.None;
        }

        bool hasBattery = (status.BatteryFlag & NativeConstants.BATTERY_FLAG_NO_BATTERY) == 0
            && status.BatteryLifePercent != NativeConstants.BATTERY_PERCENT_UNKNOWN;

        if (!hasBattery)
        {
            return BatteryStatus.None;
        }

        return new BatteryStatus(
            HasBattery: true,
            Percent: Math.Clamp((int)status.BatteryLifePercent, 0, 100),
            IsPluggedIn: IsOnAcPower(status),
            IsSaverOn: (status.SystemStatusFlag & NativeConstants.SYSTEM_STATUS_FLAG_POWER_SAVER) != 0);
    }

    /// <summary>
    /// Plugged in, and not charging — which is what the native panel shows.
    ///
    /// The two diverge precisely in the most common case of working plugged in: near the end of the
    /// charge Windows stops charging and clears <c>BATTERY_FLAG_CHARGING</c>, but keeps
    /// <c>ACLineStatus</c> online. Measured on this machine, at 97% with the cable in: the charging
    /// flag said no, and the Windows panel showed the bolt.
    ///
    /// Without the power line — machines that cannot report it —, the charging flag is what
    /// remains: it says less, but it does not invent.
    /// </summary>
    private static bool IsOnAcPower(SYSTEM_POWER_STATUS status)
    {
        if (status.ACLineStatus == NativeConstants.AC_LINE_STATUS_UNKNOWN)
        {
            return (status.BatteryFlag & NativeConstants.BATTERY_FLAG_CHARGING) != 0;
        }

        return status.ACLineStatus == NativeConstants.AC_LINE_STATUS_ONLINE;
    }
}
