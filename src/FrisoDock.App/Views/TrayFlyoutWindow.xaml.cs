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

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
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
            case MouseButton.Left:
                icon.ActivateCommand.Execute(null);
                break;

            case MouseButton.Right:
                icon.OpenContextMenuCommand.Execute(null);
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// The menu the app opens belongs to it, not to us: we close the flyout to get out of the way,
    /// otherwise the menu would appear behind this window, which is topmost.
    ///
    /// Closing is deferred until WPF finishes processing the current input. Closing in the middle of the
    /// click would hand the rest of the mouse sequence to the window below.
    /// </summary>
    private void OnIconInvoked(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, CloseFlyout);
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
