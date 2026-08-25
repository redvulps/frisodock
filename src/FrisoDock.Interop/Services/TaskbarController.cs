using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Hides and restores the native taskbar. Nothing beyond that (SRP).
///
/// Hiding is <c>ShowWindow</c> on the taskbar windows — the primary one and the secondary ones on
/// each monitor. The band the taskbar reserves on screen does not go with it: measured, the work
/// area stays 1920x1032 with the window invisible, and the only way to recover it is autohide
/// (<c>ABM_SETSTATE</c>), which gives it back whole (1920x1080).
///
/// Autohide is not always applied because it has a price: on that edge Explorer brings the bar
/// back when the cursor arrives. Hence the rule being the dock's edge — with both on the same edge,
/// the dock window covers the band and discounting it is enough (<see cref="GetReservedBand"/>); on any
/// other edge nobody covers it and it would become a hole on screen, so releasing it is worth it.
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

    public void Hide(DockEdge dockEdge)
    {
        // With the turn yielded, Explorer is once again the first Shell_TrayWnd — which is what this
        // service looks for. Without that, with the tray hosted here, it would find the dock's own
        // window and hide the wrong one.
        using IDisposable priority = _trayPriority.Yield();

        if (ReadTaskbarEdge() is DockEdge taskbarEdge && taskbarEdge != dockEdge)
        {
            ReleaseReservedBand();
        }
        else
        {
            RestoreReleasedBand();
        }

        ApplyVisibility(NativeConstants.SW_HIDE);

        IsHidden = true;
    }

    public void Restore()
    {
        using IDisposable priority = _trayPriority.Yield();

        ApplyVisibility(NativeConstants.SW_SHOW);

        // A stored value means the band was released — in this run or in one that never
        // got to restore. Restoring and erasing leaves the machine as the user had it.
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

    /// <summary>
    /// Puts the taskbar into autohide, storing the state the user had beforehand.
    ///
    /// The state goes to disk because it has to survive an abnormal shutdown: without that the next
    /// instance would read the autohide left behind as the user's preference and
    /// would restore to autohide forever after.
    /// </summary>
    private void ReleaseReservedBand()
    {
        int state = GetAppBarState();

        // Already in autohide by the user's choice: there is no band to release and no state of ours
        // to store. Overwriting here would erase the true state written by a previous run
        // that did not restore.
        if ((state & NativeConstants.ABS_AUTOHIDE) != 0)
        {
            return;
        }

        _stateStore.Save(state);
        SetAppBarState(state | NativeConstants.ABS_AUTOHIDE);
    }

    /// <summary>
    /// Undoes the autohide we turned on, when the dock moves onto the taskbar's edge.
    /// Without this, moving the dock from the side to the bottom edge would leave the bar reappearing
    /// on top of it.
    /// </summary>
    private void RestoreReleasedBand()
    {
        if (_stateStore.Load() is not int original)
        {
            return;
        }

        SetAppBarState(original);
        _stateStore.Clear();
    }

    /// <summary>
    /// Edge the taskbar is anchored to, told by the shell itself:
    /// <c>ABM_GETTASKBARPOS</c> fills in <c>uEdge</c>, which spares us deducing it from the shape of the
    /// rectangle.
    ///
    /// It does not yield the turn because it already runs inside a yielded scope — yielding again would
    /// restore the position on leaving the inner scope, with the outer one thinking it is still yielded.
    /// </summary>
    private static DockEdge? ReadTaskbarEdge()
    {
        APPBARDATA data = CreateAppBarData();

        if (NativeMethods.SHAppBarMessage(NativeConstants.ABM_GETTASKBARPOS, ref data) == 0)
        {
            return null;
        }

        return data.uEdge switch
        {
            NativeConstants.ABE_LEFT => DockEdge.Left,
            NativeConstants.ABE_TOP => DockEdge.Top,
            NativeConstants.ABE_RIGHT => DockEdge.Right,
            NativeConstants.ABE_BOTTOM => DockEdge.Bottom,
            _ => null,
        };
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
