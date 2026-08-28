using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The palette derived from the Windows accent color. A pure function: the legibility of the text
/// over the accent can be checked without opening any window.
/// </summary>
public sealed class AccentPalettesTests
{
    /// <summary>The default Windows 11 blue, light variant — the color the dock used hard-coded.</summary>
    private static readonly AccentColor DefaultBlue = new(0x4C, 0xC2, 0xFF);

    /// <summary>The development machine's purple, light variant, read from UISettings.</summary>
    private static readonly AccentColor Purple = new(0xDB, 0x9F, 0xE5);

    [Fact]
    public void Default_IsTheWindowsBlue()
    {
        Assert.Equal(DefaultBlue, AccentPalettes.Default.Accent);
    }

    [Fact]
    public void LightAccent_AsksForDarkText()
    {
        AccentColor foreground = AccentPalettes.ForegroundFor(DefaultBlue);

        Assert.True(AccentPalettes.RelativeLuminance(foreground) < 0.05);
    }

    [Fact]
    public void DarkAccent_AsksForLightText()
    {
        // Navy blue: dark text would disappear on top of it.
        AccentColor foreground = AccentPalettes.ForegroundFor(new AccentColor(0x10, 0x20, 0x60));

        Assert.True(AccentPalettes.RelativeLuminance(foreground) > 0.5);
    }

    /// <summary>
    /// The text is neither flat black nor flat white: it carries the accent's hue, which is what the
    /// native panel does. Over blue, the text's blue channel is still the strongest.
    /// </summary>
    [Fact]
    public void DarkText_KeepsTheAccentHue()
    {
        AccentColor foreground = AccentPalettes.ForegroundFor(DefaultBlue);

        Assert.True(foreground.B > foreground.G);
        Assert.True(foreground.G > foreground.R);
    }

    [Theory]
    [InlineData(0x4C, 0xC2, 0xFF)] // azul padrão
    [InlineData(0xDB, 0x9F, 0xE5)] // roxo
    [InlineData(0x10, 0x20, 0x60)] // azul-marinho
    [InlineData(0x00, 0x00, 0x00)] // preto
    [InlineData(0xFF, 0xFF, 0xFF)] // branco
    [InlineData(0x9B, 0x9B, 0x9B)] // cinza médio, o pior caso para contraste
    public void AnyAccent_YieldsReadableContrast(byte r, byte g, byte b)
    {
        var accent = new AccentColor(r, g, b);
        AccentPalette palette = AccentPalettes.From(accent);

        Assert.True(Contrast(palette.Accent, palette.OnAccent) >= 4.5);
    }

    /// <summary>
    /// The tipping point between light and dark text has to be unique: sweeping from black to
    /// white, the decision changes exactly once. Two flips would signal that the calculation oscillates.
    /// </summary>
    [Fact]
    public void TheFlip_BetweenLightAndDarkText_HappensOnlyOnce()
    {
        var changes = 0;
        bool? previous = null;

        for (var level = 0; level <= 255; level++)
        {
            var accent = new AccentColor((byte)level, (byte)level, (byte)level);
            bool isDarkText = AccentPalettes.RelativeLuminance(AccentPalettes.ForegroundFor(accent)) < 0.2;

            if (previous is not null && previous != isDarkText)
            {
                changes++;
            }

            previous = isDarkText;
        }

        Assert.Equal(1, changes);
    }

    private static double Contrast(AccentColor first, AccentColor second)
    {
        double a = AccentPalettes.RelativeLuminance(first);
        double b = AccentPalettes.RelativeLuminance(second);

        return a > b
            ? (a + 0.05) / (b + 0.05)
            : (b + 0.05) / (a + 0.05);
    }
}
