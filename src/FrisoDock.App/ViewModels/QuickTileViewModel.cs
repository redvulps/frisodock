using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// One tile of the quick settings panel.
///
/// The Windows tiles come in two shapes, and both fit here: the whole one, which only toggles,
/// and the split one, whose right half leads to a settings page. When there is nowhere
/// to lead, the chevron disappears — <see cref="HasChevron"/>.
/// </summary>
public sealed partial class QuickTileViewModel : ObservableObject
{
    private readonly Func<Task>? _toggle;
    private readonly Action? _navigate;

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    private bool _isAvailable = true;

    [ObservableProperty]
    private string? _detail;

    public QuickTileViewModel(
        string glyph,
        string label,
        Func<Task>? toggle = null,
        Action? navigate = null)
    {
        Glyph = glyph;
        Label = label;
        _toggle = toggle;
        _navigate = navigate;
    }

    /// <summary>Tile icon, in Segoe Fluent Icons.</summary>
    public string Glyph { get; }

    /// <summary>Label under the tile.</summary>
    public string Label { get; }

    /// <summary>Whether the tile has the half that leads to settings.</summary>
    public bool HasChevron => _navigate is not null;

    /// <summary>
    /// Text that replaces the label when there is something more specific to say — the name of the
    /// connected network, for instance. It is what the native panel shows.
    /// </summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(Detail) ? Label : Detail;

    partial void OnDetailChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        if (_toggle is null)
        {
            // Tile with no switch: clicking it leads to settings, which is where the option lives.
            Navigate();
            return;
        }

        await _toggle();
    }

    [RelayCommand]
    private void Navigate()
    {
        _navigate?.Invoke();
    }
}
