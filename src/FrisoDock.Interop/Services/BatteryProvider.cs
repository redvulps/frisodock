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
            IsCharging: (status.BatteryFlag & NativeConstants.BATTERY_FLAG_CHARGING) != 0,
            IsSaverOn: (status.SystemStatusFlag & NativeConstants.SYSTEM_STATUS_FLAG_POWER_SAVER) != 0);
    }
}
