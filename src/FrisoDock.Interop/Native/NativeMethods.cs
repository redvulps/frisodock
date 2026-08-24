using System.Runtime.InteropServices;
using System.Text;

namespace FrisoDock.Interop.Native;

/// <summary>
/// The project's ONLY P/Invoke point (DRY). No DllImport may exist outside here —
/// the services in <c>FrisoDock.Interop.Services</c> consume these methods and expose
/// clean abstractions to the rest of the application.
/// </summary>
internal static class NativeMethods
{
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";
    private const string Shell32 = "shell32.dll";
    private const string Dwmapi = "dwmapi.dll";
    private const string Shcore = "shcore.dll";
    private const string Shlwapi = "shlwapi.dll";
    private const string Ole32 = "ole32.dll";

    // ------------------------------------------------------------------ windows

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint FindWindow(string? className, string? windowName);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(NativeTypes.EnumWindowsProc callback, nint lParam);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindow(nint windowHandle);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowVisible(nint windowHandle);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsIconic(nint windowHandle);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(nint windowHandle, out RECT rect);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCursorPos(out POINT point);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

    /// <summary>Opens an OLE compound file, which is the automaticDestinations-ms format.</summary>
    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    internal static extern int StgOpenStorage(
        string name,
        nint priority,
        uint mode,
        nint exclude,
        uint reserved,
        out IStorage storage);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindow(nint windowHandle, int command);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShowWindowAsync(nint windowHandle, int command);

    [DllImport(User32)]
    internal static extern nint GetForegroundWindow();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport(User32)]
    internal static extern nint GetShellWindow();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport(User32)]
    internal static extern nint GetAncestor(nint windowHandle, uint flags);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowTextLength(nint windowHandle);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(nint windowHandle, StringBuilder buffer, int maxCount);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetClassName(nint windowHandle, StringBuilder buffer, int maxCount);

    [DllImport(User32, EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern nint GetWindowLongPtr(nint windowHandle, int index);

    [DllImport(User32, EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern nint SetWindowLongPtr(nint windowHandle, int index, nint value);

    [DllImport(User32, SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);

    [DllImport(User32, CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessage(string message);

    [DllImport(User32, CharSet = CharSet.Unicode)]
    internal static extern nint SendMessageTimeout(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam,
        uint flags,
        uint timeoutMilliseconds,
        out nint result);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostMessage(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport(User32, EntryPoint = "GetClassLongPtrW", SetLastError = true)]
    internal static extern nint GetClassLongPtr(nint windowHandle, int index);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AttachThreadInput(uint attachTo, uint attachFrom, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport(Kernel32)]
    internal static extern uint GetCurrentThreadId();

    // ------------------------------------------------------------------ processes

    [DllImport(Kernel32, SetLastError = true)]
    internal static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryFullProcessImageName(nint processHandle, uint flags, StringBuilder buffer, ref uint size);

    [DllImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);

    // ------------------------------------------------------------------ DWM

    [DllImport(Dwmapi)]
    internal static extern int DwmGetWindowAttribute(nint windowHandle, int attribute, out int value, int size);

    [DllImport(Dwmapi)]
    internal static extern int DwmSetWindowAttribute(nint windowHandle, int attribute, ref int value, int size);

    [DllImport(Dwmapi)]
    internal static extern int DwmRegisterThumbnail(nint destination, nint source, out nint thumbnail);

    [DllImport(Dwmapi)]
    internal static extern int DwmUnregisterThumbnail(nint thumbnail);

    [DllImport(Dwmapi)]
    internal static extern int DwmUpdateThumbnailProperties(nint thumbnail, ref DWM_THUMBNAIL_PROPERTIES properties);

    [DllImport(Dwmapi)]
    internal static extern int DwmQueryThumbnailSourceSize(nint thumbnail, out SIZE size);

    // ------------------------------------------------------------------ icons

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyIcon(nint icon);

    [DllImport(User32, SetLastError = true)]
    internal static extern nint CopyIcon(nint icon);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int PrivateExtractIcons(
        string fileName,
        int iconIndex,
        int desiredWidth,
        int desiredHeight,
        [Out] nint[] icons,
        [Out] int[] iconIds,
        int iconCount,
        uint flags);

    [DllImport(Shell32, CharSet = CharSet.Unicode)]
    internal static extern nint SHGetFileInfo(
        string path,
        uint fileAttributes,
        ref SHFILEINFO fileInfo,
        uint fileInfoSize,
        uint flags);

    // ------------------------------------------------------------------ jump lists

    /// <summary>
    /// Creates an IStream over a memory block. It is the vehicle for handing the jump list
    /// file to the Windows ShellLink, which parses the embedded shortcuts.
    /// </summary>
    [DllImport(Shlwapi, EntryPoint = "SHCreateMemStream")]
    internal static extern nint SHCreateMemStream(byte[] data, uint size);

    [DllImport(Ole32)]
    internal static extern int PropVariantClear([In, Out] PropVariant value);

    // ------------------------------------------------------------------ tray host

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClass(ref WNDCLASS windowClass);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnregisterClass(string className, nint instance);

    [DllImport(User32, EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowEx(
        uint exStyle,
        nint classAtom,
        string? windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint param);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint windowHandle);

    [DllImport(User32, EntryPoint = "DefWindowProcW", CharSet = CharSet.Unicode)]
    internal static extern nint DefWindowProc(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport(User32, EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
    internal static extern nint SendMessage(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport(User32, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SendNotifyMessage(nint windowHandle, uint message, nint wParam, nint lParam);

    [DllImport(User32)]
    internal static extern int GetSystemMetrics(int index);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport(Kernel32, CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint GetModuleHandle(string? moduleName);

    // ------------------------------------------------------------------ appbar

    [DllImport(Shell32, SetLastError = true)]
    internal static extern nuint SHAppBarMessage(uint message, ref APPBARDATA data);

    // ------------------------------------------------------------------ window events

    [DllImport(User32, SetLastError = true)]
    internal static extern nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint moduleHandle,
        NativeTypes.WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hookHandle);

    // ------------------------------------------------------------------ keyboard hook

    [DllImport(User32, SetLastError = true)]
    internal static extern nint SetWindowsHookEx(
        int hookId,
        NativeTypes.LowLevelKeyboardProc callback,
        nint moduleHandle,
        uint threadId);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(nint hookHandle);

    [DllImport(User32)]
    internal static extern nint CallNextHookEx(nint hookHandle, int code, nint wParam, nint lParam);

    [DllImport(User32)]
    internal static extern short GetAsyncKeyState(int virtualKey);

    // ------------------------------------------------------------------ message pump

    [DllImport(User32)]
    internal static extern int GetMessage(out MSG message, nint windowHandle, uint filterMin, uint filterMax);

    [DllImport(User32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref MSG message);

    [DllImport(User32)]
    internal static extern nint DispatchMessage(ref MSG message);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessage(uint threadId, uint message, nint wParam, nint lParam);

    // ------------------------------------------------------------------ synthetic input

    [DllImport(User32, SetLastError = true)]
    internal static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

    // ------------------------------------------------------------------ monitors

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(nint deviceContext, nint clip, NativeTypes.EnumMonitorsProc callback, nint lParam);

    [DllImport(User32, CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitorHandle, ref MONITORINFO monitorInfo);

    [DllImport(User32)]
    internal static extern nint MonitorFromPoint(POINT point, uint flags);

    [DllImport(Shcore)]
    internal static extern int GetDpiForMonitor(nint monitorHandle, int dpiType, out uint dpiX, out uint dpiY);
}
