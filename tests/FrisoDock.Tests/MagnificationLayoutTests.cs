using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The rebuild of the icon strip under magnification. What matters here is that the magnified icons
/// do not overlap: scaling without pushing the neighbours left a pile of icons on top of
/// each other, which is the defect that gave rise to this class.
/// </summary>
public sealed class MagnificationLayoutTests
{
    private const double IconSize = 44;
    private const double Spacing = 8;
    private const double Slot = IconSize + Spacing;
    private const int Count = 8;

    private readonly MagnificationLayout _layout = new();

    private static double RestCenter(int index)
    {
        return Spacing + (index * Slot) + (IconSize / 2);
    }

    [Fact]
    public void IsWithinStrip_AppliesFromTheStartToTheEndOfTheStrip()
    {
        Assert.True(_layout.IsWithinStrip(Count, IconSize, Spacing, 0));
        Assert.True(_layout.IsWithinStrip(Count, IconSize, Spacing, RestCenter(0)));
        Assert.True(_layout.IsWithinStrip(Count, IconSize, Spacing, RestCenter(Count - 1)));
        Assert.True(_layout.IsWithinStrip(Count, IconSize, Spacing, Count * Slot));
    }

    [Fact]
    public void IsWithinStrip_DoesNotApplyAfterTheLastIcon()
    {
        // It is where the separator, the tray and the clock sit. The curve's reach gets there, and without
        // this limit a cursor resting on the clock would leave the last icon magnified.
        Assert.False(_layout.IsWithinStrip(Count, IconSize, Spacing, (Count * Slot) + 1));
        Assert.False(_layout.IsWithinStrip(Count, IconSize, Spacing, -1));
    }

    [Fact]
    public void IsWithinStrip_WithNoIconsAppliesNowhere()
    {
        Assert.False(_layout.IsWithinStrip(0, IconSize, Spacing, 0));
    }

    [Theory]
    [InlineData(1.2)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void IconsNeverOverlap(double magnification)
    {
        // It sweeps the whole strip, half a pixel at a time: the overlap appeared precisely at the
        // intermediate positions, between two icons.
        for (double cursorX = 0; cursorX <= Count * Slot; cursorX += 0.5)
        {
            MagnificationLayoutResult result = _layout.Calculate(Count, IconSize, Spacing, cursorX, magnification);

            for (int index = 0; index + 1 < Count; index++)
            {
                double right = Edge(result, index, +1);
                double left = Edge(result, index + 1, -1);

                Assert.True(right <= left + 1e-9, $"ícones {index} e {index + 1} se sobrepõem em {cursorX}px");
            }
        }
    }

    [Fact]
    public void TheStripGrowsExactlyWhatTheIconsGrew()
    {
        MagnificationLayoutResult result = _layout.Calculate(Count, IconSize, Spacing, RestCenter(3), 2.0);

        double growth = result.Items.Sum(item => (item.Scale - 1.0) * IconSize);

        Assert.Equal(growth, result.ExtraWidth, precision: 6);
    }

    [Fact]
    public void TheIconUnderTheCursorAtTheStripCentre_DoesNotSlide()
    {
        // With the cursor at the centre of the strip the growth is symmetric, so the icon under the cursor
        // stays where it is — it is what avoids the feeling of the dock running from the mouse.
        MagnificationLayoutResult result = _layout.Calculate(9, IconSize, Spacing, RestCenter(4), 2.0);

        Assert.Equal(0, ScreenOffset(result, 4), precision: 6);
    }

    [Fact]
    public void TheNeighboursArePushedToOppositeSides()
    {
        MagnificationLayoutResult result = _layout.Calculate(9, IconSize, Spacing, RestCenter(4), 2.0);

        Assert.True(ScreenOffset(result, 3) < 0);
        Assert.True(ScreenOffset(result, 5) > 0);
    }

    [Fact]
    public void WithoutMagnification_NothingMoves()
    {
        MagnificationLayoutResult result = _layout.Calculate(Count, IconSize, Spacing, RestCenter(2), 1.0);

        Assert.Equal(0, result.ExtraWidth);
        Assert.All(result.Items, item => Assert.Equal(1.0, item.Scale));
        Assert.All(result.Items, item => Assert.Equal(0, item.OffsetX));
    }

    [Fact]
    public void WithNoItems_DoesNotBreak()
    {
        MagnificationLayoutResult result = _layout.Calculate(0, IconSize, Spacing, cursorX: 10, magnification: 2.0);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.ExtraWidth);
    }

    [Fact]
    public void TheMaximumCoversAnyCursorPosition()
    {
        // This is the calculation that sizes the window's side headroom: if it underestimates, the panel
        // touches the window edge and the effect shows up clipped.
        double max = _layout.CalculateMaxExtraWidth(Count, IconSize, Spacing, 2.0);

        for (double cursorX = 0; cursorX <= Count * Slot; cursorX += 0.5)
        {
            double extra = _layout.Calculate(Count, IconSize, Spacing, cursorX, 2.0).ExtraWidth;

            // The maximum comes from fine sampling; the sub-pixel error disappears in the rounding up
            // that DockMetrics applies.
            Assert.True(extra <= max + 0.01, $"folga insuficiente em {cursorX}px");
        }
    }

    [Fact]
    public void TheMaximumGrowsWithTheStrength()
    {
        double suave = _layout.CalculateMaxExtraWidth(Count, IconSize, Spacing, 1.2);
        double forte = _layout.CalculateMaxExtraWidth(Count, IconSize, Spacing, 2.0);

        Assert.True(forte > suave);
        Assert.Equal(0, _layout.CalculateMaxExtraWidth(Count, IconSize, Spacing, 1.0));
    }

    /// <summary>
    /// The icon's offset in screen coordinates. The strip is rebuilt from left to
    /// right, but it is itself centred in the panel: the effect the user sees is the
    /// difference between the two.
    /// </summary>
    private static double ScreenOffset(MagnificationLayoutResult result, int index)
    {
        return result.Items[index].OffsetX - (result.ExtraWidth / 2);
    }

    /// <summary>Edge of the already magnified and offset icon. <paramref name="side"/>: -1 left, +1 right.</summary>
    private static double Edge(MagnificationLayoutResult result, int index, int side)
    {
        MagnifiedItem item = result.Items[index];
        double center = RestCenter(index) + item.OffsetX;

        return center + (side * IconSize * item.Scale / 2);
    }
}
