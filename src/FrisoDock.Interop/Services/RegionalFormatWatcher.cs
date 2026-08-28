using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Detects regional format changes through the WM_SETTINGCHANGE broadcast. That is all (SRP).
/// </summary>
public sealed class RegionalFormatWatcher : IRegionalFormatWatcher
{
    public event EventHandler? RegionalFormatChanged;

    public bool TryHandle(uint message, nint lParam)
    {
        if (message != NativeConstants.WM_SETTINGCHANGE)
        {
            return false;
        }

        // lParam names the settings section that changed, and filtering by it matters:
        // WM_SETTINGCHANGE also fires for wallpaper, policies, environment variables — reacting
        // to all of them would re-measure the clock on every system tweak. "intl" is the
        // section the Region control panel names when the formats change.
        if (lParam == 0 || !string.Equals(Marshal.PtrToStringUni(lParam), "intl", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        RegionalFormatChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
