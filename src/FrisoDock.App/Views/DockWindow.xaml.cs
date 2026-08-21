using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// The dock window. Single responsibility: WPF plumbing — creating the HWND, forwarding shell
/// messages to the services that know how to read them, and asking for repositioning.
///
/// No dock logic lives here: items belong to <see cref="DockViewModel"/>, geometry to
/// <see cref="DockPlacementService"/> and Win32 to the Interop layer.
/// </summary>
public partial class DockWindow : Window
{
    /// <summary>
    /// Private message the shell uses to notify our appbar. WM_USER+1 is safe:
    /// the WM_USER range belongs to the window class, and this window is ours alone.
    /// </summary>
    private const uint AppBarCallbackMessage = 0x0400 + 1;

    private readonly DockViewModel _viewModel;
    private readonly DockPlacementService _placement;
    private readonly IShellRestartWatcher _shellRestartWatcher;
    private readonly IAppBarService _appBar;
    private readonly ITaskbarController _taskbarController;
    private readonly JumpListFlyoutFactory _flyoutFactory;
    private readonly TrayFlyoutFactory _trayFlyoutFactory;
    private readonly ITrayHost _trayHost;
    private readonly IWindowBackdrop _backdrop;
    private readonly IWindowPositioner _positioner;
    private readonly IScreenProvider _screens;

    private JumpListWindow? _jumpList;
    private TrayFlyoutWindow? _trayFlyout;
    private DispatcherTimer? _trayMaintenanceTimer;

    public DockWindow(
        DockViewModel viewModel,
        DockPlacementService placement,
        IShellRestartWatcher shellRestartWatcher,
        IAppBarService appBar,
        ITaskbarController taskbarController,
        JumpListFlyoutFactory flyoutFactory,
        TrayFlyoutFactory trayFlyoutFactory,
        ITrayHost trayHost,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IScreenProvider screens)
    {
        _viewModel = viewModel;
        _placement = placement;
        _shellRestartWatcher = shellRestartWatcher;
        _appBar = appBar;
        _taskbarController = taskbarController;
        _flyoutFactory = flyoutFactory;
        _trayFlyoutFactory = trayFlyoutFactory;
        _trayHost = trayHost;
        _backdrop = backdrop;
        _positioner = positioner;
        _screens = screens;

        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.LayoutChanged += OnLayoutChanged;
        _shellRestartWatcher.ShellRestarted += OnShellRestarted;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        nint handle = new WindowInteropHelper(this).Handle;

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(OnWindowMessage);
        }

        _placement.Attach(handle, AppBarCallbackMessage);
        _viewModel.Start();
        _placement.Update(_viewModel.Items.Count);

        StartTrayHost();
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseJumpList();
        CloseTrayFlyout();
        StopTrayHost();

        _viewModel.LayoutChanged -= OnLayoutChanged;
        _shellRestartWatcher.ShellRestarted -= OnShellRestarted;

        _placement.Detach();
        _viewModel.Dispose();

        base.OnClosed(e);
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        _placement.Update(_viewModel.Items.Count);
    }

    /// <summary>
    /// Takes over the notification area. From here on the icons come to the dock and stop
    /// appearing in Explorer's tray, until <see cref="StopTrayHost"/> gives the role back.
    /// </summary>
    private void StartTrayHost()
    {
        _trayHost.Start();

        // Windows does not report when Explorer takes the tray back, nor when an app dies without
        // removing its icon; the periodic maintenance covers both cases.
        _trayMaintenanceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _trayMaintenanceTimer.Tick += OnTrayMaintenanceTick;
        _trayMaintenanceTimer.Start();
    }

    private void StopTrayHost()
    {
        if (_trayMaintenanceTimer is not null)
        {
            _trayMaintenanceTimer.Stop();
            _trayMaintenanceTimer.Tick -= OnTrayMaintenanceTick;
            _trayMaintenanceTimer = null;
        }

        _trayHost.Stop();
    }

    private void OnTrayMaintenanceTick(object? sender, EventArgs e)
    {
        _trayHost.Maintain();
    }

    /// <summary>
    /// Right click on an app icon opens its jump list, not the dock menu.
    ///
    /// <see cref="ContextMenuEventArgs"/> bubbles, so marking it handled is what keeps the
    /// panel menu from showing up alongside.
    /// </summary>
    private void OnAppIconContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: DockItemViewModel item } element)
        {
            return;
        }

        ShowJumpList(item, element);
    }

    /// <summary>
    /// Opens the flyout with the tray icons the dock is hosting.
    /// </summary>
    private void OnTrayButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement anchor)
        {
            return;
        }

        CloseJumpList();

        try
        {
            PixelRect anchorRect = FlyoutChrome.GetScreenRect(anchor);

            // The point is resolved at click time, and not now, because the flyout closes before
            // forwarding: the app menu must be born over the dock button, which stays put.
            TrayFlyoutViewModel flyout = _trayFlyoutFactory.Create(
                () => new PixelPoint(anchorRect.Left + (anchorRect.Width / 2), anchorRect.Top));

            _trayFlyout = new TrayFlyoutWindow(flyout, anchorRect, _backdrop, _positioner, _screens);
            _trayFlyout.Closed += OnTrayFlyoutClosed;
            _trayFlyout.Show();
            _trayFlyout.Activate();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CloseTrayFlyout();
        }
    }

    private void CloseTrayFlyout()
    {
        if (_trayFlyout is null)
        {
            return;
        }

        _trayFlyout.Closed -= OnTrayFlyoutClosed;
        _trayFlyout.Close();
        _trayFlyout = null;
    }

    private void OnTrayFlyoutClosed(object? sender, EventArgs e)
    {
        _trayFlyout = null;
    }

    private void ShowJumpList(DockItemViewModel item, FrameworkElement anchorElement)
    {
        CloseJumpList();
        CloseTrayFlyout();

        // The jump list is an accessory: an app with a malformed file, a corrupt icon or a
        // layout failure must not kill the dock — falling here would leave the user's taskbar hidden
        // until the process was restarted.
        try
        {
            JumpListFlyoutViewModel flyout = _flyoutFactory.Create(item.Model);
            if (flyout.IsEmpty)
            {
                return;
            }

            _jumpList = new JumpListWindow(flyout, GetScreenRect(anchorElement), _backdrop, _positioner, _screens);
            _jumpList.Closed += OnJumpListClosed;
            _jumpList.Show();
            _jumpList.Activate();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            CloseJumpList();
        }
    }

    private void CloseJumpList()
    {
        if (_jumpList is null)
        {
            return;
        }

        _jumpList.Closed -= OnJumpListClosed;
        _jumpList.Close();
        _jumpList = null;
    }

    private void OnJumpListClosed(object? sender, EventArgs e)
    {
        _jumpList = null;
    }

    /// <summary>The element's rectangle in physical pixels, to anchor the flyout.</summary>
    private static PixelRect GetScreenRect(FrameworkElement element)
    {
        Point topLeft = element.PointToScreen(new Point(0, 0));
        Point bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));

        return new PixelRect(
            (int)Math.Round(topLeft.X),
            (int)Math.Round(topLeft.Y),
            (int)Math.Round(bottomRight.X),
            (int)Math.Round(bottomRight.Y));
    }

    /// <summary>
    /// A new Explorer recreates the taskbar already visible and erases the appbar registered by the
    /// previous instance. We reapply both states.
    /// </summary>
    private void OnShellRestarted(object? sender, EventArgs e)
    {
        if (_taskbarController.IsHidden)
        {
            _taskbarController.Hide();
        }

        _placement.Update(_viewModel.Items.Count);
        _placement.BringToTop();
    }

    private nint OnWindowMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        uint windowMessage = (uint)message;

        if (_shellRestartWatcher.TryHandle(windowMessage))
        {
            handled = true;
            return 0;
        }

        if (_appBar.TryHandleNotification(windowMessage, wParam, out AppBarNotification notification))
        {
            HandleAppBarNotification(notification);
            handled = true;
            return 0;
        }

        return 0;
    }

    private void HandleAppBarNotification(AppBarNotification notification)
    {
        switch (notification)
        {
            case AppBarNotification.PositionChanged:
            case AppBarNotification.StateChange:
                _placement.Update(_viewModel.Items.Count);
                break;

            case AppBarNotification.FullScreenApp:
                // A full screen app should cover the dock; it comes back to the top on the next
                // layout change.
                break;

            case AppBarNotification.WindowArrange:
                break;

            default:
                break;
        }
    }
}
