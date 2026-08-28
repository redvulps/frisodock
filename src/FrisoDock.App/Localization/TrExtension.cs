using System.Windows.Data;
using System.Windows.Markup;

namespace FrisoDock.App.Localization;

/// <summary>
/// Puts a translated string in the XAML: <c>Text="{loc:Tr SettingsTitle}"</c>. That is all (SRP).
///
/// It hands back a binding, and not the text: the language can change with the dock running, and
/// a value resolved at load time would keep the screen in the language it was opened with. The
/// binding points at the single <see cref="LocalizationSource" />, which is what gets refreshed.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
        Key = string.Empty;
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    /// <summary>Name of the entry in <see cref="Core.Resources.Strings" />.</summary>
    [ConstructorArgument("key")]
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationSource.Instance,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
