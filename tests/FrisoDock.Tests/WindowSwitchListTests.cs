using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Rules of the grouped switcher: one item per app, in usage order, and each app's entry window
/// being the last one that was in front.
/// </summary>
public sealed class WindowSwitchListTests
{
    private readonly WindowSwitchList _list = new();

    private static WindowInfo Window(nint handle, string executable, string title = "janela")
    {
        return new WindowInfo(handle, title, ProcessId: (int)handle, executable, IsMinimized: false, IsForeground: false);
    }

    [Fact]
    public void Build_GroupsWindowsOfTheSameAppIntoOneItem()
    {
        WindowInfo[] windows =
        [
            Window(1, @"C:\apps\brave.exe"),
            Window(2, @"C:\apps\code.exe"),
            Window(3, @"C:\apps\brave.exe"),
        ];

        IReadOnlyList<DockItem> apps = _list.Build(windows);

        Assert.Equal(2, apps.Count);
        Assert.Equal(2, apps[0].Windows.Count);
        Assert.Single(apps[1].Windows);
    }

    [Fact]
    public void Build_KeepsTheArrivalOrder()
    {
        // The input comes in Z order, which is usage order: the app at the top has to stay at the top.
        WindowInfo[] windows =
        [
            Window(1, @"C:\apps\code.exe"),
            Window(2, @"C:\apps\brave.exe"),
            Window(3, @"C:\apps\explorer.exe"),
        ];

        IReadOnlyList<DockItem> apps = _list.Build(windows);

        Assert.Equal(@"c:\apps\code.exe", apps[0].Key.Value);
        Assert.Equal(@"c:\apps\brave.exe", apps[1].Key.Value);
        Assert.Equal(@"c:\apps\explorer.exe", apps[2].Key.Value);
    }

    [Fact]
    public void Build_TheItemsFirstWindowIsTheAppsMostRecent()
    {
        // It is what makes the switcher enter through the window that was in use, and not just any one.
        WindowInfo[] windows =
        [
            Window(10, @"C:\apps\brave.exe", "recente"),
            Window(20, @"C:\apps\code.exe"),
            Window(30, @"C:\apps\brave.exe", "antiga"),
        ];

        DockItem brave = _list.Build(windows)[0];

        Assert.Equal(10, brave.Windows[0].Handle);
        Assert.Equal("recente", brave.Windows[0].Title);
    }

    [Fact]
    public void Build_DiscardsWindowWithNoExecutable()
    {
        WindowInfo[] windows =
        [
            Window(1, @"C:\apps\code.exe"),
            new WindowInfo(2, "sem caminho", 2, null, IsMinimized: false, IsForeground: false),
        ];

        Assert.Single(_list.Build(windows));
    }

    [Fact]
    public void Build_WithNoWindowsReturnsAnEmptyList()
    {
        Assert.Empty(_list.Build([]));
    }

    [Fact]
    public void SelectFirst_ForwardsGoesToTheSecondApp()
    {
        // Alt+Tab tapped and released has to lead to the previous app, not to the one already in use.
        Assert.Equal(1, _list.SelectFirst(count: 4, backwards: false));
    }

    [Fact]
    public void SelectFirst_BackwardsGoesToTheLastApp()
    {
        Assert.Equal(3, _list.SelectFirst(count: 4, backwards: true));
    }

    [Fact]
    public void SelectFirst_WithASingleAppStaysOnIt()
    {
        Assert.Equal(0, _list.SelectFirst(count: 1, backwards: false));
        Assert.Equal(0, _list.SelectFirst(count: 1, backwards: true));
    }

    [Theory]
    [InlineData(3, 0, false, 1)]
    [InlineData(3, 2, false, 0)]
    [InlineData(3, 0, true, 2)]
    [InlineData(3, 1, true, 0)]
    public void Step_WrapsAtBothEnds(int count, int current, bool backwards, int expected)
    {
        Assert.Equal(expected, _list.Step(count, current, backwards));
    }

    [Fact]
    public void Step_WithAnEmptyListStaysAtZero()
    {
        Assert.Equal(0, _list.Step(count: 0, current: 0, backwards: false));
    }
}
