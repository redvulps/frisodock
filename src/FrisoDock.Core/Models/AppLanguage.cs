namespace FrisoDock.Core.Models;

/// <summary>
/// Language the dock shows its own text in.
///
/// It is not the regional format: the clock and the calendar keep following what the user chose
/// in Windows, so someone running the system in English with the Brazilian format gets an
/// English dock still showing 28/08/2026.
/// </summary>
public enum AppLanguage
{
    /// <summary>Follow the Windows display language, falling back to English.</summary>
    System = 0,

    PortugueseBrazil = 1,

    English = 2,

    Spanish = 3,
}

public static class AppLanguageExtensions
{
    /// <summary>
    /// Culture the language maps to, or null for <see cref="AppLanguage.System" /> — where the
    /// answer is whatever Windows already gave the process.
    ///
    /// The specific cultures are what the user picks, while the translations are filed under the
    /// neutral ones (pt, es). Resource fallback closes the gap on its own, and it closes it for
    /// the region variants too: pt-PT and es-AR land on the same files, which they would not if
    /// the catalogue were filed under pt-BR and es-ES.
    /// </summary>
    public static string? ToCultureName(this AppLanguage language)
    {
        return language switch
        {
            AppLanguage.PortugueseBrazil => "pt-BR",
            AppLanguage.English => "en-US",
            AppLanguage.Spanish => "es-ES",
            _ => null,
        };
    }
}
