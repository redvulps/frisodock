using System.Windows.Media;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

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
    private readonly IIconExtractor _iconExtractor;
    private readonly IAppLauncher _launcher;
    private readonly IWindowActivator _activator;
    private readonly IPinnedAppsEditor _pinnedApps;
    private readonly IconImageProvider _iconImages;

    public JumpListFlyoutFactory(
        IJumpListProvider jumpLists,
        IIconExtractor iconExtractor,
        IAppLauncher launcher,
        IWindowActivator activator,
        IPinnedAppsEditor pinnedApps,
        IconImageProvider iconImages)
    {
        _jumpLists = jumpLists;
        _iconExtractor = iconExtractor;
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

    private JumpList ReadJumpList(DockItem item)
    {
        // The jump list belongs to the executable. An item pinned by shortcut keeps the exe in MatchExecutablePath.
        string? executable = item.Pinned?.MatchExecutablePath
            ?? item.Windows.FirstOrDefault()?.ExecutablePath
            ?? item.IconSource;

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
                entry.Title,
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
            isPinned ? "Desafixar da barra de tarefas" : "Fixar na barra de tarefas",
            icon: null,
            isPinned ? UnpinGlyph : PinGlyph,
            () => _pinnedApps.TogglePin(item)));

        if (item.IsRunning)
        {
            items.Add(new JumpListItemViewModel(
                item.HasMultipleWindows ? "Fechar todas as janelas" : "Fechar janela",
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
        if (string.IsNullOrWhiteSpace(entry.IconPath))
        {
            return null;
        }

        using IconHandle? handle = _iconExtractor.FromFile(entry.IconPath, EntryIconSize, entry.IconIndex);
        return IconImageProvider.ToImageSource(handle);
    }
}
