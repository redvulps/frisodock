using System.Windows;
using System.Windows.Input;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// Quick settings panel, in the Windows 11 finish. Same mechanics as the dock's other flyouts,
/// shared through <see cref="FlyoutChrome"/>.
/// </summary>
public partial class QuickSettingsWindow : Window
{
    private readonly QuickSettingsViewModel _viewModel;
    private readonly FlyoutChrome _chrome;

    private bool _closing;

    public QuickSettingsWindow(
        QuickSettingsViewModel viewModel,
        PixelRect anchor,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IScreenProvider screens)
    {
        _viewModel = viewModel;

        InitializeComponent();

        _chrome = new FlyoutChrome(this, anchor, backdrop, positioner, screens);
        DataContext = _viewModel;
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

        // The panel timer only runs while it is open: outside that, nobody is looking.
        _viewModel.Start();
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
        _viewModel.Dispose();
        base.OnClosed(e);
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
