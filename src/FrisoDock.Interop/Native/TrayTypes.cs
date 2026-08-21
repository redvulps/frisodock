using System.Runtime.InteropServices;

namespace FrisoDock.Interop.Native;

/// <summary>
/// Structures of the notification area protocol. Undocumented by Microsoft: the
/// <c>Shell_NotifyIcon</c> merely packs this data and sends it through <c>WM_COPYDATA</c> to the
/// <c>Shell_TrayWnd</c> window. Layout checked against the ManagedShell implementation
/// (CairoShell/RetroBar), which is the open reference for this format.
/// </summary>
internal static class TrayTypes
{
    internal delegate nint WndProc(nint windowHandle, uint message, nint wParam, nint lParam);
}

[StructLayout(LayoutKind.Sequential)]
internal struct COPYDATASTRUCT
{
    public nint dwData;
    public int cbData;
    public nint lpData;
}

/// <summary>
/// NOTIFYICONDATA as it travels in <c>WM_COPYDATA</c>.
///
/// The handles are <c>uint</c> on purpose, and not <c>nint</c>: this message's layout is fixed at
/// 32 bits so that 32- and 64-bit processes talk to the same shell. Declaring them as
/// native pointers would misalign every following field on x64.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NOTIFYICONDATA
{
    public int cbSize;
    public uint hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public uint hIcon;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szTip;

    public int dwState;
    public int dwStateMask;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string szInfo;

    public uint uVersion;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string szInfoTitle;

    public uint dwInfoFlags;
    public Guid guidItem;
    public uint hBalloonIcon;
}

/// <summary>Payload of <c>WM_COPYDATA</c> with <c>dwData == 1</c>: an operation on an icon.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SHELLTRAYDATA
{
    public int dwUnknown;
    public uint dwMessage;
    public NOTIFYICONDATA nid;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASS
{
    public uint style;

    [MarshalAs(UnmanagedType.FunctionPtr)]
    public TrayTypes.WndProc lpfnWndProc;

    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string? lpszMenuName;

    [MarshalAs(UnmanagedType.LPWStr)]
    public string lpszClassName;
}
