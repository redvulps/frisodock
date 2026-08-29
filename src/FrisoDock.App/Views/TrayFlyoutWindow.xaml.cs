using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// Flyout with the notification area icons, in the Windows 11 finish.
/// Same mechanics as the jump list flyout, shared through <see cref="FlyoutChrome"/>.
/// </summary>
public partial class TrayFlyoutWindow : Window
{
    private readonly TrayFlyoutViewModel _viewModel;
    private readonly FlyoutChrome _chrome;

    private bool _closing;

    /// <summary>Icon whose app was last handed an interaction, and may be showing a menu now.</summary>
    private TrayIconViewModel? _forwardedTo;

    /// <summary>Click count of the press being handled, read on the release.</summary>
    private int _pressClickCount;

    private DispatcherTimer? _foregroundWatch;

    public TrayFlyoutWindow(
        TrayFlyoutViewModel viewModel,
        PixelRect anchor,
        DockEdge edge,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IWindowSwitcherExclusion switcherExclusion,
        IScreenProvider screens)
    {
        _viewModel = viewModel;

        InitializeComponent();

        _chrome = new FlyoutChrome(this, anchor, edge, backdrop, positioner, switcherExclusion, screens);

        DataContext = _viewModel;
        _viewModel.SubscribeToInvocations(OnIconInvoked);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _chrome.ApplyAppearance();
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        _chrome.PositionByAnchor();
    }

    /// <summary>
    /// Losing the focus is not one event but two, and only one of them means the user is done.
    /// The app coming forward to show the menu it was just asked for is the other, and closing on
    /// it took the flyout away from under the user in the middle of the gesture.
    ///
    /// Which of the two it is cannot be told here: the foreground changes in steps, and asking
    /// who owns it at this instant can catch the moment before the app has it. So a pending
    /// interaction hands the decision to the watch, which asks again a moment later.
    /// </summary>
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);

        if (_forwardedTo is not null)
        {
            StartForegroundWatch();
            return;
        }

        CloseFlyout();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            CloseFlyout();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopForegroundWatch();
        _viewModel.UnsubscribeFromInvocations(OnIconInvoked);
        base.OnClosed(e);
    }

    /// <summary>
    /// Consumes the mouse press.
    ///
    /// Without this, closing the flyout on the click would let the matching release land on the window
    /// behind, which would open its own context menu alongside the app's — two menus on
    /// screen, which was the symptom.
    /// </summary>
    private void OnIconPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // The count belongs to the press: WPF counts the clicks as they arrive, and by the
        // release there is nothing left to ask.
        _pressClickCount = e.ClickCount;
        e.Handled = true;
    }

    /// <summary>
    /// Fires the action on mouse release, once the input sequence has completed inside
    /// this window and there is nothing left to leak.
    /// </summary>
    private void OnIconPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: TrayIconViewModel icon })
        {
            return;
        }

        switch (e.ChangedButton)
        {
            case MouseButton.Left when _pressClickCount >= 2:
                icon.ActivateTwiceCommand.Execute(null);
                break;

            case MouseButton.Left:
                icon.ActivateCommand.Execute(null);
                break;

            case MouseButton.Right:
                icon.OpenContextMenuCommand.Execute(null);
                break;

            case MouseButton.Middle:
                icon.ActivateMiddleCommand.Execute(null);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// The flyout stays. The app was handed an interaction and may be about to show a menu, and
    /// closing here is what made the tray vanish from under the user, one icon per opening.
    ///
    /// What replaces the close is the watch below: the flyout goes away when the foreground stops
    /// being ours and stops being that app's.
    /// </summary>
    private void OnIconInvoked(object? sender, EventArgs e)
    {
        _forwardedTo = sender as TrayIconViewModel;
        StartForegroundWatch();
    }

    /// <summary>
    /// True while the app we just poked owns the foreground, which is the menu the user asked for
    /// and not a reason to go away.
    /// </summary>
    private bool IsAppShowingItself()
    {
        return _forwardedTo?.IsOwnerInForeground == true;
    }

    /// <summary>
    /// Sampling, because there is no second deactivation to wait for: the window is already
    /// inactive while the app menu is up, so nothing else would tell us the user moved on.
    /// The same reason the dock samples the cursor instead of listening for it.
    /// </summary>
    private void StartForegroundWatch()
    {
        if (_foregroundWatch is not null)
        {
            return;
        }

        _foregroundWatch = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        _foregroundWatch.Tick += OnForegroundWatchTick;
        _foregroundWatch.Start();
    }

    private void StopForegroundWatch()
    {
        if (_foregroundWatch is null)
        {
            return;
        }

        _foregroundWatch.Stop();
        _foregroundWatch.Tick -= OnForegroundWatchTick;
        _foregroundWatch = null;
    }

    private void OnForegroundWatchTick(object? sender, EventArgs e)
    {
        if (IsActive)
        {
            // The user came back to the flyout; the watch has nothing left to answer.
            StopForegroundWatch();
            return;
        }

        if (IsAppShowingItself())
        {
            return;
        }

        CloseFlyout();
    }

    private void CloseFlyout()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }
}
