using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The magnification effect's curve. It is what gives the macOS dock's "wave" feel, and it is a
/// pure function — its shape can be checked without opening any window.
/// </summary>
public sealed class MagnificationCurveTests
{
    private const double IconSize = 44;
    private const double Magnification = 1.5;

    private readonly MagnificationCurve _curve = new();

    [Fact]
    public void UnderTheCursor_AppliesFullMagnification()
    {
        Assert.Equal(Magnification, _curve.CalculateScale(0, IconSize, Magnification), precision: 6);
    }

    [Fact]
    public void OutOfReach_DoesNotMagnify()
    {
        // The reach is 2.5 icons to each side.
        Assert.Equal(1.0, _curve.CalculateScale(IconSize * 3, IconSize, Magnification), precision: 6);
    }

    [Fact]
    public void NeighboursGrowLessAsTheyMoveAway()
    {
        double vizinho = _curve.CalculateScale(IconSize, IconSize, Magnification);
        double distante = _curve.CalculateScale(IconSize * 2, IconSize, Magnification);

        Assert.True(vizinho > distante);
        Assert.True(distante > 1.0);
        Assert.True(vizinho < Magnification);
    }

    [Fact]
    public void TheCurveIsSymmetric()
    {
        double esquerda = _curve.CalculateScale(-IconSize, IconSize, Magnification);
        double direita = _curve.CalculateScale(IconSize, IconSize, Magnification);

        Assert.Equal(esquerda, direita, precision: 6);
    }

    [Fact]
    public void TheCurveNeverLeavesTheRange()
    {
        for (double distance = 0; distance <= IconSize * 4; distance += 2)
        {
            double scale = _curve.CalculateScale(distance, IconSize, Magnification);

            Assert.InRange(scale, 1.0, Magnification);
        }
    }

    [Fact]
    public void TheCurveIsMonotonic()
    {
        // No oscillation: an icon never grows as it moves away from the cursor.
        double previous = _curve.CalculateScale(0, IconSize, Magnification);

        for (double distance = 1; distance <= IconSize * 3; distance += 1)
        {
            double scale = _curve.CalculateScale(distance, IconSize, Magnification);

            Assert.True(scale <= previous + 1e-9, $"cresceu em {distance}px");
            previous = scale;
        }
    }

    [Fact]
    public void AtTheEdgeOfTheReach_ArrivesSmoothlyAtOne()
    {
        // Slope near zero at the end: it is what avoids the visible jump when the icon
        // enters and leaves the reach.
        double range = IconSize * 2.5;
        double antes = _curve.CalculateScale(range - 1, IconSize, Magnification);

        Assert.InRange(antes - 1.0, 0, 0.001);
    }

    [Fact]
    public void MagnificationOff_ChangesNothing()
    {
        Assert.Equal(1.0, _curve.CalculateScale(0, IconSize, magnification: 1.0), precision: 6);
    }

    [Fact]
    public void InvalidIconSize_DoesNotBreak()
    {
        Assert.Equal(1.0, _curve.CalculateScale(0, iconSize: 0, Magnification), precision: 6);
    }
}
