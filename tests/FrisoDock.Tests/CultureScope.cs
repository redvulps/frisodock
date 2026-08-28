using System.Globalization;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Runs a block in a chosen UI culture, and puts the previous one back.
///
/// It exists because the language is process state, and a test that reads translated text is
/// otherwise at the mercy of the machine it runs on: asserting "Tarefas" passes here and fails on
/// an English or Spanish checkout.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previous;
    private readonly CultureInfo? _previousDefault;

    public CultureScope(string cultureName)
    {
        _previous = CultureInfo.CurrentUICulture;
        _previousDefault = CultureInfo.DefaultThreadCurrentUICulture;

        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentUICulture = _previous;
        CultureInfo.DefaultThreadCurrentUICulture = _previousDefault;
    }
}

/// <summary>
/// The tests that read or write the UI culture, kept out of each other's way.
///
/// xUnit runs test classes in parallel, and the UI culture is not fully per test:
/// <see cref="CultureInfo.DefaultThreadCurrentUICulture" /> is process wide, and reaches every
/// thread that has not set one of its own. Without a shared collection, a class switching the
/// language would flip the text another class is asserting on, in a way that only fails sometimes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CultureCollection
{
    public const string Name = "Culture";
}
