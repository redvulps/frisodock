using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Applies the Windows 11 flyout finish through the DWM. That is all (SRP).
///
/// Each attribute is applied independently and the failure of one does not block the others:
/// DWMWA_SYSTEMBACKDROP_TYPE and DWMWA_WINDOW_CORNER_PREFERENCE only exist from
/// Windows 11 22H2 on, and on Windows 10 the calls merely return an error, which is ignored.
/// </summary>
public sealed class DwmWindowBackdrop : IWindowBackdrop
{
    public void ApplyFlyoutAppearance(nint windowHandle, bool darkMode)
    {
        if (windowHandle == 0)
        {
            return;
        }

        SetAttribute(windowHandle, NativeConstants.DWMWA_USE_IMMERSIVE_DARK_MODE, darkMode ? 1 : 0);
        SetAttribute(windowHandle, NativeConstants.DWMWA_WINDOW_CORNER_PREFERENCE, NativeConstants.DWMWCP_ROUND);
        SetAttribute(windowHandle, NativeConstants.DWMWA_SYSTEMBACKDROP_TYPE, NativeConstants.DWMSBT_TRANSIENTWINDOW);
    }

    private static void SetAttribute(nint windowHandle, int attribute, int value)
    {
        int buffer = value;
        NativeMethods.DwmSetWindowAttribute(windowHandle, attribute, ref buffer, sizeof(int));
    }
}
