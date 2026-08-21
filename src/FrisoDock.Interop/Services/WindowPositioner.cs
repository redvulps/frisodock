using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Positions windows in physical pixels. That is all (SRP).
/// </summary>
public sealed class WindowPositioner : IWindowPositioner
{
    public void SetBounds(nint windowHandle, PixelRect bounds, bool topMost)
    {
        if (windowHandle == 0)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            windowHandle,
            topMost ? NativeConstants.HWND_TOPMOST : NativeConstants.HWND_NOTOPMOST,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            NativeConstants.SWP_NOACTIVATE | NativeConstants.SWP_SHOWWINDOW);
    }

    public void BringToTop(nint windowHandle)
    {
        if (windowHandle == 0)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            windowHandle,
            NativeConstants.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOACTIVATE);
    }
}
