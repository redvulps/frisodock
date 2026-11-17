using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;
using System.Runtime.InteropServices;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Recognizes Alt+Tab and Alt+' and swallows them, so the dock's switchers appear in place of the
/// native ones. That is all (SRP): it does not know what a window is nor what will be shown.
///
/// The low-level keyboard hook is the only path, and the alternatives were measured, not
/// assumed: <c>RegisterHotKey(MOD_ALT, VK_TAB)</c> returns <c>ERROR_HOTKEY_ALREADY_REGISTERED</c>
/// because the combination already belongs to the system, and raw input sees the key but does not
/// consume it, which would let the native switcher appear under ours.
///
/// <c>WH_KEYBOARD_LL</c> is global but not injected: no code of ours enters another
/// process, and Windows is the one calling back here — the same family as the WinEvent hooks the dock
/// already uses to watch windows, and with no administrator required.
///
/// Both gestures share the same hook: what changes between them is only which key is swallowed, and
/// which scope the step carries. A session in progress locks the scope — pressing the other
/// gesture's key in the middle of a session does nothing.
/// </summary>
public sealed class WindowSwitcherGesture : IWindowSwitcherGesture, IDisposable
{
    private const int KeyPressedMask = 0x8000;

    /// <summary>How long to wait for the hook thread to finish before moving on.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly object _sync = new();
    private readonly IKeyboardHookPlatform _platform;
    private readonly TimeSpan _waitTimeout;
    private HookRun? _run;
    private nint _hook;
    private bool _altDown;
    private bool _sessionActive;
    private WindowSwitchScope _sessionScope;
    private volatile bool _recognizeByApp;
    private volatile bool _recognizeSameApp;
    private bool _disposed;

    public WindowSwitcherGesture() : this(new KeyboardHookPlatform(), StopTimeout)
    {
    }

    internal WindowSwitcherGesture(IKeyboardHookPlatform platform, TimeSpan waitTimeout)
    {
        _platform = platform;
        _waitTimeout = waitTimeout;
    }

    internal Task Completion
    {
        get
        {
            lock (_sync)
            {
                return _run?.Stopped.Task ?? Task.CompletedTask;
            }
        }
    }

    public event EventHandler<WindowSwitcherStepEventArgs>? Stepped;

    public event EventHandler? Committed;

    public event EventHandler? Cancelled;

    public void SetModes(bool groupedByApp, bool sameApp)
    {
        HookRun? stopping = null;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _recognizeByApp = groupedByApp;
            _recognizeSameApp = sameApp;
            if (!groupedByApp && !sameApp)
            {
                stopping = RequestStop();
            }
        }

        if (groupedByApp || sameApp)
        {
            Start();
            return;
        }

        WaitForStop(stopping);
    }

    public void Stop()
    {
        HookRun? run;
        lock (_sync)
        {
            _recognizeByApp = false;
            _recognizeSameApp = false;
            run = RequestStop();
        }

        WaitForStop(run);
    }

    private HookRun? RequestStop()
    {
        HookRun? run = _run;
        if (run is not null)
        {
            run.StopRequested = true;
            uint threadId = Volatile.Read(ref run.ThreadId);
            if (threadId != 0)
            {
                _platform.PostQuit(threadId);
            }
        }

        return run;
    }

    private void WaitForStop(HookRun? run)
    {
        // A timed-out worker still owns its hook and callback. Keep it tracked until its
        // finally block unhooks it; a replacement must never share that native state.
        if (run is not null && run.Thread != Thread.CurrentThread)
        {
            run.Thread.Join(_waitTimeout);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
    }

    private void Start()
    {
        HookRun run;
        lock (_sync)
        {
            if (_disposed || _run is not null || (!_recognizeByApp && !_recognizeSameApp))
            {
                return;
            }

            // A thread of its own, and not the UI one: this hook's return is in the path of every key
            // in the system, and Windows drops a hook that takes too long to answer. On the UI thread,
            // a dock animation in progress would be enough to slow everyone's typing down.
            run = new HookRun();
            run.Thread = new Thread(() => Run(run))
            {
                Name = "FrisoDock.WindowSwitcherGesture",
                IsBackground = true,
            };

            _run = run;
            run.Thread.Start();
        }

        // Readiness has no disposable wait handle: late startup remains safe after this
        // bounded wait, including when Stop was requested before the message queue existed.
        run.Ready.Task.Wait(_waitTimeout);
    }

    /// <summary>Installs the hook and keeps the message pump that feeds it.</summary>
    private void Run(HookRun run)
    {
        NativeTypes.LowLevelKeyboardProc callback = OnKey;
        bool installed = false;
        try
        {
            uint threadId = _platform.CreateMessageQueue();
            Volatile.Write(ref run.ThreadId, threadId);
            if (run.StopRequested)
            {
                return;
            }

            _hook = _platform.Install(callback);
            installed = _hook != 0;
            run.Ready.TrySetResult();
            if (!installed || run.StopRequested)
            {
                return;
            }

            // A low-level hook only receives calls on the thread that installed it, and only
            // while that thread is dispatching messages.
            while (!run.StopRequested && _platform.GetMessage(out MSG message) > 0)
            {
                _platform.Dispatch(ref message);
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.TraceError("Keyboard hook failed: {0}", exception);
        }
        finally
        {
            run.Ready.TrySetResult();
            if (installed)
            {
                _platform.Uninstall(_hook);
            }

            _hook = 0;
            GC.KeepAlive(callback);
            bool restart;
            lock (_sync)
            {
                _run = null;
                _altDown = false;
                _sessionActive = false;
                restart = run.StopRequested && !_disposed && (_recognizeByApp || _recognizeSameApp);
            }

            run.Stopped.TrySetResult();
            if (restart)
            {
                Start();
            }
        }
    }

    private sealed class HookRun
    {
        public readonly TaskCompletionSource Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Thread Thread = null!;
        public uint ThreadId;
        public volatile bool StopRequested;
    }

    /// <summary>
    /// One key. Returning 1 swallows it; anything else lets it through.
    ///
    /// What is swallowed is only the gesture's key, and only while the gesture is in progress. Alt goes
    /// on its way deliberately: swallowing it would leave the apps thinking it is still held down,
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

        if (key.VirtualKey == NativeConstants.VK_TAB && _recognizeByApp && CanStep(WindowSwitchScope.ByApp))
        {
            Step(WindowSwitchScope.ByApp, down);

            // The release is swallowed too: letting it through would hand the app half a Tab.
            return 1;
        }

        if (key.VirtualKey == NativeConstants.VK_OEM_3 && _recognizeSameApp && CanStep(WindowSwitchScope.SameApp))
        {
            Step(WindowSwitchScope.SameApp, down);
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

    /// <summary>
    /// Whether this scope's key should act now.
    ///
    /// Outside a session, Alt being held is enough. Inside one, only the key of the scope that
    /// opened the session keeps working — pressing the other gesture's key midway changes nothing.
    /// </summary>
    private bool CanStep(WindowSwitchScope scope)
    {
        if (_sessionActive)
        {
            return _sessionScope == scope;
        }

        return _altDown;
    }

    private void Step(WindowSwitchScope scope, bool down)
    {
        if (!down)
        {
            return;
        }

        bool starts = !_sessionActive;
        _sessionActive = true;
        _sessionScope = scope;

        Stepped?.Invoke(this, new WindowSwitcherStepEventArgs(scope, IsShiftDown(), starts));
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
