using System.Globalization;
using System.Windows.Data;

namespace FrisoDock.App.Views;

/// <summary>
/// Speaker icon according to mute. It exists because the state is a single one — muted or not — and
/// the icon has to say it without text, as in the Windows volume control.
/// </summary>
public sealed class MuteGlyphConverter : IValueConverter
{
    private const string Muted = "";
    private const string Audible = "";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Muted : Audible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("O ícone não volta a virar estado.");
    }
}
