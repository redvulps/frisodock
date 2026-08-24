using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>One app in the switcher list.</summary>
public sealed partial class WindowSwitcherEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public WindowSwitcherEntryViewModel(DockItem item, ImageSource? image)
    {
        ArgumentNullException.ThrowIfNull(item);

        DisplayName = item.DisplayName;
        Image = image;
        WindowCount = item.Windows.Count;

        // The item's first window is the last one that was in use: the list arrives in Z order.
        Target = item.Windows[0];
    }

    public string DisplayName { get; }

    public ImageSource? Image { get; }

    public int WindowCount { get; }

    /// <summary>Window the switcher activates if this item is the chosen one.</summary>
    public WindowInfo Target { get; }

    /// <summary>
    /// Label shown under the icon row. The window count comes in because it is exactly what
    /// grouping hides: without it, an app with eight windows looks like it has one.
    /// </summary>
    public string Caption => WindowCount > 1
        ? $"{DisplayName}  ·  {WindowCount} janelas"
        : DisplayName;
}

/// <summary>
/// State of the switcher while Alt is held: the app list and which of them is under the
/// selection. It does not decide what an app is and activates nothing (SRP).
/// </summary>
public sealed partial class WindowSwitcherViewModel : ObservableObject
{
    private int _selectedIndex = -1;

    public WindowSwitcherViewModel(IEnumerable<WindowSwitcherEntryViewModel> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = new ObservableCollection<WindowSwitcherEntryViewModel>(entries);
    }

    public ObservableCollection<WindowSwitcherEntryViewModel> Entries { get; }

    public WindowSwitcherEntryViewModel? Selected =>
        _selectedIndex >= 0 && _selectedIndex < Entries.Count ? Entries[_selectedIndex] : null;

    public int SelectedIndex => _selectedIndex;

    /// <summary>Label of the item under the selection, in the fixed spot below the row.</summary>
    public string Caption => Selected?.Caption ?? string.Empty;

    /// <summary>
    /// Moves the selection. The highlight belongs to the item, not to the list, so WPF redraws only
    /// the two that changed instead of the whole row on every Tab.
    /// </summary>
    public void Select(int index)
    {
        if (index == _selectedIndex || index < 0 || index >= Entries.Count)
        {
            return;
        }

        if (Selected is WindowSwitcherEntryViewModel previous)
        {
            previous.IsSelected = false;
        }

        _selectedIndex = index;
        Entries[index].IsSelected = true;

        OnPropertyChanged(nameof(SelectedIndex));
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(Caption));
    }
}
