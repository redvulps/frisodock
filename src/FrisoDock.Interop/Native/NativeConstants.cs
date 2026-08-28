namespace FrisoDock.Interop.Native;

/// <summary>
/// Win32 constants used by the dock. Declared a single time (DRY): no other
/// file in the project may redeclare native values.
/// </summary>
internal static class NativeConstants
{
    // --- Shell window classes ---
    internal const string TaskbarPrimaryClass = "Shell_TrayWnd";
    internal const string TaskbarSecondaryClass = "Shell_SecondaryTrayWnd";
    internal const string TaskbarCreatedMessage = "TaskbarCreated";

    // --- ShowWindow ---
    internal const int SW_HIDE = 0;
    internal const int SW_SHOWNORMAL = 1;
    internal const int SW_SHOWMINIMIZED = 2;
    internal const int SW_SHOW = 5;
    internal const int SW_MINIMIZE = 6;
    internal const int SW_RESTORE = 9;

    // --- GetWindowLongPtr ---
    internal const int GWL_STYLE = -16;
    internal const int GWL_EXSTYLE = -20;

    internal const long WS_VISIBLE = 0x10000000L;
    internal const long WS_CHILD = 0x40000000L;

    internal const long WS_EX_TOOLWINDOW = 0x00000080L;
    internal const long WS_EX_APPWINDOW = 0x00040000L;
    internal const long WS_EX_NOACTIVATE = 0x08000000L;
    internal const long WS_EX_TOPMOST = 0x00000008L;

    // --- SetWindowPos ---
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    internal static readonly nint HWND_TOPMOST = -1;
    internal static readonly nint HWND_NOTOPMOST = -2;

    // --- GetAncestor ---
    internal const uint GA_ROOTOWNER = 3;

    // --- DwmGetWindowAttribute / DwmSetWindowAttribute ---
    internal const int DWMWA_CLOAKED = 14;
    internal const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    internal const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    internal const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    internal const int DWMWCP_ROUND = 2;

    /// <summary>Mica: the material of Windows 11 app windows, Settings included.</summary>
    internal const int DWMSBT_MAINWINDOW = 2;

    /// <summary>Transient window acrylic: the material Windows uses in menus and flyouts.</summary>
    internal const int DWMSBT_TRANSIENTWINDOW = 3;

    internal const uint WM_CLOSE = 0x0010;

    // --- DWM thumbnails ---
    internal const int DWM_TNP_RECTDESTINATION = 0x00000001;
    internal const int DWM_TNP_VISIBLE = 0x00000008;
    internal const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

    // --- Icons ---
    internal const uint WM_GETICON = 0x007F;
    internal const uint WM_DISPLAYCHANGE = 0x007E;

    /// <summary>Read, denying writes to others: it is how the shell keeps the file open.</summary>
    internal const uint STGM_READ_SHARE_DENY_WRITE = 0x00000020;

    /// <summary>
    /// Exclusive read. It is the mode a stream inside a compound file accepts — the
    /// sharing was already negotiated when the file was opened.
    /// </summary>
    internal const uint STGM_READ_SHARE_EXCLUSIVE = 0x00000010;

    /// <summary>The "no battery" flag in SYSTEM_POWER_STATUS.BatteryFlag.</summary>
    internal const byte BATTERY_FLAG_NO_BATTERY = 128;

    /// <summary>The "charging" flag in SYSTEM_POWER_STATUS.BatteryFlag.</summary>
    internal const byte BATTERY_FLAG_CHARGING = 8;

    /// <summary>Battery saver active, in SYSTEM_POWER_STATUS.SystemStatusFlag.</summary>
    internal const byte SYSTEM_STATUS_FLAG_POWER_SAVER = 1;

    /// <summary>Unknown percentage in SYSTEM_POWER_STATUS.BatteryLifePercent.</summary>
    internal const byte BATTERY_PERCENT_UNKNOWN = 255;

    /// <summary>The "stream" element type in a compound file's enumerator.</summary>
    internal const uint STGTY_STREAM = 2;
    internal const nint ICON_SMALL = 0;
    internal const nint ICON_BIG = 1;
    internal const nint ICON_SMALL2 = 2;

    internal const int GCLP_HICON = -14;
    internal const int GCLP_HICONSM = -34;

    internal const uint SMTO_ABORTIFHUNG = 0x0002;

    // --- SHGetFileInfo ---
    internal const uint SHGFI_ICON = 0x000000100;
    internal const uint SHGFI_LARGEICON = 0x000000000;
    internal const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    // --- OpenProcess ---
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // --- SHAppBarMessage ---
    internal const uint ABM_NEW = 0x00000000;
    internal const uint ABM_REMOVE = 0x00000001;
    internal const uint ABM_QUERYPOS = 0x00000002;
    internal const uint ABM_SETPOS = 0x00000003;
    internal const uint ABM_GETSTATE = 0x00000004;
    internal const uint ABM_GETTASKBARPOS = 0x00000005;
    internal const uint ABM_SETSTATE = 0x0000000A;

    internal const uint ABE_LEFT = 0;
    internal const uint ABE_TOP = 1;
    internal const uint ABE_RIGHT = 2;
    internal const uint ABE_BOTTOM = 3;

    internal const int ABS_AUTOHIDE = 0x0000001;
    internal const int ABS_ALWAYSONTOP = 0x0000002;

    internal const int ABN_STATECHANGE = 0x0000000;
    internal const int ABN_POSCHANGED = 0x0000001;
    internal const int ABN_FULLSCREENAPP = 0x0000002;
    internal const int ABN_WINDOWARRANGE = 0x0000003;

    // --- SetWinEventHook ---
    internal const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    internal const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
    internal const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
    internal const uint EVENT_OBJECT_CREATE = 0x8000;
    internal const uint EVENT_OBJECT_DESTROY = 0x8001;
    internal const uint EVENT_OBJECT_SHOW = 0x8002;
    internal const uint EVENT_OBJECT_HIDE = 0x8003;
    internal const uint EVENT_OBJECT_NAMECHANGE = 0x800C;

    internal const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    internal const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    internal const int OBJID_WINDOW = 0;
    internal const int CHILDID_SELF = 0;

    // --- Notification area (tray host) ---
    internal const string TrayNotifyClass = "TrayNotifyWnd";

    internal const uint WM_COPYDATA = 0x004A;
    internal const uint WM_WINDOWPOSCHANGED = 0x0047;
    internal const uint WM_CONTEXTMENU = 0x007B;
    internal const uint WM_USER = 0x0400;

    internal const uint WM_MOUSEMOVE = 0x0200;
    internal const uint WM_LBUTTONDOWN = 0x0201;
    internal const uint WM_LBUTTONUP = 0x0202;
    internal const uint WM_LBUTTONDBLCLK = 0x0203;
    internal const uint WM_RBUTTONDOWN = 0x0204;
    internal const uint WM_RBUTTONUP = 0x0205;
    internal const uint WM_MBUTTONUP = 0x0208;

    /// <summary>Icon selection in version 4 of the protocol, in place of WM_LBUTTONUP.</summary>
    internal const uint NIN_SELECT = WM_USER + 0;

    internal static readonly nint HWND_BROADCAST = 0xFFFF;
    internal static readonly nint HWND_BOTTOM = 1;

    // WM_COPYDATA payload: 0 = appbar, 1 = notification icon, 3 = position query.
    internal const nint TrayCopyDataAppBar = 0;
    internal const nint TrayCopyDataIcon = 1;

    internal const uint NIM_ADD = 0;
    internal const uint NIM_MODIFY = 1;
    internal const uint NIM_DELETE = 2;
    internal const uint NIM_SETFOCUS = 3;
    internal const uint NIM_SETVERSION = 4;

    internal const uint NIF_MESSAGE = 0x0001;
    internal const uint NIF_ICON = 0x0002;
    internal const uint NIF_TIP = 0x0004;
    internal const uint NIF_STATE = 0x0008;
    internal const uint NIF_GUID = 0x0020;

    internal const int NIS_HIDDEN = 0x0001;

    internal const uint CS_DBLCLKS = 0x0008;

    internal const uint WS_POPUP = 0x80000000;
    internal const uint WS_CHILD_STYLE = 0x40000000;
    internal const uint WS_CLIPSIBLINGS = 0x04000000;
    internal const uint WS_CLIPCHILDREN = 0x02000000;

    internal const uint WS_EX_TOOLWINDOW_STYLE = 0x00000080;
    internal const uint WS_EX_TOPMOST_STYLE = 0x00000008;

    internal const int SM_CXSCREEN = 0;

    // --- SendInput ---
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const ushort VK_LWIN = 0x5B;

    // --- Low-level keyboard hook ---
    internal const int WH_KEYBOARD_LL = 13;
    internal const int HC_ACTION = 0;
    internal const uint WM_KEYDOWN = 0x0100;
    internal const uint WM_KEYUP = 0x0101;
    internal const uint WM_SYSKEYDOWN = 0x0104;
    internal const uint WM_SYSKEYUP = 0x0105;
    internal const uint WM_QUIT = 0x0012;

    internal const uint VK_TAB = 0x09;
    internal const uint VK_ESCAPE = 0x1B;
    internal const uint VK_SHIFT = 0x10;

    // The key that on the US keyboard carries the backtick, and on ABNT2 the apostrophe. It is the
    // same physical key macOS uses to switch between windows of one app; here it becomes ALT+'.
    internal const uint VK_OEM_3 = 0xC0;

    // The hook reports Alt side by side, never the generic VK_MENU: what arrives is 0xA4 or 0xA5.
    internal const uint VK_LMENU = 0xA4;
    internal const uint VK_RMENU = 0xA5;

    // --- Monitors ---
    internal const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    internal const uint MONITORINFOF_PRIMARY = 0x00000001;
    internal const int MDT_EFFECTIVE_DPI = 0;
    internal const int USER_DEFAULT_SCREEN_DPI = 96;
}
