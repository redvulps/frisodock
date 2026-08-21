using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// One dock icon. Translates a domain <see cref="DockItem"/> into what the UI needs
/// and handles the click. It discovers no windows and extracts no icons (SRP).
/// </summary>
public sealed partial class DockItemViewModel : ObservableObject
{
    private readonly IWindowActivator _activator;
    private readonly IAppLauncher _launcher;

    private int _cycleIndex;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private ImageSource? _icon;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _hasMultipleWindows;

    public DockItemViewModel(DockItem item, ImageSource? icon, IWindowActivator activator, IAppLauncher launcher)
    {
        _activator = activator;
        _launcher = launcher;

        Key = item.Key;
        Model = item;

        Apply(item, icon);
    }

    /// <summary>App identity, used to reconcile the list between updates.</summary>
    public AppKey Key { get; }

    /// <summary>Most recent domain state of this item.</summary>
    public DockItem Model { get; private set; }

    /// <summary>Updates the item in place, preserving the instance and avoiding flicker in the UI.</summary>
    public void Apply(DockItem item, ImageSource? icon)
    {
        ArgumentNullException.ThrowIfNull(item);

        Model = item;
        DisplayName = item.DisplayName;
        IsRunning = item.IsRunning;
        IsActive = item.IsActive;
        HasMultipleWindows = item.HasMultipleWindows;

        if (icon is not null)
        {
            Icon = icon;
        }

        if (_cycleIndex >= item.Windows.Count)
        {
            _cycleIndex = 0;
        }
    }

    /// <summary>
    /// Click on the icon: toggles the window if the app is running, otherwise launches the pinned app.
    /// With several windows, each click moves to the next — the same behaviour as
    /// dash-to-dock.
    /// </summary>
    [RelayCommand]
    private void Activate()
    {
        if (!Model.IsRunning)
        {
            LaunchIfPinned();
            return;
        }

        if (Model.HasMultipleWindows)
        {
            CycleWindows();
            return;
        }

        _activator.ToggleActivation(Model.Windows[0]);
    }

    /// <summary>Middle click: always opens a new instance, even with the app already running.</summary>
    [RelayCommand]
    private void LaunchNewInstance()
    {
        LaunchIfPinned();
    }

    private void LaunchIfPinned()
    {
        if (Model.Pinned is PinnedApp pinned)
        {
            _launcher.Launch(pinned);
        }
    }

    private void CycleWindows()
    {
        IReadOnlyList<WindowInfo> windows = Model.Windows;

        // If no window of the group is focused, the first click brings the current one back instead
        // of jumping to the next — jumping would surprise someone who only wants to restore it.
        if (!Model.IsActive)
        {
            _activator.Activate(windows[_cycleIndex]);
            return;
        }

        _cycleIndex = (_cycleIndex + 1) % windows.Count;
        _activator.Activate(windows[_cycleIndex]);
    }
}
