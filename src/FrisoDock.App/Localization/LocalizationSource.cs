using System.ComponentModel;
using System.Windows.Data;
using FrisoDock.Core.Resources;

namespace FrisoDock.App.Localization;

/// <summary>
/// What the XAML binds to when it asks for a translated string. That is all (SRP): the text
/// comes from <see cref="Strings" />, and choosing the language belongs to the language service.
///
/// It exists because a translated string in XAML cannot be a plain value. Resolving the resource
/// once, at load time, is what x:Static does, and it would freeze the screen in the language it
/// was opened with. An indexer on a single observable object gives the WPF binding engine
/// something to re-read: <see cref="Refresh" /> raises the change for every index at once, and
/// every bound label updates in place.
/// </summary>
public sealed class LocalizationSource : INotifyPropertyChanged
{
    /// <summary>
    /// One instance, because the bindings are created by a markup extension that has nothing to
    /// receive a dependency through, and because a second instance would simply never be refreshed.
    /// </summary>
    public static LocalizationSource Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The text for a key, in the language in force.</summary>
    public string this[string key] => Strings.Get(key);

    /// <summary>
    /// Tells every binding to read again. The name is the one WPF reserves for "all the
    /// indexer values changed"; a per-key notification would mean knowing which keys are on
    /// screen, which is exactly what this class does not know.
    /// </summary>
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
    }
}
