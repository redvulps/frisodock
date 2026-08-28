using FrisoDock.Core.Resources;

namespace FrisoDock.Core.Models;

/// <summary>
/// Nature of a jump list category.
///
/// The first three values are the ones written in the tasks file, and are read straight off it:
/// they are the on-disk format, and cannot be renumbered. The ones after come from the recents
/// file, which has no field for this — there the grouping is ours, and the value only has to be
/// one the file never carries.
/// </summary>
public enum JumpListCategoryKind
{
    /// <summary>Category named by the app itself (e.g. "Most visited").</summary>
    Custom = 0,

    /// <summary>Windows default category (Frequent / Recent), with no embedded entries.</summary>
    Known = 1,

    /// <summary>App tasks. Windows labels this section "Tasks".</summary>
    Tasks = 2,

    /// <summary>What the user pinned to the app's jump list.</summary>
    Pinned = 3,

    /// <summary>What the user opened recently in the app.</summary>
    Recent = 4,
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
    int IconIndex)
{
    /// <summary>
    /// Label to display.
    ///
    /// The tasks an app writes carry System.Title filled in; recent documents do not —
    /// there Windows shows the file name, and that is what we do when the title comes empty.
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                return Title;
            }

            if (string.IsNullOrWhiteSpace(TargetPath))
            {
                return string.Empty;
            }

            string name = Path.GetFileName(TargetPath);

            return string.IsNullOrWhiteSpace(name) ? TargetPath : name;
        }
    }

    /// <summary>An entry with no label and no target has nothing to show and nothing to open.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(DisplayTitle);
}

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
    /// Label to display.
    ///
    /// The categories Windows names itself carry no name in the file, and the dock names them to
    /// match. Resolving here, and not where the category is built, is what keeps them out of the
    /// jump list cache: a label stored at parse time would stay in the previous language until
    /// the destinations file changed. Only the name the app wrote is passed through.
    /// </summary>
    public string DisplayTitle => Kind switch
    {
        JumpListCategoryKind.Tasks => Strings.JumpListTasks,
        JumpListCategoryKind.Pinned => Strings.JumpListPinned,
        JumpListCategoryKind.Recent => Strings.JumpListRecent,
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
