using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;
using System.Runtime.InteropServices;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Recognizes Alt+Tab and swallows it, so the dock's switcher appears in place of the native one.
/// That is all (SRP): it does not know what a window is nor what will be shown.
///
/// The low-level keyboard hook is the only path, and the alternatives were measured, not
/// assumed: <c>RegisterHotKey(MOD_ALT, VK_TAB)</c> returns <c>ERROR_HOTKEY_ALREADY_REGISTERED</c>
/// because the combination already belongs to the system, and raw input sees the key but does not
/// consume it, which would let the native switcher appear under ours.
///
/// <c>WH_KEYBOARD_LL</c> is global but not injected: no code of ours enters another
/// process, and Windows is the one calling back here — the same family as the WinEvent hooks the dock
/// already uses to watch windows, and with no administrator required.
/// </summary>
public sealed class WindowSwitcherGesture : IWindowSwitcherGesture, IDisposable
{
    private const int KeyPressedMask = 0x8000;

    /// <summary>How long to wait for the hook thread to finish before moving on.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly object _sync = new();

    // Win32 stores only the delegate's pointer. Without keeping the reference alive, the GC collects it
    // and the process dies on the first key — the same trap as the enumerator's WinEventProc.
    private NativeTypes.LowLevelKeyboardProc? _callback;

    private Thread? _thread;
    private uint _threadId;
    private nint _hook;
    private bool _altDown;
    private bool _sessionActive;
    private bool _disposed;

    public event EventHandler<WindowSwitcherStepEventArgs>? Stepped;

    public event EventHandler? Committed;

    public event EventHandler? Cancelled;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            if (_thread is not null)
            {
                return;
            }

            using var ready = new ManualResetEventSlim(false);

            // A thread of its own, and not the UI one: this hook's return is in the path of every key
            // in the system, and Windows drops a hook that takes too long to answer. On the UI thread,
            // a dock animation in progress would be enough to slow everyone's typing down.
            var thread = new Thread(() => Run(ready))
            {
                Name = "FrisoDock.WindowSwitcherGesture",
                IsBackground = true,
            };

            thread.Start();
            ready.Wait(StopTimeout);

            _thread = thread;
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_thread is null)
            {
                return;
            }

            if (_threadId != 0)
            {
                NativeMethods.PostThreadMessage(_threadId, NativeConstants.WM_QUIT, 0, 0);
            }

            _thread.Join(StopTimeout);
            _thread = null;
            _threadId = 0;
            _altDown = false;
            _sessionActive = false;
        }
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

    /// <summary>Installs the hook and keeps the message pump that feeds it.</summary>
    private void Run(ManualResetEventSlim ready)
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        _callback = OnKey;
        _hook = NativeMethods.SetWindowsHookEx(
            NativeConstants.WH_KEYBOARD_LL,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);

        ready.Set();

        if (_hook == 0)
        {
            return;
        }

        try
        {
            // A low-level hook only receives calls on the thread that installed it, and only
            // while that thread is dispatching messages.
            while (NativeMethods.GetMessage(out MSG message, 0, 0, 0) > 0)
            {
                NativeMethods.TranslateMessage(ref message);
                NativeMethods.DispatchMessage(ref message);
            }
        }
        finally
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
            _callback = null;
        }
    }

    /// <summary>
    /// One key. Returning 1 swallows it; anything else lets it through.
    ///
    /// What is swallowed is only Tab, and only while the gesture is in progress. Alt goes on its
    /// way deliberately: swallowing it would leave the apps thinking it is still held down,
    /// because the release would never reach them.
    /// </summary>
    private nint OnKey(int code, nint wParam, nint lParam)
    {
        if (code != NativeConstants.HC_ACTION)
        {
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        KBDLLHOOKSTRUCT key = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        uint message = (uint)wParam;
        bool down = message is NativeConstants.WM_KEYDOWN or NativeConstants.WM_SYSKEYDOWN;

        if (key.VirtualKey is NativeConstants.VK_LMENU or NativeConstants.VK_RMENU)
        {
            HandleAlt(down);
            return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (key.VirtualKey == NativeConstants.VK_TAB && (_altDown || _sessionActive))
        {
            if (down)
            {
                bool starts = !_sessionActive;
                _sessionActive = true;

                Stepped?.Invoke(this, new WindowSwitcherStepEventArgs(IsShiftDown(), starts));
            }

            // The release is swallowed too: letting it through would hand the app half a Tab.
            return 1;
        }

        if (key.VirtualKey == NativeConstants.VK_ESCAPE && _sessionActive && down)
        {
            _sessionActive = false;
            Cancelled?.Invoke(this, EventArgs.Empty);

            return 1;
        }

        return NativeMethods.CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void HandleAlt(bool down)
    {
        _altDown = down;

        if (down || !_sessionActive)
        {
            return;
        }

        _sessionActive = false;
        Committed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The physical Shift state, and not this thread's queue state: the hook runs on a thread with no
    /// windows, which never received a key and therefore has no keyboard state of its own.
    /// </summary>
    private static bool IsShiftDown()
    {
        return (NativeMethods.GetAsyncKeyState((int)NativeConstants.VK_SHIFT) & KeyPressedMask) != 0;
    }
}
