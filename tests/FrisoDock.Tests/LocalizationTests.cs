using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FrisoDock.App.Services;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The translation catalogue.
///
/// What is being protected here is the failure that only shows up on screen: a key that exists
/// in one language and not in another, or a XAML label pointing at a key nobody wrote. Both come
/// out as a missing or marked string in front of the user, and both are cheap to catch here.
/// </summary>
public sealed class LocalizationTests
{
    private static readonly string[] TranslatedCultures = ["pt", "es"];

    [Fact]
    public void EveryTranslationCarriesTheSameKeysAsTheNeutralCatalogue()
    {
        HashSet<string> neutral = ReadResxKeys("Strings.resx");

        foreach (string culture in TranslatedCultures)
        {
            HashSet<string> translated = ReadResxKeys($"Strings.{culture}.resx");

            Assert.Empty(neutral.Except(translated));
            Assert.Empty(translated.Except(neutral));
        }
    }

    [Fact]
    public void TheAccessorAndTheNeutralCatalogueDescribeTheSameKeys()
    {
        HashSet<string> neutral = ReadResxKeys("Strings.resx");

        Assert.Empty(neutral.Except(Strings.Keys));
        Assert.Empty(Strings.Keys.Except(neutral));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("pt-BR")]
    [InlineData("es-ES")]
    [InlineData("pt-PT")]
    [InlineData("es-AR")]
    public void EveryKeyResolvesToRealTextInEveryLanguage(string cultureName)
    {
        // The region variants are in the list on purpose: the catalogue is filed under the
        // neutral cultures, and it is resource fallback that has to carry pt-PT and es-AR.
        CultureInfo previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);

        try
        {
            foreach (string key in Strings.Keys)
            {
                string value = Strings.Get(key);

                Assert.False(string.IsNullOrWhiteSpace(value), key);

                // The marker Get uses for a key it did not find. Seeing it here means the
                // catalogue and the accessor drifted apart.
                Assert.DoesNotContain('!', value);
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact]
    public void EveryKeyUsedInTheXamlExistsInTheCatalogue()
    {
        var referenced = new HashSet<string>(StringComparer.Ordinal);

        foreach (string file in Directory.GetFiles(
            Path.Combine(RepositoryRoot(), "src", "FrisoDock.App"),
            "*.xaml",
            SearchOption.AllDirectories))
        {
            foreach (Match match in Regex.Matches(File.ReadAllText(file), @"\{loc:Tr\s+([A-Za-z0-9_]+)\s*\}"))
            {
                referenced.Add(match.Groups[1].Value);
            }
        }

        // A catalogue that no screen reads would make the check above pass by being empty.
        Assert.NotEmpty(referenced);
        Assert.Empty(referenced.Except(Strings.Keys));
    }

    [Fact]
    public void TranslationsShipUnderTheNeutralCulturesSoTheRegionVariantsFallBack()
    {
        Assert.Equal("pt-BR", AppLanguage.PortugueseBrazil.ToCultureName());
        Assert.Equal("en-US", AppLanguage.English.ToCultureName());
        Assert.Equal("es-ES", AppLanguage.Spanish.ToCultureName());

        // Following the system is the absence of a choice, not a culture of its own.
        Assert.Null(AppLanguage.System.ToCultureName());
    }

    [Fact]
    public void FollowingTheSystemResolvesToTheInstalledLanguage()
    {
        Assert.Equal(CultureInfo.InstalledUICulture, LanguageService.Resolve(AppLanguage.System));
        Assert.Equal("pt-BR", LanguageService.Resolve(AppLanguage.PortugueseBrazil).Name);
    }

    [Fact]
    public void ChoosingALanguageDoesNotTouchTheRegionalFormat()
    {
        // The two settings are independent: someone running Windows in English with the Brazilian
        // format has to get an English dock still showing 28/08/2026.
        CultureInfo format = CultureInfo.CurrentCulture;
        CultureInfo previousUi = CultureInfo.CurrentUICulture;

        try
        {
            LanguageService.Apply(AppLanguage.Spanish);

            Assert.Equal("es-ES", CultureInfo.CurrentUICulture.Name);
            Assert.Equal(format, CultureInfo.CurrentCulture);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.DefaultThreadCurrentUICulture = null;
        }
    }

    private static HashSet<string> ReadResxKeys(string fileName)
    {
        string path = Path.Combine(RepositoryRoot(), "src", "FrisoDock.Core", "Resources", fileName);

        return XDocument.Load(path)
            .Root!
            .Elements("data")
            .Select(data => data.Attribute("name")!.Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The resx and the XAML are sources, not build output: they are read from the checkout, by
    /// walking up from the test assembly until the solution file shows up.
    /// </summary>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FrisoDock.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("FrisoDock.sln not found above the test assembly.");
    }
}
