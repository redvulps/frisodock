using FrisoDock.Core.Models;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Jump list display rules. Reading the file is Win32 and lives in Interop; what can be
/// tested without Windows is how the categories become menu sections.
/// </summary>
public sealed class JumpListTests
{
    [Fact]
    public void TasksCategory_UsesTheWindowsLabel()
    {
        // The file stores no name for this category: what writes "Tasks" is the shell.
        var category = new JumpListCategory(JumpListCategoryKind.Tasks, null, [CreateEntry("Nova janela")]);

        Assert.Equal("Tarefas", category.DisplayTitle);
    }

    [Fact]
    public void CustomCategory_UsesTheNameWrittenByTheApp()
    {
        var category = new JumpListCategory(JumpListCategoryKind.Custom, "Mais visitados", [CreateEntry("Facebook")]);

        Assert.Equal("Mais visitados", category.DisplayTitle);
    }

    [Fact]
    public void EmptyCategories_DoNotAppear()
    {
        // The Windows default categories come with no embedded entries and must not become a section.
        var jumpList = new JumpList(
        [
            new JumpListCategory(JumpListCategoryKind.Known, null, []),
            new JumpListCategory(JumpListCategoryKind.Custom, "Mais visitados", [CreateEntry("Facebook")]),
        ]);

        JumpListCategory visible = Assert.Single(jumpList.VisibleCategories);
        Assert.Equal("Mais visitados", visible.DisplayTitle);
    }

    [Fact]
    public void JumpListWithOnlyEmptyCategories_IsConsideredEmpty()
    {
        var jumpList = new JumpList([new JumpListCategory(JumpListCategoryKind.Known, null, [])]);

        Assert.True(jumpList.IsEmpty);
    }

    [Fact]
    public void EmptyJumpList_HasNoCategories()
    {
        Assert.True(JumpList.Empty.IsEmpty);
        Assert.Empty(JumpList.Empty.Categories);
    }

    [Fact]
    public void CategoryOrder_IsPreserved()
    {
        // The app writes in the order it wants to see; the dock does not reorder.
        var jumpList = new JumpList(
        [
            new JumpListCategory(JumpListCategoryKind.Custom, "Mais visitados", [CreateEntry("a")]),
            new JumpListCategory(JumpListCategoryKind.Custom, "Fechadas recentemente", [CreateEntry("b")]),
            new JumpListCategory(JumpListCategoryKind.Tasks, null, [CreateEntry("c")]),
        ]);

        Assert.Collection(
            jumpList.VisibleCategories,
            first => Assert.Equal("Mais visitados", first.DisplayTitle),
            second => Assert.Equal("Fechadas recentemente", second.DisplayTitle),
            third => Assert.Equal("Tarefas", third.DisplayTitle));
    }

    private static JumpListEntry CreateEntry(string title)
    {
        return new JumpListEntry(title, @"C:\app.exe", "--flag", @"C:\app.exe", 0);
    }
}
