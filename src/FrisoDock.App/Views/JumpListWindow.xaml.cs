using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// Jump list flyout of a dock icon, in the Windows 11 finish.
///
/// It is a window of its own, and not a WPF ContextMenu, for a concrete reason: the system
/// acrylic is drawn by the DWM behind the window, and the DWM only does that on non-layered windows.
/// A rounded ContextMenu in WPF requires AllowsTransparency, which makes the window layered and
/// kills the material. Finish and positioning come from <see cref="FlyoutChrome"/>.
/// </summary>
public partial class JumpListWindow : Window
{
    private readonly JumpListFlyoutViewModel _viewModel;
    private readonly FlyoutChrome _chrome;

    private bool _closing;

    public JumpListWindow(
        JumpListFlyoutViewModel viewModel,
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
        _viewModel.SubscribeToInvocations(OnItemInvoked);
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
        _viewModel.UnsubscribeFromInvocations(OnItemInvoked);
        base.OnClosed(e);
    }

    /// <summary>
    /// Closing deferred until WPF finishes processing the current input: closing in the middle of the
    /// click would hand the rest of the mouse sequence to the window below.
    /// </summary>
    private void OnItemInvoked(object? sender, EventArgs e)
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
