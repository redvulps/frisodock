using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;

namespace FrisoDock.App.ViewModels;

/// <summary>One tray icon inside the flyout.</summary>
public sealed partial class TrayIconViewModel : ObservableObject
{
    private readonly ITrayHost _host;
    private readonly TrayIcon _icon;
    private readonly Func<PixelPoint> _resolveScreenPoint;

    public TrayIconViewModel(
        TrayIcon icon,
        ImageSource? image,
        ITrayHost host,
        Func<PixelPoint> resolveScreenPoint)
    {
        _icon = icon;
        _host = host;
        _resolveScreenPoint = resolveScreenPoint;

        Image = image;
    }

    public string DisplayName => _icon.DisplayName;

    /// <summary>
    /// True while the app that owns this icon holds the foreground. The flyout reads it to tell
    /// the menu it just asked for apart from the user walking away.
    /// </summary>
    public bool IsOwnerInForeground => _host.IsAppInForeground(_icon);

    public ImageSource? Image { get; }

    /// <summary>Raised after forwarding an interaction, so the flyout can react to it.</summary>
    public event EventHandler? Invoked;

    /// <summary>
    /// Left click: forwarded to the owning app, which reacts as it would in the native tray.
    /// </summary>
    [RelayCommand]
    private void Activate()
    {
        Forward(TrayMouseEvent.LeftClick);
    }

    /// <summary>
    /// Double click: the gesture that opens the main window in most apps, and the one they
    /// answer when a single click does nothing at all.
    /// </summary>
    [RelayCommand]
    private void ActivateTwice()
    {
        Forward(TrayMouseEvent.LeftDoubleClick);
    }

    /// <summary>Right click: opens the app's own context menu.</summary>
    [RelayCommand]
    private void OpenContextMenu()
    {
        Forward(TrayMouseEvent.RightClick);
    }

    /// <summary>Middle click, forwarded like the others: what it does is the app's business.</summary>
    [RelayCommand]
    private void ActivateMiddle()
    {
        Forward(TrayMouseEvent.MiddleClick);
    }

    private void Forward(TrayMouseEvent mouseEvent)
    {
        _host.ForwardMouseEvent(_icon, mouseEvent, _resolveScreenPoint());
        Invoked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Content of the tray flyout. It builds the view model and nothing else (SRP): receiving the
/// icons belongs to <see cref="ITrayHost"/>.
/// </summary>
public sealed class TrayFlyoutViewModel
{
    public TrayFlyoutViewModel(IReadOnlyList<TrayIconViewModel> icons)
    {
        Icons = icons;
    }

    public IReadOnlyList<TrayIconViewModel> Icons { get; }

    public bool IsEmpty => Icons.Count == 0;

    /// <summary>Text shown when no app has registered an icon.</summary>
    public string EmptyMessage => Strings.TrayEmpty;

    public Visibility EmptyMessageVisibility => IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    public void SubscribeToInvocations(EventHandler handler)
    {
        foreach (TrayIconViewModel icon in Icons)
        {
            icon.Invoked += handler;
        }
    }

    public void UnsubscribeFromInvocations(EventHandler handler)
    {
        foreach (TrayIconViewModel icon in Icons)
        {
            icon.Invoked -= handler;
        }
    }
}
