using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Hides and restores the native taskbar. Nothing beyond that (SRP).
///
/// There are two independent steps, and both matter:
///  1. Putting Explorer's appbar into autohide (ABM_SETSTATE). Merely hiding the window does not
///     give the screen work area back — the space would stay reserved and maximized windows
///     would end up with an empty band at the bottom.
///  2. Hiding the taskbar windows (the primary one and the secondary one on each monitor).
///
/// It requires no administrator privilege: Explorer runs at the same integrity level
/// as the user.
/// </summary>
public sealed class TaskbarController : ITaskbarController
{
    private readonly ITaskbarStateStore _stateStore;
    private readonly IShellTrayPriority _trayPriority;

    private int? _originalAppBarState;

    public TaskbarController(ITaskbarStateStore stateStore, IShellTrayPriority trayPriority)
    {
        _stateStore = stateStore;
        _trayPriority = trayPriority;
    }

    public bool IsHidden { get; private set; }

    public void Hide()
    {
        // With the turn yielded, Explorer is once again the first Shell_TrayWnd — which is what this
        // service looks for, both for the window to hide and for the ABM_SETSTATE target.
        // Without that, with the tray hosted here, it would find the dock's own window.
        using IDisposable priority = _trayPriority.Yield();

        // It stores the original state only the first time, so as not to write "autohide"
        // as if it were the user's preference on a second call.
        _originalAppBarState ??= ReadOriginalState();

        SetAppBarState(NativeConstants.ABS_AUTOHIDE | NativeConstants.ABS_ALWAYSONTOP);
        ApplyVisibility(NativeConstants.SW_HIDE);

        IsHidden = true;
    }

    public void Restore()
    {
        using IDisposable priority = _trayPriority.Yield();

        ApplyVisibility(NativeConstants.SW_SHOW);

        int? state = _originalAppBarState ?? _stateStore.Load();
        if (state is int original)
        {
            SetAppBarState(original);
        }

        _stateStore.Clear();
        _originalAppBarState = null;
        IsHidden = false;
    }

    /// <summary>
    /// State to restore later.
    ///
    /// A pending value on disk means the previous run died without restoring: the
    /// taskbar is already in autohide because of it, and reading the current state would merely store that
    /// autohide as if it were the user's preference. In that case the stored value is the correct one.
    /// </summary>
    private int ReadOriginalState()
    {
        if (_stateStore.Load() is int pending)
        {
            return pending;
        }

        int current = GetAppBarState();
        _stateStore.Save(current);

        return current;
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
