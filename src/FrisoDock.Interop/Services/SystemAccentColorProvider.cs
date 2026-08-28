using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Windows.UI.ViewManagement;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reads the Windows accent color and reports when it changes. That is all (SRP).
///
/// It is WinRT, and it works in an unpackaged app: measured on this machine, <c>UISettings</c> returns
/// the whole accent palette with no package manifest and no administrator — this machine's purple
/// accent came out as <c>#A94DC1</c>, with <c>AccentLight2</c> at <c>#DB9FE5</c>. It is the same
/// path the quick settings radios already use.
///
/// The read is of the light variant, not of the pure accent. The dock is dark, and that is what
/// Windows 11 does in its panel's tiles in dark mode; the pure accent sinks into the background.
/// Confirmed by the value that was hand-written in the XAML: <c>#4CC2FF</c> is exactly the
/// <c>AccentLight2</c> of the default blue.
/// </summary>
public sealed class SystemAccentColorProvider : IAccentColorProvider, IDisposable
{
    /// <summary>
    /// Kept in a field out of obligation, not convenience: the event lives as long as the object
    /// does, and a <c>UISettings</c> created just for the read would be collected right afterwards —
    /// the new color would never arrive.
    /// </summary>
    private readonly UISettings _settings = new();

    private AccentPalette _current;

    public SystemAccentColorProvider()
    {
        _current = Read();
        _settings.ColorValuesChanged += OnColorValuesChanged;
    }

    public event EventHandler? Changed;

    public AccentPalette Current => Volatile.Read(ref _current);

    public void Dispose()
    {
        _settings.ColorValuesChanged -= OnColorValuesChanged;
    }

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        AccentPalette palette = Read();

        // The same event arrives on the light/dark switch and on high contrast. Without this
        // comparison, the dock would rebuild the brushes for changes that are not the accent color.
        if (palette == Volatile.Read(ref _current))
        {
            return;
        }

        Volatile.Write(ref _current, palette);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private AccentPalette Read()
    {
        try
        {
            Windows.UI.Color color = _settings.GetColorValue(UIColorType.AccentLight2);
            return AccentPalettes.From(new AccentColor(color.R, color.G, color.B));
        }
        catch (Exception)
        {
            // Reading the accent is finish, not a requirement: a corporate policy or a build
            // without the WinRT projection must not kill the dock. The default blue will do.
            return AccentPalettes.Default;
        }
    }
}
