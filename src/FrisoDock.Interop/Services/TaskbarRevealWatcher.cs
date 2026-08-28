using System.Text;
using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reports when the shell reshows the taskbar. That is all (SRP).
///
/// It exists because hiding the taskbar is not a state the shell respects. With it in autohide —
/// which is how the dock gives the reserved band back when it is not on that edge —, Explorer
/// reshows the window on its own and parks it off screen; then it brings it back whole
/// when the Start menu opens or the cursor touches that edge.
///
/// **Measured, with the dock on the left and the taskbar at the bottom:** at rest it shows at most 2 px,
/// which is the autohide sliver and nobody sees. With the Start menu open, it shows the full 48 px in
/// 30 of 30 samples. Re-hiding on every reshow, it falls to 0 px — and the shell only reshows
/// **once** per opening, so there is no fight and no repeated flicker.
///
/// The hook is an out-of-process WinEvent, the same family the window enumerator already uses: nothing
/// is injected into another process.
/// </summary>
public sealed class TaskbarRevealWatcher : ITaskbarRevealWatcher
{
    /// <summary>
    /// Kept in a field out of obligation: Win32 stores only the pointer, and a collected delegate
    /// kills the process on the first event.
    /// </summary>
    private readonly NativeTypes.WinEventProc _callback;

    private nint _hook;
    private bool _disposed;

    public TaskbarRevealWatcher()
    {
        _callback = OnWinEvent;
    }

    public event EventHandler? TaskbarRevealed;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hook != 0)
        {
            return;
        }

        // EVENT_OBJECT_SHOW only, and not the whole range: reshowing is the only event that
        // matters here, and the class filter discards the rest at no read cost.
        _hook = NativeMethods.SetWinEventHook(
            NativeConstants.EVENT_OBJECT_SHOW,
            NativeConstants.EVENT_OBJECT_SHOW,
            0,
            _callback,
            0,
            0,
            NativeConstants.WINEVENT_OUTOFCONTEXT | NativeConstants.WINEVENT_SKIPOWNPROCESS);
    }

    public void Stop()
    {
        if (_hook == 0)
        {
            return;
        }

        NativeMethods.UnhookWinEvent(_hook);
        _hook = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private void OnWinEvent(
        nint hookHandle,
        uint eventType,
        nint windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        // The window itself, and not a child of it: the taskbar buttons appear and disappear all the
        // time, and none of them is the bar.
        if (objectId != NativeConstants.OBJID_WINDOW || childId != 0 || windowHandle == 0)
        {
            return;
        }

        if (!IsTaskbarWindow(windowHandle))
        {
            return;
        }

        TaskbarRevealed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsTaskbarWindow(nint windowHandle)
    {
        var buffer = new StringBuilder(64);

        if (NativeMethods.GetClassName(windowHandle, buffer, buffer.Capacity) == 0)
        {
            return false;
        }

        string className = buffer.ToString();

        return className == NativeConstants.TaskbarPrimaryClass
            || className == NativeConstants.TaskbarSecondaryClass;
    }
}
