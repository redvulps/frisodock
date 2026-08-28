using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>Cursor position through <c>GetCursorPos</c>. That is all (SRP).</summary>
public sealed class CursorProvider : ICursorProvider
{
    public PixelPoint GetPosition()
    {
        if (!NativeMethods.GetCursorPos(out POINT point))
        {
            return default;
        }

        return new PixelPoint(point.X, point.Y);
    }

    public bool IsPrimaryButtonPressed()
    {
        // The physical state (GetAsyncKeyState), not the thread-synchronized one (GetKeyState,
        // which is what WPF's Mouse class reads): the press this exists for was eaten before
        // reaching any of our windows, so the synchronized state never learned about it. The
        // same trap, and the same cure, as the Shift key in the window switcher hook.
        int button = NativeMethods.GetSystemMetrics(NativeConstants.SM_SWAPBUTTON) != 0
            ? NativeConstants.VK_RBUTTON
            : NativeConstants.VK_LBUTTON;

        return (NativeMethods.GetAsyncKeyState(button) & 0x8000) != 0;
    }
}
