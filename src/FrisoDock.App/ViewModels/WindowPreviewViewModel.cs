using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>One window inside the thumbnail panel.</summary>
public sealed class WindowPreviewItemViewModel
{
    public WindowPreviewItemViewModel(WindowInfo window)
    {
        Window = window;
    }

    public WindowInfo Window { get; }

    /// <summary>Window title, or the app name when it has no title.</summary>
    public string Title => string.IsNullOrWhiteSpace(Window.Title) ? Window.DisplayName : Window.Title;
}

/// <summary>Content of the thumbnail panel of an app with open windows.</summary>
public sealed class WindowPreviewViewModel
{
    public WindowPreviewViewModel(IReadOnlyList<WindowPreviewItemViewModel> windows)
    {
        Windows = windows;
    }

    public IReadOnlyList<WindowPreviewItemViewModel> Windows { get; }

    public bool IsEmpty => Windows.Count == 0;

    public static WindowPreviewViewModel FromItem(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new WindowPreviewViewModel(
            item.Windows.Select(window => new WindowPreviewItemViewModel(window)).ToList());
    }
}
