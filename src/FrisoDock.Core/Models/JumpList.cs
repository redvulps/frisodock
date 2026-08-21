namespace FrisoDock.Core.Models;

/// <summary>Nature of a jump list category, as written by the app.</summary>
public enum JumpListCategoryKind
{
    /// <summary>Category named by the app itself (e.g. "Most visited").</summary>
    Custom = 0,

    /// <summary>Windows default category (Frequent / Recent), with no embedded entries.</summary>
    Known = 1,

    /// <summary>App tasks. Windows labels this section "Tasks".</summary>
    Tasks = 2,
}

/// <summary>
/// An actionable jump list entry. It corresponds to a shortcut embedded in the app's file.
/// </summary>
/// <param name="Title">Displayed label, coming from the shortcut's System.Title.</param>
/// <param name="TargetPath">Executable that will be launched.</param>
/// <param name="Arguments">The shortcut's command line arguments.</param>
/// <param name="IconPath">File to extract the entry's icon from.</param>
/// <param name="IconIndex">Index of the icon within the file.</param>
public sealed record JumpListEntry(
    string Title,
    string TargetPath,
    string Arguments,
    string? IconPath,
    int IconIndex);

/// <summary>One jump list block, with its header and its entries.</summary>
/// <param name="Kind">Nature of the category.</param>
/// <param name="Name">Name given by the app; null for categories Windows labels.</param>
/// <param name="Entries">Entries of the block, in the order they were written.</param>
public sealed record JumpListCategory(
    JumpListCategoryKind Kind,
    string? Name,
    IReadOnlyList<JumpListEntry> Entries)
{
    /// <summary>
    /// Label to display. Task categories carry no name in the file — Windows itself
    /// writes "Tasks" there, and the dock does the same to match.
    /// </summary>
    public string DisplayTitle => Kind switch
    {
        JumpListCategoryKind.Tasks => "Tarefas",
        _ => Name ?? string.Empty,
    };

    public bool HasEntries => Entries.Count > 0;
}

/// <summary>An app's jump list, as read from the shell file.</summary>
/// <param name="Categories">Categories in the order the app wrote them.</param>
public sealed record JumpList(IReadOnlyList<JumpListCategory> Categories)
{
    public static JumpList Empty { get; } = new([]);

    /// <summary>Categories that really have something to show.</summary>
    public IReadOnlyList<JumpListCategory> VisibleCategories =>
        Categories.Where(category => category.HasEntries).ToList();

    public bool IsEmpty => VisibleCategories.Count == 0;
}
