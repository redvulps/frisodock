using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Keeps the window from taking focus on click with <c>WS_EX_NOACTIVATE</c>. That is all (SRP).
///
/// It is the extended style <c>Shell_TrayWnd</c> itself uses. The click still reaches the
/// window — the buttons work, dragging captures the mouse —, but it does not become the
/// foreground one, so it does not take the focus from whoever had it. That is what lets the Start
/// button hide the menu on a second click: with no focus stealing, the Windows key becomes a real
/// toggle again.
/// </summary>
public sealed class WindowActivationPolicy : IWindowActivationPolicy
{
    public void PreventActivation(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        nint exStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeConstants.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeConstants.GWL_EXSTYLE,
            exStyle | (nint)NativeConstants.WS_EX_NOACTIVATE);
    }
}
