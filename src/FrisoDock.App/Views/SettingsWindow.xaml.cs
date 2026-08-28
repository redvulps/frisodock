using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Views;

/// <summary>
/// The dock settings screen, in the same finish as the flyouts.
///
/// It does not close on focus loss, unlike the flyouts: the effect of every option happens behind
/// this window, and the user has to be able to look at the dock and come back without reopening.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly IWindowBackdrop _backdrop;

    public SettingsWindow(SettingsViewModel viewModel, IWindowBackdrop backdrop)
    {
        _backdrop = backdrop;

        InitializeComponent();

        DataContext = viewModel;
    }

    /// <summary>Handle of this window, for whoever needs to bring it to the foreground through Win32.</summary>
    public nint Handle => new WindowInteropHelper(this).Handle;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Mica, and not the flyouts' acrylic: this is the dock's app window, and it is the material
        // the Windows Settings screen uses — the background pulls the wallpaper color
        // instead of sitting in a neutral gray.
        FlyoutChrome.ApplyAppearance(this, _backdrop, WindowBackdropMaterial.MainWindow);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    /// <summary>
    /// The window has no title bar — dragging by the background is the only way to move it.
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
