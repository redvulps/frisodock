using System.Globalization;
using FrisoDock.App.Localization;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Puts the chosen language in force. That is all (SRP): the text itself belongs to
/// <see cref="Core.Resources.Strings" />, and reacting to it to whoever is bound to the
/// localization source.
///
/// Only the UI culture is touched. The regional format is a separate setting of the user's,
/// applied by <see cref="RegionalFormatService" /> over <see cref="CultureInfo.CurrentCulture" />,
/// and the clock has to keep following it whatever language the dock is in.
/// </summary>
public sealed class LanguageService : IDisposable
{
    /// <summary>
    /// The UI culture the process was born with, which is the user's Windows display language.
    ///
    /// It is captured once, and not read on demand, because <see cref="Apply" /> overwrites the
    /// current UI culture: after the first change, "the system language" would be whatever the
    /// dock last applied. The static initializer runs on the first touch of this class, which is
    /// the call in App.OnStartup, before anything here has written a culture.
    ///
    /// <see cref="CultureInfo.InstalledUICulture" /> is not the answer either, and that is the
    /// part that is easy to get wrong: it is the language Windows was *installed* with, so a
    /// machine installed in English and later switched to Portuguese would go back to English
    /// every time the user chose to follow the system. On a machine where the two agree the
    /// mistake is invisible.
    /// </summary>
    public static CultureInfo SystemUiCulture { get; } = CultureInfo.CurrentUICulture;

    private readonly DockSettingsService _settings;

    private bool _disposed;

    public LanguageService(DockSettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Applies the language already configured and starts following the changes.
    ///
    /// It has to run before the first window: a screen born in the wrong language would only
    /// correct itself on the next change.
    /// </summary>
    public void Start()
    {
        Apply(_settings.Current.Language);
        _settings.Changed += OnSettingsChanged;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _settings.Changed -= OnSettingsChanged;
        _disposed = true;
    }

    /// <summary>
    /// Culture the language resolves to, already falling back to the system.
    ///
    /// It is public because the language is applied before the container exists — the very first
    /// call happens with nothing built yet.
    /// </summary>
    public static CultureInfo Resolve(AppLanguage language)
    {
        string? name = language.ToCultureName();

        if (name is null)
        {
            return SystemUiCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(name);
        }
        catch (CultureNotFoundException)
        {
            // A culture Windows does not carry is not a reason to bring the dock down: the
            // resources fall back to the neutral English on their own.
            return SystemUiCulture;
        }
    }

    /// <summary>
    /// Applies the language to the process.
    ///
    /// <see cref="CultureInfo.DefaultThreadCurrentUICulture" /> goes along with the current thread
    /// on purpose: the jump list warmer and the tray host run on pool threads, and a thread born
    /// after the change would otherwise start in the system language.
    /// </summary>
    public static void Apply(AppLanguage language)
    {
        CultureInfo culture = Resolve(language);

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;

        LocalizationSource.Instance.Refresh();
    }

    private void OnSettingsChanged(object? sender, DockSettingsChangedEventArgs e)
    {
        if (e.LanguageChanged)
        {
            Apply(e.Current.Language);
        }
    }
}
