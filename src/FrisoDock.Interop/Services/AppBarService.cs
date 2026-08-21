using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Registers the dock window as a shell appbar, reserving space at the edge. That is all (SRP).
///
/// Without it, a maximized window would cover the dock. The protocol is: ABM_NEW registers,
/// ABM_QUERYPOS asks the shell where the band fits (it adjusts so as not to collide with other
/// appbars), ABM_SETPOS confirms. ABM_REMOVE gives the space back.
/// </summary>
public sealed class AppBarService : IAppBarService, IDisposable
{
    private nint _windowHandle;
    private uint _callbackMessage;

    public bool IsRegistered { get; private set; }

    public void Register(nint windowHandle, uint callbackMessage)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("Handle de janela inválido.", nameof(windowHandle));
        }

        if (IsRegistered)
        {
            return;
        }

        _windowHandle = windowHandle;
        _callbackMessage = callbackMessage;

        APPBARDATA data = CreateAppBarData();
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_NEW, ref data);

        IsRegistered = true;
    }

    public PixelRect SetPosition(DockEdge edge, PixelRect desired)
    {
        if (!IsRegistered)
        {
            return desired;
        }

        APPBARDATA data = CreateAppBarData();
        data.uEdge = ToNativeEdge(edge);
        data.rc = RECT.FromPixelRect(desired);

        // The shell may shrink the band if there is already another appbar on the same edge.
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_QUERYPOS, ref data);
        data.rc = ClampToRequestedThickness(edge, data.rc, desired);

        NativeMethods.SHAppBarMessage(NativeConstants.ABM_SETPOS, ref data);

        return data.rc.ToPixelRect();
    }

    public void Unregister()
    {
        if (!IsRegistered)
        {
            return;
        }

        APPBARDATA data = CreateAppBarData();
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_REMOVE, ref data);

        IsRegistered = false;
        _windowHandle = 0;
    }

    public bool TryHandleNotification(uint message, nint wParam, out AppBarNotification notification)
    {
        notification = default;

        if (!IsRegistered || message != _callbackMessage)
        {
            return false;
        }

        switch ((int)wParam)
        {
            case NativeConstants.ABN_STATECHANGE:
                notification = AppBarNotification.StateChange;
                return true;

            case NativeConstants.ABN_POSCHANGED:
                notification = AppBarNotification.PositionChanged;
                return true;

            case NativeConstants.ABN_FULLSCREENAPP:
                notification = AppBarNotification.FullScreenApp;
                return true;

            case NativeConstants.ABN_WINDOWARRANGE:
                notification = AppBarNotification.WindowArrange;
                return true;

            default:
                return false;
        }
    }

    public void Dispose()
    {
        Unregister();
    }

    /// <summary>
    /// ABM_QUERYPOS returns the slid edge, not the final thickness. We reapply the requested
    /// thickness from the approved position — it is the step the appbar documentation requires.
    /// </summary>
    private static RECT ClampToRequestedThickness(DockEdge edge, RECT approved, PixelRect desired)
    {
        RECT result = approved;

        switch (edge)
        {
            case DockEdge.Bottom:
                result.Top = result.Bottom - desired.Height;
                break;

            case DockEdge.Top:
                result.Bottom = result.Top + desired.Height;
                break;

            case DockEdge.Left:
                result.Right = result.Left + desired.Width;
                break;

            case DockEdge.Right:
                result.Left = result.Right - desired.Width;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida.");
        }

        return result;
    }

    private static uint ToNativeEdge(DockEdge edge)
    {
        return edge switch
        {
            DockEdge.Left => NativeConstants.ABE_LEFT,
            DockEdge.Top => NativeConstants.ABE_TOP,
            DockEdge.Right => NativeConstants.ABE_RIGHT,
            DockEdge.Bottom => NativeConstants.ABE_BOTTOM,
            _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Borda de dock desconhecida."),
        };
    }

    private APPBARDATA CreateAppBarData()
    {
        return new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd = _windowHandle,
            uCallbackMessage = _callbackMessage,
        };
    }
}
