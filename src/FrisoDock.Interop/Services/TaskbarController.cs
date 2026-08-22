using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Hides and restores the native taskbar. Nothing beyond that (SRP).
///
/// Hiding is <c>ShowWindow</c> on the taskbar windows — the primary one and the secondary ones on
/// monitor — and nothing else. Autohide (<c>ABM_SETSTATE</c>) was used for a while, because it is the
/// only way to give back the band the taskbar reserves; but autohide means Explorer
/// shows the bar again as soon as the cursor touches the edge, on top of the dock. Really hiding
/// is worth more than the band: what discounts the band from the dock's reservation is
/// <see cref="GetReservedBand"/>, and the result on screen is the same.
///
/// It requires no administrator privilege: Explorer runs at the same integrity level
/// as the user.
/// </summary>
public sealed class TaskbarController : ITaskbarController
{
    private readonly ITaskbarStateStore _stateStore;
    private readonly IShellTrayPriority _trayPriority;

    public TaskbarController(ITaskbarStateStore stateStore, IShellTrayPriority trayPriority)
    {
        _stateStore = stateStore;
        _trayPriority = trayPriority;
    }

    public bool IsHidden { get; private set; }

    public void Hide()
    {
        // With the turn yielded, Explorer is once again the first Shell_TrayWnd — which is what this
        // service looks for. Without that, with the tray hosted here, it would find the dock's own
        // window and hide the wrong one.
        using IDisposable priority = _trayPriority.Yield();

        ApplyVisibility(NativeConstants.SW_HIDE);

        IsHidden = true;
    }

    public void Restore()
    {
        using IDisposable priority = _trayPriority.Yield();

        ApplyVisibility(NativeConstants.SW_SHOW);

        // A stored value comes from an earlier version, which put the taskbar into autohide to
        // release the band. Restoring and erasing leaves the machine as the user had it.
        if (_stateStore.Load() is int pending)
        {
            SetAppBarState(pending);
        }

        _stateStore.Clear();
        IsHidden = false;
    }

    /// <inheritdoc />
    public PixelRect? GetReservedBand()
    {
        using IDisposable priority = _trayPriority.Yield();

        // Autohide is the user's choice in the Windows settings, and in that mode the taskbar
        // reserves nothing — there is nothing to discount.
        if ((GetAppBarState() & NativeConstants.ABS_AUTOHIDE) != 0)
        {
            return null;
        }

        APPBARDATA data = CreateAppBarData();
        if (NativeMethods.SHAppBarMessage(NativeConstants.ABM_GETTASKBARPOS, ref data) == 0)
        {
            return null;
        }

        return data.rc.ToPixelRect();
    }

    private static void ApplyVisibility(int showCommand)
    {
        foreach (nint taskbar in EnumerateTaskbarWindows())
        {
            NativeMethods.ShowWindow(taskbar, showCommand);
        }
    }

    /// <summary>
    /// The primary taskbar plus the secondary ones (one per additional monitor). Explorer creates and
    /// destroys the secondary ones as monitors come and go, so the list is read on every use.
    /// </summary>
    private static IEnumerable<nint> EnumerateTaskbarWindows()
    {
        nint primary = NativeMethods.FindWindow(NativeConstants.TaskbarPrimaryClass, null);
        if (primary != 0)
        {
            yield return primary;
        }

        nint secondary = 0;
        while (true)
        {
            secondary = NativeMethods.FindWindowEx(0, secondary, NativeConstants.TaskbarSecondaryClass, null);
            if (secondary == 0)
            {
                yield break;
            }

            yield return secondary;
        }
    }

    private static int GetAppBarState()
    {
        APPBARDATA data = CreateAppBarData();
        return (int)NativeMethods.SHAppBarMessage(NativeConstants.ABM_GETSTATE, ref data);
    }

    private static void SetAppBarState(int state)
    {
        APPBARDATA data = CreateAppBarData();
        data.lParam = state;
        NativeMethods.SHAppBarMessage(NativeConstants.ABM_SETSTATE, ref data);
    }

    private static APPBARDATA CreateAppBarData()
    {
        return new APPBARDATA
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<APPBARDATA>(),
            hWnd = NativeMethods.FindWindow(NativeConstants.TaskbarPrimaryClass, null),
        };
    }
}
