using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Hosts the notification area. That is all (SRP): it draws nothing and knows nothing of a dock.
///
/// How it works: we create a <c>Shell_TrayWnd</c> class window (plus the
/// <c>TrayNotifyWnd</c> child, which some apps look for) and fire the
/// <c>TaskbarCreated</c> broadcast. The apps then call <c>Shell_NotifyIcon</c>, which the shell turns into
/// <c>WM_COPYDATA</c> to the first window of that class — ours.
///
/// "First" means the highest in Z order, and that is why we push Explorer's window down
/// and keep ours on top, reasserting it periodically: anything that raises
/// its window back would send new icons to Explorer instead of here.
///
/// Messages that are not about icons (appbar, position queries) are forwarded to Explorer.
/// Without that, the dock's own space reservation — which goes through <c>SHAppBarMessage</c> and therefore
/// also looks for the first <c>Shell_TrayWnd</c> — would stop working.
/// </summary>
public sealed class TrayHost : ITrayHost, IDisposable
{
    /// <summary>Height of the host's hidden window. It only has to be plausible; nothing is drawn in it.</summary>
    private const int HostWindowHeight = 24;

    private readonly List<TrayIcon> _icons = [];

    // The delegate has to stay alive while the class is registered: Win32 stores only the
    // pointer, and the GC does not know that.
    private readonly TrayTypes.WndProc _wndProc;
    private readonly nint _instance;
    private readonly uint _taskbarCreatedMessage;

    private nint _trayWindow;
    private nint _notifyWindow;
    private nint _explorerTray;
    private ushort _trayClass;
    private ushort _notifyClass;
    private bool _disposed;

    public TrayHost()
    {
        _wndProc = OnWindowMessage;
        _instance = NativeMethods.GetModuleHandle(null);
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage(NativeConstants.TaskbarCreatedMessage);
    }

    public event EventHandler? IconsChanged;

    public IReadOnlyList<TrayIcon> Icons => _icons;

    public bool IsHosting => _trayWindow != 0;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsHosting)
        {
            return;
        }

        // Stored before we create ours: after that, FindWindow would find ours.
        _explorerTray = NativeMethods.FindWindow(NativeConstants.TaskbarPrimaryClass, null);

        CreateHostWindows();

        if (!IsHosting)
        {
            return;
        }

        PushExplorerTrayDown();
        RaiseHostWindow();
        BroadcastTaskbarCreated();
    }

    public void Stop()
    {
        if (!IsHosting)
        {
            return;
        }

        DestroyHostWindows();
        ClearIcons();

        // It gives the role back to Explorer: without this broadcast the apps would keep thinking the
        // host vanished and the icons would not come back to the native tray.
        BroadcastTaskbarCreated();
    }

    public void Maintain()
    {
        EnsureTopmost();
        RemoveDeadIcons();
    }

    /// <summary>
    /// Reasserts our window on top, because Explorer raises its own back in several
    /// situations (restart, DPI change).
    /// </summary>
    private void EnsureTopmost()
    {
        if (!IsHosting)
        {
            return;
        }

        if (NativeMethods.FindWindow(NativeConstants.TaskbarPrimaryClass, null) == _trayWindow)
        {
            return;
        }

        PushExplorerTrayDown();
        RaiseHostWindow();
    }

    public void ForwardMouseEvent(TrayIcon icon, TrayMouseEvent mouseEvent, PixelPoint screenPoint)
    {
        ArgumentNullException.ThrowIfNull(icon);

        nint owner = icon.Key.OwnerWindow;
        if (owner == 0 || !NativeMethods.IsWindow(owner))
        {
            return;
        }

        // Without this the app cannot bring its own menu forward: we are the ones holding the focus.
        NativeMethods.GetWindowThreadProcessId(owner, out uint processId);
        NativeMethods.AllowSetForegroundWindow(processId);

        foreach (uint message in GetMouseMessages(icon, mouseEvent))
        {
            PostCallback(icon, message, screenPoint);
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

    // ------------------------------------------------------------------ host windows

    private void CreateHostWindows()
    {
        _trayClass = RegisterWindowClass(NativeConstants.TaskbarPrimaryClass);
        if (_trayClass == 0)
        {
            return;
        }

        int screenWidth = NativeMethods.GetSystemMetrics(NativeConstants.SM_CXSCREEN);

        _trayWindow = NativeMethods.CreateWindowEx(
            NativeConstants.WS_EX_TOPMOST_STYLE | NativeConstants.WS_EX_TOOLWINDOW_STYLE,
            _trayClass,
            string.Empty,
            NativeConstants.WS_POPUP | NativeConstants.WS_CLIPCHILDREN | NativeConstants.WS_CLIPSIBLINGS,
            0,
            0,
            screenWidth,
            HostWindowHeight,
            0,
            0,
            _instance,
            0);

        if (_trayWindow == 0)
        {
            UnregisterWindowClass(ref _trayClass, NativeConstants.TaskbarPrimaryClass);
            return;
        }

        // Some apps look for the TrayNotifyWnd child to position themselves; without it they give up
        // on registering the icon.
        _notifyClass = RegisterWindowClass(NativeConstants.TrayNotifyClass);
        if (_notifyClass == 0)
        {
            return;
        }

        _notifyWindow = NativeMethods.CreateWindowEx(
            0,
            _notifyClass,
            null,
            NativeConstants.WS_CHILD_STYLE | NativeConstants.WS_CLIPCHILDREN | NativeConstants.WS_CLIPSIBLINGS,
            0,
            0,
            screenWidth,
            HostWindowHeight,
            _trayWindow,
            0,
            _instance,
            0);
    }

    private void DestroyHostWindows()
    {
        if (_notifyWindow != 0)
        {
            NativeMethods.DestroyWindow(_notifyWindow);
            _notifyWindow = 0;
        }

        UnregisterWindowClass(ref _notifyClass, NativeConstants.TrayNotifyClass);

        if (_trayWindow != 0)
        {
            NativeMethods.DestroyWindow(_trayWindow);
            _trayWindow = 0;
        }

        UnregisterWindowClass(ref _trayClass, NativeConstants.TaskbarPrimaryClass);
    }

    private ushort RegisterWindowClass(string className)
    {
        var windowClass = new WNDCLASS
        {
            style = NativeConstants.CS_DBLCLKS,
            lpfnWndProc = _wndProc,
            hInstance = _instance,
            lpszClassName = className,
        };

        return NativeMethods.RegisterClass(ref windowClass);
    }

    private void UnregisterWindowClass(ref ushort classAtom, string className)
    {
        if (classAtom == 0)
        {
            return;
        }

        NativeMethods.UnregisterClass(className, _instance);
        classAtom = 0;
    }

    private void PushExplorerTrayDown()
    {
        nint explorer = FindExplorerTray();
        if (explorer == 0)
        {
            return;
        }

        NativeMethods.SetWindowPos(
            explorer,
            NativeConstants.HWND_BOTTOM,
            0,
            0,
            0,
            0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOACTIVATE);
    }

    private void RaiseHostWindow()
    {
        NativeMethods.SetWindowPos(
            _trayWindow,
            NativeConstants.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOACTIVATE);
    }

    /// <summary>Explorer's window, that is, the first <c>Shell_TrayWnd</c> that is not ours.</summary>
    private nint FindExplorerTray()
    {
        if (_explorerTray != 0 && _explorerTray != _trayWindow && NativeMethods.IsWindow(_explorerTray))
        {
            return _explorerTray;
        }

        nint candidate = 0;
        while (true)
        {
            candidate = NativeMethods.FindWindowEx(0, candidate, NativeConstants.TaskbarPrimaryClass, null);
            if (candidate == 0)
            {
                return 0;
            }

            if (candidate != _trayWindow)
            {
                _explorerTray = candidate;
                return candidate;
            }
        }
    }

    private void BroadcastTaskbarCreated()
    {
        if (_taskbarCreatedMessage == 0)
        {
            return;
        }

        NativeMethods.SendNotifyMessage(NativeConstants.HWND_BROADCAST, _taskbarCreatedMessage, 0, 0);
    }

    // ------------------------------------------------------------------ messages

    private nint OnWindowMessage(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        if (message == NativeConstants.WM_COPYDATA && TryHandleCopyData(lParam, out nint copyDataResult))
        {
            return copyDataResult;
        }

        // Explorer shows its own taskbar again in some situations; if our window
        // were shown along with it, it would appear as a black band at the top of the screen.
        if (message == NativeConstants.WM_WINDOWPOSCHANGED && windowHandle == _trayWindow)
        {
            NativeMethods.ShowWindow(_trayWindow, NativeConstants.SW_HIDE);
        }

        if (message == NativeConstants.WM_COPYDATA || message >= NativeConstants.WM_USER)
        {
            return ForwardToExplorer(windowHandle, message, wParam, lParam);
        }

        return NativeMethods.DefWindowProc(windowHandle, message, wParam, lParam);
    }

    private bool TryHandleCopyData(nint lParam, out nint result)
    {
        result = 0;

        if (lParam == 0)
        {
            return false;
        }

        var copyData = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
        if (copyData.dwData != NativeConstants.TrayCopyDataIcon || copyData.lpData == 0)
        {
            return false;
        }

        SHELLTRAYDATA trayData;
        try
        {
            trayData = Marshal.PtrToStructure<SHELLTRAYDATA>(copyData.lpData);
        }
        catch (ArgumentException)
        {
            // The app sent a payload of unexpected size: ignoring it beats hanging the shell.
            return false;
        }

        if (!ApplyIconMessage(trayData.dwMessage, trayData.nid))
        {
            return false;
        }

        result = 1;
        return true;
    }

    private nint ForwardToExplorer(nint windowHandle, uint message, nint wParam, nint lParam)
    {
        nint explorer = FindExplorerTray();
        if (explorer == 0)
        {
            return NativeMethods.DefWindowProc(windowHandle, message, wParam, lParam);
        }

        return NativeMethods.SendMessage(explorer, message, wParam, lParam);
    }

    // ------------------------------------------------------------------ icon list

    private bool ApplyIconMessage(uint message, NOTIFYICONDATA data)
    {
        TrayIconKey key = CreateKey(data);

        switch (message)
        {
            case NativeConstants.NIM_ADD:
            case NativeConstants.NIM_MODIFY:
                AddOrUpdate(key, data);
                return true;

            case NativeConstants.NIM_DELETE:
                Remove(key);
                return true;

            case NativeConstants.NIM_SETVERSION:
                SetVersion(key, data.uVersion);
                return true;

            case NativeConstants.NIM_SETFOCUS:
                return true;

            default:
                return false;
        }
    }

    private void AddOrUpdate(TrayIconKey key, NOTIFYICONDATA data)
    {
        int index = _icons.FindIndex(icon => icon.Key.Equals(key));
        TrayIcon? existing = index >= 0 ? _icons[index] : null;

        // Each field only counts if the app set the matching flag; the rest of the struct is garbage.
        bool hasIcon = (data.uFlags & NativeConstants.NIF_ICON) != 0;
        bool hasTip = (data.uFlags & NativeConstants.NIF_TIP) != 0;
        bool hasMessage = (data.uFlags & NativeConstants.NIF_MESSAGE) != 0;
        bool hasState = (data.uFlags & NativeConstants.NIF_STATE) != 0;

        nint iconHandle = existing?.IconHandle ?? 0;
        if (hasIcon)
        {
            // The HICON belongs to the app: we copy it so ours stays valid even if the app destroys it.
            nint copy = NativeMethods.CopyIcon((nint)data.hIcon);
            if (copy != 0)
            {
                DestroyIcon(iconHandle);
                iconHandle = copy;
            }
        }

        var updated = new TrayIcon(
            key,
            hasTip ? data.szTip : existing?.Tooltip ?? string.Empty,
            iconHandle,
            hasMessage ? data.uCallbackMessage : existing?.CallbackMessage ?? 0,
            existing?.Version ?? 0,
            hasState ? (data.dwState & NativeConstants.NIS_HIDDEN) != 0 : existing?.IsHidden ?? false);

        if (index >= 0)
        {
            _icons[index] = updated;
        }
        else
        {
            _icons.Add(updated);
        }

        RaiseIconsChanged();
    }

    private void Remove(TrayIconKey key)
    {
        int index = _icons.FindIndex(icon => icon.Key.Equals(key));
        if (index < 0)
        {
            return;
        }

        DestroyIcon(_icons[index].IconHandle);
        _icons.RemoveAt(index);

        RaiseIconsChanged();
    }

    private void SetVersion(TrayIconKey key, uint version)
    {
        int index = _icons.FindIndex(icon => icon.Key.Equals(key));
        if (index < 0)
        {
            return;
        }

        _icons[index] = _icons[index] with { Version = version };
    }

    /// <summary>
    /// Drops icons whose app died. Windows does not report it: an app that closes without calling
    /// NIM_DELETE would leave a ghost icon in the list forever.
    /// </summary>
    private void RemoveDeadIcons()
    {
        int removed = _icons.RemoveAll(icon =>
        {
            bool dead = icon.Key.OwnerWindow == 0 || !NativeMethods.IsWindow(icon.Key.OwnerWindow);
            if (dead)
            {
                DestroyIcon(icon.IconHandle);
            }

            return dead;
        });

        if (removed > 0)
        {
            RaiseIconsChanged();
        }
    }

    private void ClearIcons()
    {
        foreach (TrayIcon icon in _icons)
        {
            DestroyIcon(icon.IconHandle);
        }

        _icons.Clear();
        RaiseIconsChanged();
    }

    private void RaiseIconsChanged()
    {
        IconsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void DestroyIcon(nint handle)
    {
        if (handle != 0)
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static TrayIconKey CreateKey(NOTIFYICONDATA data)
    {
        bool usesGuid = (data.uFlags & NativeConstants.NIF_GUID) != 0;

        // The handles arrive truncated to 32 bits by the fixed WM_COPYDATA format; widening them
        // back is safe because window handles fit in 32 bits even on x64.
        return new TrayIconKey((nint)data.hWnd, data.uID, usesGuid ? data.guidItem : Guid.Empty);
    }

    // ------------------------------------------------------------------ click forwarding

    /// <summary>
    /// Messages the app expects for each interaction.
    ///
    /// Many apps only react to the "button up", but others expect the down/up pair —
    /// sending both is what the tray itself does and covers both cases.
    /// </summary>
    private static IEnumerable<uint> GetMouseMessages(TrayIcon icon, TrayMouseEvent mouseEvent)
    {
        const uint Version4 = 4;

        switch (mouseEvent)
        {
            case TrayMouseEvent.LeftClick:
                yield return NativeConstants.WM_LBUTTONDOWN;
                yield return icon.Version >= Version4
                    ? NativeConstants.NIN_SELECT
                    : NativeConstants.WM_LBUTTONUP;
                break;

            case TrayMouseEvent.RightClick:
                yield return NativeConstants.WM_RBUTTONDOWN;
                yield return NativeConstants.WM_RBUTTONUP;

                // In version 4 it is WM_CONTEXTMENU that opens the app's menu.
                if (icon.Version >= Version4)
                {
                    yield return NativeConstants.WM_CONTEXTMENU;
                }

                break;

            case TrayMouseEvent.MiddleClick:
                yield return NativeConstants.WM_MBUTTONUP;
                break;

            default:
                yield break;
        }
    }

    /// <summary>
    /// Packs the message in the format of the app's protocol version.
    ///
    /// Up to version 3: wParam is the icon id and lParam the mouse message. In version 4 it is
    /// reversed — wParam carries the screen coordinates and lParam joins message and id — because the
    /// app needs to know where to draw the menu.
    /// </summary>
    private static void PostCallback(TrayIcon icon, uint message, PixelPoint screenPoint)
    {
        const uint Version4 = 4;

        if (icon.CallbackMessage == 0)
        {
            return;
        }

        nint wParam;
        nint lParam;

        if (icon.Version >= Version4)
        {
            wParam = MakeParam((uint)screenPoint.X, (uint)screenPoint.Y);
            lParam = MakeParam(message, icon.Key.Id);
        }
        else
        {
            wParam = (nint)icon.Key.Id;
            lParam = (nint)message;
        }

        NativeMethods.PostMessage(icon.Key.OwnerWindow, icon.CallbackMessage, wParam, lParam);
    }

    private static nint MakeParam(uint low, uint high)
    {
        return (nint)((low & 0xFFFF) | ((high & 0xFFFF) << 16));
    }
}
