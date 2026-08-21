using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrisoDock.Core.Models;

namespace FrisoDock.App.ViewModels;

/// <summary>An actionable row of the flyout.</summary>
public sealed partial class JumpListItemViewModel : ObservableObject
{
    private readonly Action _invoke;

    public JumpListItemViewModel(
        string title,
        ImageSource? icon,
        string? glyph,
        Action invoke,
        bool isDestructive = false)
    {
        Title = title;
        Icon = icon;
        Glyph = glyph;
        IsDestructive = isDestructive;
        _invoke = invoke;
    }

    public string Title { get; }

    /// <summary>Icon extracted from the shortcut. The dock's own actions use <see cref="Glyph"/>.</summary>
    public ImageSource? Icon { get; }

    /// <summary>Character from the system icon font, used in the dock actions.</summary>
    public string? Glyph { get; }

    public bool HasIcon => Icon is not null;

    public bool HasGlyph => !string.IsNullOrEmpty(Glyph);

    /// <summary>Actions like "Close window", which Windows separates from the rest of the menu.</summary>
    public bool IsDestructive { get; }

    /// <summary>Raised after running the action, so the flyout can close.</summary>
    public event EventHandler? Invoked;

    [RelayCommand]
    private void Invoke()
    {
        _invoke();
        Invoked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>One block of the flyout: header plus its rows.</summary>
public sealed class JumpListSectionViewModel
{
    public JumpListSectionViewModel(string? title, IReadOnlyList<JumpListItemViewModel> items, bool separatedAbove)
    {
        Title = title;
        Items = items;
        SeparatedAbove = separatedAbove;
    }

    /// <summary>Section label; null in the final block, which Windows shows with no header.</summary>
    public string? Title { get; }

    public bool HasTitle => !string.IsNullOrEmpty(Title);

    /// <summary>Divider above the section, as Windows does before the app block.</summary>
    public bool SeparatedAbove { get; }

    public IReadOnlyList<JumpListItemViewModel> Items { get; }
}

/// <summary>
/// Content of a dock icon's flyout: the categories the app published, plus the final block
/// with the dock's own actions — open a new instance, pin/unpin and close window.
/// It builds the view model and nothing else (SRP): it reads no files and draws nothing.
/// </summary>
public sealed class JumpListFlyoutViewModel
{
    public JumpListFlyoutViewModel(string appName, IReadOnlyList<JumpListSectionViewModel> sections)
    {
        AppName = appName;
        Sections = sections;
    }

    public string AppName { get; }

    public IReadOnlyList<JumpListSectionViewModel> Sections { get; }

    public bool IsEmpty => Sections.Count == 0;

    /// <summary>Forwards the close requested by any row to the flyout's owner.</summary>
    public void SubscribeToInvocations(EventHandler handler)
    {
        foreach (JumpListSectionViewModel section in Sections)
        {
            foreach (JumpListItemViewModel item in section.Items)
            {
                item.Invoked += handler;
            }
        }
    }

    public void UnsubscribeFromInvocations(EventHandler handler)
    {
        foreach (JumpListSectionViewModel section in Sections)
        {
            foreach (JumpListItemViewModel item in section.Items)
            {
                item.Invoked -= handler;
            }
        }
    }
}
