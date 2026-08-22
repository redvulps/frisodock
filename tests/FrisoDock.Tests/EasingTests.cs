using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The curve shared by the dock's animations: the hide slide and the magnification
/// retraction.
/// </summary>
public sealed class EasingTests
{
    [Fact]
    public void Smoothstep_StartsAtZeroAndEndsAtOne()
    {
        Assert.Equal(0.0, Easing.Smoothstep(0));
        Assert.Equal(1.0, Easing.Smoothstep(1));
    }

    [Fact]
    public void Smoothstep_ClampsValuesOutOfRange()
    {
        // The current frame is divided by the total, and one rounding too many must not become a
        // scale larger than the one requested.
        Assert.Equal(0.0, Easing.Smoothstep(-0.5));
        Assert.Equal(1.0, Easing.Smoothstep(1.5));
    }

    [Fact]
    public void Smoothstep_GrowsWithoutGoingBack()
    {
        double anterior = -1;

        for (int passo = 0; passo <= 20; passo++)
        {
            double atual = Easing.Smoothstep(passo / 20.0);

            Assert.True(atual >= anterior);
            anterior = atual;
        }
    }

    [Fact]
    public void Smoothstep_ArrivesAtTheEndsWithNearlyZeroSlope()
    {
        // It is what separates movement from a jump: linear, the dock starts and stops abruptly.
        double inicio = Easing.Smoothstep(0.05) - Easing.Smoothstep(0.0);
        double meio = Easing.Smoothstep(0.55) - Easing.Smoothstep(0.5);
        double fim = Easing.Smoothstep(1.0) - Easing.Smoothstep(0.95);

        Assert.True(inicio < meio / 3);
        Assert.True(fim < meio / 3);
    }
}
