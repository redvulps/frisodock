using System.Windows;
using System.Windows.Input;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// Calendar flyout, in the Windows 11 finish. Same mechanics as the dock's other flyouts,
/// shared through <see cref="FlyoutChrome"/>.
/// </summary>
public partial class CalendarFlyoutWindow : Window
{
    private readonly FlyoutChrome _chrome;

    private bool _closing;

    public CalendarFlyoutWindow(
        CalendarFlyoutViewModel viewModel,
        PixelRect anchor,
        DockEdge edge,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IWindowSwitcherExclusion switcherExclusion,
        IScreenProvider screens)
    {
        InitializeComponent();

        _chrome = new FlyoutChrome(this, anchor, edge, backdrop, positioner, switcherExclusion, screens);
        DataContext = viewModel;
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
