using System.Windows.Media;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;

namespace FrisoDock.App.Services;

/// <summary>
/// Builds the flyout content of a dock icon. That is all (SRP): reading the file belongs to
/// <see cref="IJumpListProvider"/>, and launching to <see cref="IAppLauncher"/>.
///
/// The order mirrors the Windows one: first the categories published by the app, then a separate
/// block with the dock actions — open a new instance, pin/unpin and close window.
/// </summary>
public sealed class JumpListFlyoutFactory
{
    // Glyphs from the system icon font, the same ones the native taskbar uses.
    private const string PinGlyph = "";
    private const string UnpinGlyph = "";
    private const string CloseGlyph = "";

    private const int EntryIconSize = 32;

    private readonly IJumpListProvider _jumpLists;
    private readonly IAppLauncher _launcher;
    private readonly IWindowActivator _activator;
    private readonly IPinnedAppsEditor _pinnedApps;
    private readonly IconImageProvider _iconImages;

    public JumpListFlyoutFactory(
        IJumpListProvider jumpLists,
        IAppLauncher launcher,
        IWindowActivator activator,
        IPinnedAppsEditor pinnedApps,
        IconImageProvider iconImages)
    {
        _jumpLists = jumpLists;
        _launcher = launcher;
        _activator = activator;
        _pinnedApps = pinnedApps;
        _iconImages = iconImages;
    }

    public JumpListFlyoutViewModel Create(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var sections = new List<JumpListSectionViewModel>();

        JumpList jumpList = ReadJumpList(item);
        foreach (JumpListCategory category in jumpList.VisibleCategories)
        {
            sections.Add(CreateCategorySection(category));
        }

        sections.Add(CreateAppSection(item, separatedAbove: sections.Count > 0));

        return new JumpListFlyoutViewModel(item.DisplayName, sections);
    }

    /// <summary>
    /// Reads the jump list and extracts the entry icons without building any view model, so that
    /// the next open finds everything cached.
    ///
    /// It can be called off the UI thread: both caches behind it are concurrent and the images
    /// are frozen. Scheduling belongs to <see cref="JumpListWarmer"/>.
    /// </summary>
    public void Preload(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        foreach (JumpListCategory category in ReadJumpList(item).VisibleCategories)
        {
            foreach (JumpListEntry entry in category.Entries)
            {
                LoadEntryIcon(entry);
            }
        }
    }

    private JumpList ReadJumpList(DockItem item)
    {
        string? executable = item.JumpListExecutable;

        if (string.IsNullOrWhiteSpace(executable))
        {
            return JumpList.Empty;
        }

        return _jumpLists.GetFor(executable);
    }

    private JumpListSectionViewModel CreateCategorySection(JumpListCategory category)
    {
        var items = new List<JumpListItemViewModel>(category.Entries.Count);

        foreach (JumpListEntry entry in category.Entries)
        {
            JumpListEntry captured = entry;
            items.Add(new JumpListItemViewModel(
                entry.DisplayTitle,
                LoadEntryIcon(entry),
                glyph: null,
                () => _launcher.Launch(captured)));
        }

        return new JumpListSectionViewModel(category.DisplayTitle, items, separatedAbove: false);
    }

    /// <summary>Final block: the dock actions over the app, in the same shape as the native taskbar.</summary>
    private JumpListSectionViewModel CreateAppSection(DockItem item, bool separatedAbove)
    {
        var items = new List<JumpListItemViewModel>(3)
        {
            new(item.DisplayName, _iconImages.GetIcon(item), glyph: null, () => LaunchNewInstance(item)),
        };

        bool isPinned = _pinnedApps.IsPinned(item.Key);
        items.Add(new JumpListItemViewModel(
            isPinned ? Strings.JumpListUnpin : Strings.JumpListPin,
            icon: null,
            isPinned ? UnpinGlyph : PinGlyph,
            () => _pinnedApps.TogglePin(item)));

        if (item.IsRunning)
        {
            items.Add(new JumpListItemViewModel(
                item.HasMultipleWindows ? Strings.JumpListCloseAllWindows : Strings.JumpListCloseWindow,
                icon: null,
                CloseGlyph,
                () => CloseWindows(item),
                isDestructive: true));
        }

        return new JumpListSectionViewModel(title: null, items, separatedAbove);
    }

    private void LaunchNewInstance(DockItem item)
    {
        if (item.Pinned is PinnedApp pinned)
        {
            _launcher.Launch(pinned);
            return;
        }

        string? executable = item.Windows.FirstOrDefault()?.ExecutablePath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        _launcher.Launch(new PinnedApp(item.DisplayName, executable));
    }

    private void CloseWindows(DockItem item)
    {
        // A copy of the list: closing changes the window set while we walk it.
        foreach (WindowInfo window in item.Windows.ToList())
        {
            _activator.Close(window);
        }
    }

    private ImageSource? LoadEntryIcon(JumpListEntry entry)
    {
        return _iconImages.GetIcon(entry.IconPath, entry.IconIndex, EntryIconSize);
    }
}
