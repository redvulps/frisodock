using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Jump list display rules. Reading the file is Win32 and lives in Interop; what can be
/// tested without Windows is how the categories become menu sections.
/// </summary>
[Collection(CultureCollection.Name)]
public sealed class JumpListTests
{
    [Fact]
    public void CategoriesNamedByTheShell_TakeTheirLabelFromTheCatalogue()
    {
        // The file stores no name for these: the shell is what names them, and so does the dock.
        // The culture is pinned because the assertion is on translated text.
        using var culture = new CultureScope("en-US");

        Assert.Equal("Tasks", Category(JumpListCategoryKind.Tasks).DisplayTitle);
        Assert.Equal("Pinned", Category(JumpListCategoryKind.Pinned).DisplayTitle);
        Assert.Equal("Recent", Category(JumpListCategoryKind.Recent).DisplayTitle);
    }

    [Fact]
    public void CategoriesNamedByTheShell_FollowTheLanguage()
    {
        // The label is resolved on display, and not when the category is built: that is what
        // keeps it out of the jump list cache when the user changes language.
        var category = new JumpListCategory(JumpListCategoryKind.Recent, null, [CreateEntry("a")]);

        using (var portuguese = new CultureScope("pt-BR"))
        {
            Assert.Equal("Recentes", category.DisplayTitle);
        }

        using var spanish = new CultureScope("es-ES");
        Assert.Equal("Recientes", category.DisplayTitle);
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
            third => Assert.Equal(Strings.JumpListTasks, third.DisplayTitle));
    }

    [Fact]
    public void ItemPinnedByShortcut_TakesTheJumpListFromTheMatchExecutable()
    {
        // The launch target is the .lnk, which has no jump list at all: the exe is the one that has it.
        var pinned = new PinnedApp("Brave", @"C:\atalhos\Brave.lnk", MatchExecutablePath: @"C:\brave\brave.exe");
        var item = new DockItem(pinned.Key, "Brave", pinned.IconSource, pinned, []);

        Assert.Equal(@"C:\brave\brave.exe", item.JumpListExecutable);
    }

    [Fact]
    public void RunningOnlyItem_TakesTheJumpListFromTheWindowsExecutable()
    {
        var window = new WindowInfo(1, "Documento", 100, @"C:\code\Code.exe", false, false);
        var item = new DockItem(window.Key, "Code", null, null, [window]);

        Assert.Equal(@"C:\code\Code.exe", item.JumpListExecutable);
    }

    [Fact]
    public void PinnedWithNoMatchExecutable_FallsBackToTheIconFile()
    {
        var pinned = new PinnedApp("Bloco de notas", @"C:\Windows\notepad.exe");
        var item = new DockItem(pinned.Key, "Bloco de notas", pinned.IconSource, pinned, []);

        Assert.Equal(@"C:\Windows\notepad.exe", item.JumpListExecutable);
    }

    [Fact]
    public void BlankMatchExecutable_DoesNotBlockThePathFromTheWindow()
    {
        // The "??" would stop at the first non-null value, and blank is non-null: the item would end up with
        // no jump list while having an open window that knows the path.
        var pinned = new PinnedApp("App", @"shell:AppsFolder\algo", MatchExecutablePath: "   ");
        var window = new WindowInfo(1, "Janela", 100, @"C:\app\app.exe", false, false);
        var item = new DockItem(window.Key, "App", null, pinned, [window]);

        Assert.Equal(@"C:\app\app.exe", item.JumpListExecutable);
    }

    [Fact]
    public void ItemWithNoKnownExecutable_HasNoJumpList()
    {
        var window = new WindowInfo(1, "Janela", 100, null, false, false);
        var item = new DockItem(window.Key, "Desconhecido", null, null, [window]);

        Assert.Null(item.JumpListExecutable);
    }

    private static JumpListCategory Category(JumpListCategoryKind kind)
    {
        return new JumpListCategory(kind, null, [CreateEntry("a")]);
    }

    private static JumpListEntry CreateEntry(string title)
    {
        return new JumpListEntry(title, @"C:\app.exe", "--flag", @"C:\app.exe", 0);
    }
}
