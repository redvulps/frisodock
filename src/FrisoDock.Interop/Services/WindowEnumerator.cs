using System.Diagnostics;
using System.Text;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Discovers the top-level windows that deserve an icon in the dock and reports when that set changes.
/// It does not group, sort or draw (SRP) — it only watches and reports.
///
/// The watching is done with WinEvent hooks (event driven, no polling). The events come in
/// bursts; debouncing is the consumer's responsibility, as documented on the interface.
/// </summary>
public sealed class WindowEnumerator : IWindowEnumerator, IDisposable
{
    private const int MaxTitleLength = 512;
    private const int MaxPathLength = 1024;

    private static readonly uint OwnProcessId = (uint)Environment.ProcessId;

    // The delegate has to stay alive while the hook exists: Win32 stores only the pointer,
    // and the GC does not know that. Losing that reference kills the process on the first event.
    private readonly NativeTypes.WinEventProc _winEventCallback;
    private readonly List<nint> _hooks = [];
    private readonly Dictionary<string, string?> _friendlyNames = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    public WindowEnumerator()
    {
        _winEventCallback = OnWinEvent;
    }

    public event EventHandler? WindowsChanged;

    public IReadOnlyList<WindowInfo> GetWindows()
    {
        var windows = new List<WindowInfo>();
        nint shellWindow = NativeMethods.GetShellWindow();
        nint foreground = NativeMethods.GetForegroundWindow();

        NativeMethods.EnumWindows(
            (handle, _) =>
            {
                if (!IsDockableWindow(handle, shellWindow))
                {
                    return true;
                }

                windows.Add(CreateWindowInfo(handle, foreground));
                return true;
            },
            0);

        return windows;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_hooks.Count > 0)
        {
            return;
        }

        // Two ranges: system events (focus/minimize) and object events
        // (creation, destruction, show, rename).
        InstallHook(NativeConstants.EVENT_SYSTEM_FOREGROUND, NativeConstants.EVENT_SYSTEM_MINIMIZEEND);
        InstallHook(NativeConstants.EVENT_OBJECT_CREATE, NativeConstants.EVENT_OBJECT_HIDE);
        InstallHook(NativeConstants.EVENT_OBJECT_NAMECHANGE, NativeConstants.EVENT_OBJECT_NAMECHANGE);
    }

    public void Stop()
    {
        foreach (nint hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
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

    private void InstallHook(uint eventMin, uint eventMax)
    {
        nint hook = NativeMethods.SetWinEventHook(
            eventMin,
            eventMax,
            0,
            _winEventCallback,
            0,
            0,
            NativeConstants.WINEVENT_OUTOFCONTEXT | NativeConstants.WINEVENT_SKIPOWNPROCESS);

        if (hook != 0)
        {
            _hooks.Add(hook);
        }
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
        // Only the "window" object itself matters; it ignores events from child controls,
        // which would arrive by the thousand and change nothing in the dock.
        if (objectId != NativeConstants.OBJID_WINDOW || childId != NativeConstants.CHILDID_SELF)
        {
            return;
        }

        if (windowHandle == 0)
        {
            return;
        }

        WindowsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A filter equivalent to the one the taskbar itself applies: root window, visible, with a title,
    /// not a tool window and not "cloaked".
    /// </summary>
    private static bool IsDockableWindow(nint handle, nint shellWindow)
    {
        if (handle == shellWindow || !NativeMethods.IsWindowVisible(handle))
        {
            return false;
        }

        if (IsOwnWindow(handle))
        {
            return false;
        }

        // Only the window owning the chain gets in: it avoids duplicating dialogs and tool windows.
        if (NativeMethods.GetAncestor(handle, NativeConstants.GA_ROOTOWNER) != handle)
        {
            return false;
        }

        long exStyle = NativeMethods.GetWindowLongPtr(handle, NativeConstants.GWL_EXSTYLE);
        bool isToolWindow = (exStyle & NativeConstants.WS_EX_TOOLWINDOW) != 0;
        bool isAppWindow = (exStyle & NativeConstants.WS_EX_APPWINDOW) != 0;

        // WS_EX_APPWINDOW forces presence in the taskbar even for a tool window.
        if (isToolWindow && !isAppWindow)
        {
            return false;
        }

        if (NativeMethods.GetWindowTextLength(handle) == 0)
        {
            return false;
        }

        return !IsCloaked(handle);
    }

    /// <summary>
    /// The dock never lists itself.
    ///
    /// Filtering by process, and not by the dock window's handle, is deliberate: WPF creates
    /// several auxiliary windows besides the visible one (the hidden owner window that keeps the dock out
    /// of the taskbar, resource notifications, IME). All of them are ours, and the process filter catches
    /// the whole set without the enumerator having to know the dock window.
    /// </summary>
    private static bool IsOwnWindow(nint handle)
    {
        NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
        return processId == OwnProcessId;
    }

    /// <summary>
    /// Suspended UWP windows are still "visible" to Win32 but are cloaked by the DWM.
    /// Without this test the dock would fill with ghosts (ApplicationFrameHost and friends).
    /// </summary>
    private static bool IsCloaked(nint handle)
    {
        const int SuccessHResult = 0;

        int result = NativeMethods.DwmGetWindowAttribute(
            handle,
            NativeConstants.DWMWA_CLOAKED,
            out int cloaked,
            sizeof(int));

        if (result != SuccessHResult)
        {
            return false;
        }

        return cloaked != 0;
    }

    private WindowInfo CreateWindowInfo(nint handle, nint foreground)
    {
        NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
        string? executablePath = GetExecutablePath(processId);

        return new WindowInfo(
            handle,
            GetWindowTitle(handle),
            (int)processId,
            executablePath,
            NativeMethods.IsIconic(handle),
            handle == foreground,
            GetBounds(handle),
            GetFriendlyName(executablePath));
    }

    /// <summary>
    /// The window's rectangle, which intellihide uses to know whether it occupies the dock's space.
    /// A window that does not respond returns an empty rectangle, which overlaps nothing.
    /// </summary>
    private static PixelRect GetBounds(nint handle)
    {
        if (!NativeMethods.GetWindowRect(handle, out RECT rect))
        {
            return default;
        }

        return rect.ToPixelRect();
    }

    /// <summary>
    /// Name the executable declares in its metadata — "Brave Browser" instead of "brave".
    /// It is the same text the native taskbar uses.
    ///
    /// The result is cached per path: reading the metadata opens the file, and the enumeration
    /// runs on every window change.
    /// </summary>
    private string? GetFriendlyName(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        if (_friendlyNames.TryGetValue(executablePath, out string? cached))
        {
            return cached;
        }

        string? friendlyName = ReadFriendlyName(executablePath);
        _friendlyNames[executablePath] = friendlyName;

        return friendlyName;
    }

    private static string? ReadFriendlyName(string executablePath)
    {
        try
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(executablePath);

            // FileDescription is the field the shell prefers; ProductName covers whoever leaves it empty.
            string? description = Trim(info.FileDescription) ?? Trim(info.ProductName);
            return description;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string? Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string GetWindowTitle(nint handle)
    {
        var buffer = new StringBuilder(MaxTitleLength);
        int length = NativeMethods.GetWindowText(handle, buffer, buffer.Capacity);

        if (length <= 0)
        {
            return string.Empty;
        }

        return buffer.ToString();
    }

    /// <summary>
    /// PROCESS_QUERY_LIMITED_INFORMATION is enough and works without elevation for the user's own
    /// processes. Protected processes fail — in those cases we return null and the window
    /// ends up discarded in the grouping, which is the desired behaviour.
    /// </summary>
    private static string? GetExecutablePath(uint processId)
    {
        nint processHandle = NativeMethods.OpenProcess(
            NativeConstants.PROCESS_QUERY_LIMITED_INFORMATION,
            false,
            processId);

        if (processHandle == 0)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(MaxPathLength);
            uint size = (uint)buffer.Capacity;

            if (!NativeMethods.QueryFullProcessImageName(processHandle, 0, buffer, ref size))
            {
                return null;
            }

            return buffer.ToString(0, (int)size);
        }
        finally
        {
            NativeMethods.CloseHandle(processHandle);
        }
    }
}
