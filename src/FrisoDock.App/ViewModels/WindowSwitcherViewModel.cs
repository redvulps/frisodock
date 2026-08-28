using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;

namespace FrisoDock.App.ViewModels;

/// <summary>One item in the switcher list — an app (Alt+Tab) or a single window (Alt+').</summary>
public sealed partial class WindowSwitcherEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    private WindowSwitcherEntryViewModel(WindowInfo target, ImageSource? image, string caption)
    {
        Target = target;
        Image = image;
        Caption = caption;
    }

    /// <summary>
    /// An app in the Alt+Tab list. The target is the item's first window, which is the last one that
    /// was in use — the list arrives in Z order. The count goes into the label because it is exactly
    /// what grouping hides: without it, an app with eight windows looks like it has one.
    /// </summary>
    public static WindowSwitcherEntryViewModel ForApp(DockItem item, ImageSource? image)
    {
        ArgumentNullException.ThrowIfNull(item);

        string caption = item.Windows.Count > 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.SwitcherWindowCountFormat, item.DisplayName, item.Windows.Count)
            : item.DisplayName;

        return new WindowSwitcherEntryViewModel(item.Windows[0], image, caption);
    }

    /// <summary>
    /// A window in the Alt+' list. The label is the window title, which is what tells apart the
    /// windows of a single app; when the title comes empty, the app name stands in.
    /// </summary>
    public static WindowSwitcherEntryViewModel ForWindow(WindowInfo window, ImageSource? image)
    {
        ArgumentNullException.ThrowIfNull(window);

        string caption = string.IsNullOrWhiteSpace(window.Title) ? window.DisplayName : window.Title;

        return new WindowSwitcherEntryViewModel(window, image, caption);
    }

    public ImageSource? Image { get; }

    /// <summary>Window the switcher activates if this item is the chosen one.</summary>
    public WindowInfo Target { get; }

    /// <summary>Label shown under the icon row, for the item under the selection.</summary>
    public string Caption { get; }
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
