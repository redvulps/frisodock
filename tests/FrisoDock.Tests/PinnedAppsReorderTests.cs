using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Reordering the pinned apps. The rule is pure; the drag itself belongs to the window.
/// </summary>
public sealed class PinnedAppsReorderTests
{
    private const string ChromePath = @"C:\Program Files\Chrome\chrome.exe";
    private const string NotepadPath = @"C:\Windows\System32\notepad.exe";
    private const string TerminalPath = @"C:\Windows\System32\wt.exe";

    private readonly PinnedAppsReorder _reorder = new();

    [Fact]
    public void MoveToTheLeft()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(TerminalPath), 0);

        Assert.Equal(Keys(TerminalPath, ChromePath, NotepadPath), Keys(result));
    }

    [Fact]
    public void MoveToTheRight()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(ChromePath), 2);

        Assert.Equal(Keys(NotepadPath, TerminalPath, ChromePath), Keys(result));
    }

    [Fact]
    public void MoveToTheMiddle()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(ChromePath), 1);

        Assert.Equal(Keys(NotepadPath, ChromePath, TerminalPath), Keys(result));
    }

    [Fact]
    public void DraggingPastTheLastPinned_StopsAtTheEndOfTheBlock()
    {
        // The destination comes from the index in the dock, which includes the running apps. Without clamping
        // to the range, dragging a pinned app into the middle of the running ones would leave the list.
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(ChromePath), 9);

        Assert.Equal(Keys(NotepadPath, TerminalPath, ChromePath), Keys(result));
    }

    [Fact]
    public void NegativeIndex_StopsAtTheStart()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(TerminalPath), -3);

        Assert.Equal(Keys(TerminalPath, ChromePath, NotepadPath), Keys(result));
    }

    [Fact]
    public void SamePosition_ChangesNothing()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(NotepadPath), 1);

        // The same instance: the caller uses that to avoid writing to disk for nothing.
        Assert.Same(pinned, result);
    }

    [Fact]
    public void UnpinnedApp_ChangesNothing()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath);

        IReadOnlyList<PinnedApp> result = _reorder.Move(pinned, AppKey.FromExecutable(TerminalPath), 0);

        Assert.Same(pinned, result);
    }

    [Fact]
    public void ListWithASingleItem_ChangesNothing()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath);

        Assert.Same(pinned, _reorder.Move(pinned, AppKey.FromExecutable(ChromePath), 0));
    }

    [Fact]
    public void TheOriginalListIsNotChanged()
    {
        IReadOnlyList<PinnedApp> pinned = Pinned(ChromePath, NotepadPath, TerminalPath);

        _reorder.Move(pinned, AppKey.FromExecutable(TerminalPath), 0);

        Assert.Equal(Keys(ChromePath, NotepadPath, TerminalPath), Keys(pinned));
    }

    private static IReadOnlyList<PinnedApp> Pinned(params string[] paths)
    {
        return paths.Select(path => new PinnedApp(Path.GetFileNameWithoutExtension(path), path)).ToList();
    }

    private static AppKey[] Keys(params string[] paths)
    {
        return paths.Select(AppKey.FromExecutable).ToArray();
    }

    private static AppKey[] Keys(IReadOnlyList<PinnedApp> apps)
    {
        return apps.Select(app => app.Key).ToArray();
    }
}
