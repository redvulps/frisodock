using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// Panel with the live thumbnails of an app's windows.
///
/// The thumbnails are not drawn by WPF: the DWM replicates each source window inside this one,
/// in the rectangle the reserved element occupies. That is why each thumbnail's position can only be
/// computed after layout, and has to be redone if the window changes size.
/// </summary>
public partial class WindowPreviewWindow : Window
{
    private readonly WindowPreviewViewModel _viewModel;
    private readonly IWindowThumbnailService _thumbnails;
    private readonly IWindowActivator _activator;
    private readonly FlyoutChrome _chrome;
    private readonly List<IWindowThumbnail> _registered = [];

    private bool _closing;

    public WindowPreviewWindow(
        WindowPreviewViewModel viewModel,
        PixelRect anchor,
        IWindowThumbnailService thumbnails,
        IWindowActivator activator,
        IWindowBackdrop backdrop,
        IWindowPositioner positioner,
        IScreenProvider screens)
    {
        _viewModel = viewModel;
        _thumbnails = thumbnails;
        _activator = activator;

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
        RegisterThumbnails();
    }

    protected override void OnClosed(EventArgs e)
    {
        ReleaseThumbnails();
        base.OnClosed(e);
    }

    /// <summary>Closes the panel. Called by the dock when the cursor leaves the icon.</summary>
    public void Dismiss()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        Close();
    }

    private void RegisterThumbnails()
    {
        nint destination = new WindowInteropHelper(this).Handle;
        if (destination == 0)
        {
            return;
        }

        foreach (WindowPreviewItemViewModel item in _viewModel.Windows)
        {
            if (FindThumbnailArea(item) is not FrameworkElement area)
            {
                continue;
            }

            IWindowThumbnail? thumbnail = _thumbnails.Register(destination, item.Window.Handle);
            if (thumbnail is null)
            {
                continue;
            }

            _registered.Add(thumbnail);
            thumbnail.Show(CalculateDestination(area, thumbnail.SourceSize));
        }
    }

    /// <summary>
    /// Converts the reserved element's space into physical pixels relative to this window, which
    /// is the coordinate system the DWM expects, and fits the thumbnail without distortion.
    /// </summary>
    private PixelRect CalculateDestination(FrameworkElement area, PixelSize sourceSize)
    {
        Point topLeft = area.TranslatePoint(new Point(0, 0), this);
        double scale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        var bounds = PixelRect.FromSize(
            (int)Math.Round(topLeft.X * scale),
            (int)Math.Round(topLeft.Y * scale),
            (int)Math.Round(area.ActualWidth * scale),
            (int)Math.Round(area.ActualHeight * scale));

        if (sourceSize.IsEmpty)
        {
            return bounds;
        }

        return sourceSize.FitCentered(bounds);
    }

    private FrameworkElement? FindThumbnailArea(WindowPreviewItemViewModel item)
    {
        if (PreviewItems.ItemContainerGenerator.ContainerFromItem(item) is not DependencyObject container)
        {
            return null;
        }

        return FindDescendantByName(container, "ThumbnailArea");
    }

    private static FrameworkElement? FindDescendantByName(DependencyObject root, string name)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);

        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is FrameworkElement element && element.Name == name)
            {
                return element;
            }

            FrameworkElement? found = FindDescendantByName(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private void ReleaseThumbnails()
    {
        foreach (IWindowThumbnail thumbnail in _registered)
        {
            thumbnail.Dispose();
        }

        _registered.Clear();
    }

    /// <summary>
    /// Consumes the press: closing mid-click would hand the release to the window below,
    /// which is exactly the window we are about to activate.
    /// </summary>
    private void OnCardPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void OnCardPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (sender is not FrameworkElement { DataContext: WindowPreviewItemViewModel item })
        {
            return;
        }

        _activator.Activate(item.Window);
        Dispatcher.BeginInvoke(DispatcherPriority.Input, Dismiss);
    }
}
