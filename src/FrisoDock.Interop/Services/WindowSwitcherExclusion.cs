using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Keeps the window out of Alt+Tab by marking it as a tool window. That is all (SRP).
///
/// The switcher's classic criterion is a different one: it skips owned windows, and WPF already gives a
/// hidden owner to every window with <c>ShowInTaskbar="False"</c>. That is not enough — the Windows 11
/// switcher showed the dock anyway. <c>WS_EX_TOOLWINDOW</c> is what it respects.
/// </summary>
public sealed class WindowSwitcherExclusion : IWindowSwitcherExclusion
{
    public void Exclude(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        nint exStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeConstants.GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(
            windowHandle,
            NativeConstants.GWL_EXSTYLE,
            exStyle | (nint)NativeConstants.WS_EX_TOOLWINDOW);
    }
}
