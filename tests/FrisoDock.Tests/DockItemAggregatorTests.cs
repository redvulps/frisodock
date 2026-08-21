using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The aggregator concentrates the dock's business rule and touches no Win32, so it is the point
/// where tests pay off the most.
/// </summary>
public sealed class DockItemAggregatorTests
{
    private const string ChromePath = @"C:\Program Files\Chrome\chrome.exe";
    private const string NotepadPath = @"C:\Windows\System32\notepad.exe";
    private const string TerminalPath = @"C:\Windows\System32\wt.exe";

    private readonly DockItemAggregator _aggregator = new();

    [Fact]
    public void Build_WithNoPinnedAndNoWindows_ReturnsAnEmptyList()
    {
        IReadOnlyList<DockItem> items = _aggregator.Build([], []);

        Assert.Empty(items);
    }

    [Fact]
    public void Build_PinnedAppWithNoWindow_ShowsAsNotRunning()
    {
        PinnedApp pinned = CreatePinned(ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], []);

        DockItem item = Assert.Single(items);
        Assert.True(item.IsPinned);
        Assert.False(item.IsRunning);
        Assert.Empty(item.Windows);
    }

    [Fact]
    public void Build_WindowOfAPinnedApp_IsMatchedWithThePinnedItem()
    {
        PinnedApp pinned = CreatePinned(ChromePath);
        WindowInfo window = CreateWindow(1, ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], [window]);

        DockItem item = Assert.Single(items);
        Assert.True(item.IsPinned);
        Assert.True(item.IsRunning);
        Assert.Equal(window, Assert.Single(item.Windows));
    }

    [Fact]
    public void Build_ExecutablePathIgnoresCase()
    {
        PinnedApp pinned = CreatePinned(ChromePath.ToUpperInvariant());
        WindowInfo window = CreateWindow(1, ChromePath.ToLowerInvariant());

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], [window]);

        DockItem item = Assert.Single(items);
        Assert.True(item.IsRunning);
    }

    [Fact]
    public void Build_SeveralWindowsOfTheSameApp_AreGroupedIntoASingleItem()
    {
        WindowInfo first = CreateWindow(1, ChromePath);
        WindowInfo second = CreateWindow(2, ChromePath);
        WindowInfo third = CreateWindow(3, ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([], [first, second, third]);

        DockItem item = Assert.Single(items);
        Assert.True(item.HasMultipleWindows);
        Assert.Equal(3, item.Windows.Count);
    }

    [Fact]
    public void Build_PinnedComeBeforeTheUnpinned()
    {
        PinnedApp pinned = CreatePinned(NotepadPath);
        WindowInfo unpinnedWindow = CreateWindow(1, ChromePath);
        WindowInfo pinnedWindow = CreateWindow(2, NotepadPath);

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], [unpinnedWindow, pinnedWindow]);

        Assert.Equal(2, items.Count);
        Assert.Equal(AppKey.FromExecutable(NotepadPath), items[0].Key);
        Assert.Equal(AppKey.FromExecutable(ChromePath), items[1].Key);
    }

    [Fact]
    public void Build_PinnedKeepTheOrderTheyWerePinnedIn()
    {
        PinnedApp first = CreatePinned(TerminalPath);
        PinnedApp second = CreatePinned(NotepadPath);
        PinnedApp third = CreatePinned(ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([first, second, third], []);

        Assert.Equal(AppKey.FromExecutable(TerminalPath), items[0].Key);
        Assert.Equal(AppKey.FromExecutable(NotepadPath), items[1].Key);
        Assert.Equal(AppKey.FromExecutable(ChromePath), items[2].Key);
    }

    [Fact]
    public void Build_UnpinnedFollowTheWindowDiscoveryOrder()
    {
        WindowInfo chrome = CreateWindow(1, ChromePath);
        WindowInfo notepad = CreateWindow(2, NotepadPath);

        IReadOnlyList<DockItem> items = _aggregator.Build([], [chrome, notepad]);

        Assert.Equal(AppKey.FromExecutable(ChromePath), items[0].Key);
        Assert.Equal(AppKey.FromExecutable(NotepadPath), items[1].Key);
    }

    [Fact]
    public void Build_AnAppNeverAppearsTwice()
    {
        PinnedApp pinned = CreatePinned(ChromePath);
        WindowInfo window = CreateWindow(1, ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], [window]);

        Assert.Single(items);
    }

    [Fact]
    public void Build_DuplicatePinned_KeepsOnlyTheFirst()
    {
        PinnedApp first = CreatePinned(ChromePath) with { DisplayName = "Primeiro" };
        PinnedApp duplicate = CreatePinned(ChromePath) with { DisplayName = "Duplicado" };

        IReadOnlyList<DockItem> items = _aggregator.Build([first, duplicate], []);

        DockItem item = Assert.Single(items);
        Assert.Equal("Primeiro", item.DisplayName);
    }

    [Fact]
    public void Build_WindowWithNoExecutablePath_IsDiscarded()
    {
        // Protected processes do not return the image path; without it there is no way to group.
        WindowInfo orphan = CreateWindow(1, executablePath: null);

        IReadOnlyList<DockItem> items = _aggregator.Build([], [orphan]);

        Assert.Empty(items);
    }

    [Fact]
    public void Build_ItemIsActiveWhenOneOfItsWindowsIsInTheForeground()
    {
        WindowInfo background = CreateWindow(1, ChromePath);
        WindowInfo foreground = CreateWindow(2, ChromePath, isForeground: true);

        IReadOnlyList<DockItem> items = _aggregator.Build([], [background, foreground]);

        Assert.True(Assert.Single(items).IsActive);
    }

    [Fact]
    public void Build_PinnedItemUsesItsOwnLabelAndIcon()
    {
        var pinned = new PinnedApp("Navegador", ChromePath, IconPath: @"C:\icons\chrome.ico");
        WindowInfo window = CreateWindow(1, ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], [window]);

        DockItem item = Assert.Single(items);
        Assert.Equal("Navegador", item.DisplayName);
        Assert.Equal(@"C:\icons\chrome.ico", item.IconSource);
    }

    [Fact]
    public void Build_PinnedByShortcut_MatchesTheMatchExecutable()
    {
        // Shortcuts and shell URIs do not match the process exe, hence MatchExecutablePath.
        var pinned = new PinnedApp(
            "Chrome",
            @"C:\Users\Public\Desktop\Chrome.lnk",
            MatchExecutablePath: ChromePath);

        WindowInfo window = CreateWindow(1, ChromePath);

        IReadOnlyList<DockItem> items = _aggregator.Build([pinned], [window]);

        Assert.True(Assert.Single(items).IsRunning);
    }

    [Fact]
    public void Build_SwitchingApp_DoesNotChangeTheOrderOfTheUnpinned()
    {
        // Regression: EnumWindows returns the windows in Z order. Switching app reorders that
        // list, and the dock swapped the icons around on every Alt+Tab.
        WindowInfo chrome = CreateWindow(1, ChromePath);
        WindowInfo notepad = CreateWindow(2, NotepadPath);
        WindowInfo terminal = CreateWindow(3, TerminalPath);

        IReadOnlyList<DockItem> before = _aggregator.Build([], [chrome, notepad, terminal]);

        // The user brings the terminal forward: it becomes the first window enumerated.
        IReadOnlyList<DockItem> after = _aggregator.Build([], [terminal, chrome, notepad], KeysOf(before));

        Assert.Equal(KeysOf(before), KeysOf(after));
    }

    [Fact]
    public void Build_NewApp_JoinsTheEnd()
    {
        WindowInfo chrome = CreateWindow(1, ChromePath);
        WindowInfo notepad = CreateWindow(2, NotepadPath);
        WindowInfo terminal = CreateWindow(3, TerminalPath);

        IReadOnlyList<DockItem> before = _aggregator.Build([], [chrome, notepad]);

        // The new app appears first in Z order, but its place in the dock is the end of the queue.
        IReadOnlyList<DockItem> after = _aggregator.Build([], [terminal, chrome, notepad], KeysOf(before));

        Assert.Equal([chrome.Key, notepad.Key, terminal.Key], KeysOf(after));
    }

    [Fact]
    public void Build_ClosedApp_DoesNotHoldItsPlace()
    {
        WindowInfo chrome = CreateWindow(1, ChromePath);
        WindowInfo notepad = CreateWindow(2, NotepadPath);
        WindowInfo terminal = CreateWindow(3, TerminalPath);

        IReadOnlyList<DockItem> before = _aggregator.Build([], [chrome, notepad, terminal]);
        IReadOnlyList<DockItem> after = _aggregator.Build([], [terminal, chrome], KeysOf(before));

        Assert.Equal([chrome.Key, terminal.Key], KeysOf(after));
    }

    [Fact]
    public void Build_ThePinnedDoNotMove()
    {
        PinnedApp pinned = CreatePinned(ChromePath);
        WindowInfo chrome = CreateWindow(1, ChromePath);
        WindowInfo notepad = CreateWindow(2, NotepadPath);

        IReadOnlyList<DockItem> before = _aggregator.Build([pinned], [chrome, notepad]);
        IReadOnlyList<DockItem> after = _aggregator.Build([pinned], [notepad, chrome], KeysOf(before));

        Assert.Equal([chrome.Key, notepad.Key], KeysOf(after));
        Assert.True(after[0].IsPinned);
    }

    [Fact]
    public void Build_WithNoPreviousOrder_FollowsTheWindowOrder()
    {
        // It is the case of the first build, when the dock has no list to preserve yet.
        WindowInfo chrome = CreateWindow(1, ChromePath);
        WindowInfo notepad = CreateWindow(2, NotepadPath);

        IReadOnlyList<DockItem> items = _aggregator.Build([], [notepad, chrome]);

        Assert.Equal([notepad.Key, chrome.Key], KeysOf(items));
    }

    private static AppKey[] KeysOf(IReadOnlyList<DockItem> items)
    {
        return items.Select(item => item.Key).ToArray();
    }

    private static PinnedApp CreatePinned(string executablePath)
    {
        return new PinnedApp(Path.GetFileNameWithoutExtension(executablePath), executablePath);
    }

    private static WindowInfo CreateWindow(
        nint handle,
        string? executablePath,
        bool isForeground = false)
    {
        return new WindowInfo(handle, $"Janela {handle}", (int)handle, executablePath, false, isForeground);
    }
}
