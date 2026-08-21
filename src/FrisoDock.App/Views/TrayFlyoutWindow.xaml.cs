using System.Windows;
using System.Windows.Input;
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
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IScreenProvider screens)
    {
        _viewModel = viewModel;

        InitializeComponent();

        _chrome = new FlyoutChrome(this, anchor, backdrop, positioner, screens);

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
        _chrome.PositionAboveAnchor();
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
    /// The menu the app opens belongs to it, not to us: we close the flyout to get out of the way,
    /// otherwise the menu would appear behind this window, which is topmost.
    /// </summary>
    private void OnIconInvoked(object? sender, EventArgs e)
    {
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
