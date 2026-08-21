using System.Runtime.InteropServices;
using FrisoDock.Core.Models;

namespace FrisoDock.Interop.Native;

/// <summary>Win32 structures and delegates. Declared a single time (DRY).</summary>
internal static class NativeTypes
{
    internal delegate bool EnumWindowsProc(nint windowHandle, nint lParam);

    internal delegate bool EnumMonitorsProc(nint monitorHandle, nint deviceContext, ref RECT clip, nint lParam);

    internal delegate void WinEventProc(
        nint hookHandle,
        uint eventType,
        nint windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    internal readonly PixelRect ToPixelRect()
    {
        return new PixelRect(Left, Top, Right, Bottom);
    }

    internal static RECT FromPixelRect(PixelRect rect)
    {
        return new RECT
        {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom,
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct SIZE
{
    public int cx;
    public int cy;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DWM_THUMBNAIL_PROPERTIES
{
    public int dwFlags;
    public RECT rcDestination;
    public RECT rcSource;
    public byte opacity;

    [MarshalAs(UnmanagedType.Bool)]
    public bool fVisible;

    [MarshalAs(UnmanagedType.Bool)]
    public bool fSourceClientAreaOnly;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct APPBARDATA
{
    public uint cbSize;
    public nint hWnd;
    public uint uCallbackMessage;
    public uint uEdge;
    public RECT rc;
    public nint lParam;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SHFILEINFO
{
    public nint hIcon;
    public int iIcon;
    public uint dwAttributes;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szDisplayName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string szTypeName;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion u;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)]
    public MOUSEINPUT mi;

    [FieldOffset(0)]
    public KEYBDINPUT ki;

    [FieldOffset(0)]
    public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public nint dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT
{
    public uint uMsg;
    public ushort wParamL;
    public ushort wParamH;
}
