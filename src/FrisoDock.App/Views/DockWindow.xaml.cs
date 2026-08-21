using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

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
    private readonly DockSettingsService _settings;
    private readonly SettingsWindowFactory _settingsWindows;
    private readonly IWindowThumbnailService _thumbnails;
    private readonly IWindowActivator _activator;
    private readonly DockMagnifier _magnifier = new();
    private readonly IWindowBackdrop _backdrop;
    private readonly IWindowPositioner _positioner;
    private readonly IScreenProvider _screens;
    private readonly DockAutoHide _autoHide;

    private JumpListWindow? _jumpList;
    private TrayFlyoutWindow? _trayFlyout;
    private SettingsWindow? _settingsWindow;
    private WindowPreviewWindow? _preview;
    private DockItemViewModel? _previewCandidate;
    private FrameworkElement? _previewAnchor;
    private DispatcherTimer? _trayMaintenanceTimer;
    private DispatcherTimer? _previewTimer;

    public DockWindow(
        DockViewModel viewModel,
        DockPlacementService placement,
        IShellRestartWatcher shellRestartWatcher,
        IAppBarService appBar,
        ITaskbarController taskbarController,
        JumpListFlyoutFactory flyoutFactory,
        TrayFlyoutFactory trayFlyoutFactory,
        ITrayHost trayHost,
        DockSettingsService settings,
        SettingsWindowFactory settingsWindows,
        IWindowThumbnailService thumbnails,
        IWindowActivator activator,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IScreenProvider screens,
        IWindowEnumerator windowEnumerator,
        DockVisibilityPolicy visibilityPolicy,
        ICursorProvider cursor)
    {
        _viewModel = viewModel;
        _placement = placement;
        _shellRestartWatcher = shellRestartWatcher;
        _appBar = appBar;
        _taskbarController = taskbarController;
        _flyoutFactory = flyoutFactory;
        _trayFlyoutFactory = trayFlyoutFactory;
        _trayHost = trayHost;
        _settings = settings;
        _settingsWindows = settingsWindows;
        _thumbnails = thumbnails;
        _activator = activator;
        _backdrop = backdrop;
        _positioner = positioner;
        _screens = screens;

        _autoHide = new DockAutoHide(placement, windowEnumerator, settings, visibilityPolicy, cursor, HasOpenFlyout);

        InitializeComponent();

        DataContext = _viewModel;
        _viewModel.LayoutChanged += OnLayoutChanged;
        _shellRestartWatcher.ShellRestarted += OnShellRestarted;
        _settings.Changed += OnSettingsChanged;
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
        _autoHide.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        CloseJumpList();
        CloseTrayFlyout();
        StopTrayHost();

        ClosePreview();

        _viewModel.LayoutChanged -= OnLayoutChanged;
        _shellRestartWatcher.ShellRestarted -= OnShellRestarted;
        _settings.Changed -= OnSettingsChanged;

        _autoHide.Dispose();
        _placement.Detach();
        _viewModel.Dispose();

        base.OnClosed(e);
    }

    private void OnLayoutChanged(object? sender, EventArgs e)
    {
        _placement.Update(_viewModel.Items.Count);
    }

    // ------------------------------------------------------------------ hiding the dock

    /// <summary>
    /// Touching the dock re-evaluates at once, instead of waiting for the next cursor sample: that is
    /// what makes the sliver respond the moment the mouse reaches the screen edge.
    /// </summary>
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        _autoHide.Evaluate();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _autoHide.Evaluate();
    }

    /// <summary>
    /// Whether something anchored to the dock is open right now. None of it can stay on screen with
    /// the dock hidden: the jump list and the thumbnails are positioned relative to the panel, and the
    /// dock menu even takes the cursor out of the window, which alone would ask for hiding.
    /// </summary>
    private bool HasOpenFlyout()
    {
        if (_jumpList is not null || _trayFlyout is not null || _preview is not null)
        {
            return true;
        }

        return PanelBorder.ContextMenu is { IsOpen: true };
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

    // ------------------------------------------------------------------ settings

    private void OnSettingsMenuClick(object sender, RoutedEventArgs e)
    {
        // A single window: clicking again with the screen open brings the existing one forward, instead
        // of stacking copies that edit the same settings.
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = _settingsWindows.Create();
        _settingsWindow.Closed += OnSettingsWindowClosed;
        _settingsWindow.Show();
    }

    private void OnSettingsWindowClosed(object? sender, EventArgs e)
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Closed -= OnSettingsWindowClosed;
            _settingsWindow = null;
        }
    }

    /// <summary>Applies live whatever the settings screen changed.</summary>
    private void OnSettingsChanged(object? sender, DockSettingsChangedEventArgs e)
    {
        if (e.TaskbarVisibilityChanged)
        {
            if (e.Current.HideNativeTaskbar)
            {
                _taskbarController.Hide();
            }
            else
            {
                _taskbarController.Restore();
            }
        }

        if (e.ScreenReservationChanged)
        {
            _placement.ApplyScreenReservation(AppBarCallbackMessage);
        }

        if (e.HideModeChanged)
        {
            _autoHide.Evaluate();
        }

        if (e.LayoutChanged)
        {
            // The window headroom depends on magnification: changing the strength changes the geometry.
            ResetMagnification();
            _placement.Update(_viewModel.Items.Count);
        }

        if (!e.Current.EnableWindowPreviews)
        {
            ClosePreview();
        }
    }

    // ------------------------------------------------------------------ magnification

    private void OnAppItemsMouseMove(object sender, MouseEventArgs e)
    {
        double magnification = _settings.Current.EffectiveMagnification;
        if (magnification <= 1.0)
        {
            return;
        }

        _magnifier.Apply(
            AppItems,
            e.GetPosition(AppItems),
            _viewModel.Appearance.IconSize,
            _viewModel.Appearance.ItemSpacing,
            magnification);
    }

    private void OnAppItemsMouseLeave(object sender, MouseEventArgs e)
    {
        ResetMagnification();
    }

    private void ResetMagnification()
    {
        _magnifier.Apply(
            AppItems,
            cursor: null,
            _viewModel.Appearance.IconSize,
            _viewModel.Appearance.ItemSpacing,
            magnification: 1.0);
    }

    // ------------------------------------------------------------------ window thumbnails

    /// <summary>
    /// The thumbnail panel waits for the cursor to settle on the icon. Opening at once would fill the
    /// screen with panels just from crossing the dock with the mouse.
    /// </summary>
    private void OnAppIconMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DockItemViewModel item } element)
        {
            return;
        }

        if (!_settings.Current.EnableWindowPreviews || !item.Model.IsRunning)
        {
            return;
        }

        _previewCandidate = item;
        _previewAnchor = element;

        StartPreviewTimer();
    }

    private void OnAppIconMouseLeave(object sender, MouseEventArgs e)
    {
        _previewCandidate = null;
        _previewAnchor = null;

        StopPreviewTimer();
        ClosePreview();
    }

    private void StartPreviewTimer()
    {
        StopPreviewTimer();

        _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };

        _previewTimer.Tick += OnPreviewTimerTick;
        _previewTimer.Start();
    }

    private void StopPreviewTimer()
    {
        if (_previewTimer is null)
        {
            return;
        }

        _previewTimer.Stop();
        _previewTimer.Tick -= OnPreviewTimerTick;
        _previewTimer = null;
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        StopPreviewTimer();

        if (_previewCandidate is not DockItemViewModel item || _previewAnchor is not FrameworkElement anchor)
        {
            return;
        }

        ShowPreview(item, anchor);
    }

    private void ShowPreview(DockItemViewModel item, FrameworkElement anchor)
    {
        ClosePreview();

        // Thumbnails are an accessory: a window that cannot be mirrored does not kill the dock.
        try
        {
            WindowPreviewViewModel viewModel = WindowPreviewViewModel.FromItem(item.Model);
            if (viewModel.IsEmpty)
            {
                return;
            }

            _preview = new WindowPreviewWindow(
                viewModel,
                FlyoutChrome.GetScreenRect(anchor),
                _thumbnails,
                _activator,
                _backdrop,
                _positioner,
                _screens);

            _preview.Closed += OnPreviewClosed;

            // Without activating: stealing the focus just to show a thumbnail would take the text caret
            // away from where the user was typing.
            _preview.ShowActivated = false;
            _preview.Show();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            ClosePreview();
        }
    }

    private void ClosePreview()
    {
        if (_preview is null)
        {
            return;
        }

        _preview.Closed -= OnPreviewClosed;
        _preview.Dismiss();
        _preview = null;
    }

    private void OnPreviewClosed(object? sender, EventArgs e)
    {
        _preview = null;
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
