using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Regression: <see cref="DockMetrics"/> used to be a record struct, and in that shape
/// <c>new DockMetrics()</c> ignored the primary constructor and zeroed every field.
/// The dock was created 0 px in size and stayed invisible. The layout tests did not catch it
/// because they all passed explicit metrics — these exercise precisely the default.
/// </summary>
public sealed class DockMetricsTests
{
    [Fact]
    public void Default_HasUsableMetrics()
    {
        DockMetrics metrics = DockMetrics.Default;

        Assert.True(metrics.IconSize > 0);
        Assert.True(metrics.Padding > 0);
        Assert.True(metrics.EdgeMargin > 0);
        Assert.True(metrics.ItemSpacing > 0);
    }

    [Fact]
    public void ParameterlessConstructor_UsesTheSameValuesAsDefault()
    {
        Assert.Equal(DockMetrics.Default, new DockMetrics());
    }

    [Fact]
    public void PanelThickness_AddsIconPaddingAndBorders()
    {
        var metrics = new DockMetrics(IconSize: 40, Padding: 10, BorderThickness: 1);

        Assert.Equal(40 + (10 * 2) + (1 * 2), metrics.PanelThickness);
    }

    [Fact]
    public void ReservedThickness_IsThePanelPlusTheEdgeMargin()
    {
        var metrics = new DockMetrics(IconSize: 40, Padding: 10, EdgeMargin: 5, BorderThickness: 1);

        Assert.Equal(metrics.PanelThickness + 5, metrics.ReservedThickness);
    }

    [Fact]
    public void IconImageSize_FitsInsideTheButtonWithRoomForTheHover()
    {
        DockMetrics metrics = DockMetrics.Default;

        Assert.True(metrics.IconImageSize > 0);
        Assert.True(metrics.IconImageSize < metrics.IconSize);
    }

    [Fact]
    public void ContentPadding_AppliesEquallyToIconGroupAndClock()
    {
        // The padding is a single one: if an element starts computing its own outside, the dock goes back to
        // having one tight highlight and one loose highlight side by side.
        var metrics = new DockMetrics(ContentPadding: 7);

        Assert.Equal(metrics.IconSize - 14, metrics.IconImageSize);
        Assert.Equal(metrics.QuickStatusContentWidth + 14, metrics.QuickStatusWidth);
        Assert.Equal(metrics.ClockContentWidth + 14, metrics.ClockWidth);
        Assert.Equal(metrics.ClockContentWidthWithSeconds + 14, metrics.ClockWidthWithSeconds);
    }

    [Fact]
    public void IconImageSize_DidNotChangeWhenMovingFromRatioToPadding()
    {
        // It was IconSize * 0.73. Swapping it for IconSize - 2 * ContentPadding must not change the icon.
        Assert.Equal(32, DockMetrics.Default.IconImageSize);
    }

    [Fact]
    public void LayoutWithDefaultMetrics_ProducesAVisibleDock()
    {
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var monitor = new MonitorInfo(bounds, bounds, IsPrimary: true, DpiScale: 1.0);
        var calculator = new DockLayoutCalculator();

        PixelRect panel = calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 5, DockMetrics.Default);
        PixelRect reservation = calculator.CalculateReservationRect(monitor, DockEdge.Bottom, DockMetrics.Default);

        Assert.True(panel.Width > 0);
        Assert.True(panel.Height > 0);
        Assert.True(reservation.Height > 0);

        // And inside the screen, not stuck to the bottom edge.
        Assert.True(panel.Bottom < bounds.Bottom);
        Assert.True(panel.Top > 0);
    }

    [Fact]
    public void HorizontalPanel_HasTheThicknessOfAnIcon()
    {
        var settings = new DockSettings { Edge = DockEdge.Bottom };
        DockMetrics metrics = settings.EffectiveMetrics;

        Assert.Equal(metrics.IconSize, metrics.ContentThickness);
    }

    [Fact]
    public void VerticalPanel_WithTheDateFittingTheMargin_DoesNotThicken()
    {
        // "25/08/2026" measures 56 px in the date font, against an icon's 44 px. The missing 12 px
        // come from the panel's inner margin, not from its thickness.
        var vertical = new DockSettings { Edge = DockEdge.Left, ShowClockSeconds = false }.EffectiveMetrics;
        var horizontal = new DockSettings { Edge = DockEdge.Bottom, ShowClockSeconds = false }.EffectiveMetrics;

        Assert.Equal(horizontal.PanelThickness, vertical.PanelThickness);
        Assert.Equal(vertical.ClockContentWidth, vertical.VerticalClockWidth);
    }

    [Fact]
    public void VerticalPanel_WithSeconds_ThickensOnlyByWhatDidNotFitTheMargin()
    {
        var withSeconds = new DockSettings { Edge = DockEdge.Right, ShowClockSeconds = true }.EffectiveMetrics;
        var without = new DockSettings { Edge = DockEdge.Right, ShowClockSeconds = false }.EffectiveMetrics;

        // The time with seconds still fits whole, and the panel grows only by the difference.
        Assert.Equal(withSeconds.ClockContentWidth, withSeconds.VerticalClockWidth);
        Assert.True(withSeconds.PanelThickness > without.PanelThickness);
        Assert.Equal(
            withSeconds.ClockContentWidth - (withSeconds.VerticalClockBleed * 2),
            withSeconds.ContentThickness);
    }

    [Fact]
    public void VerticalPanel_IsNeverThinnerThanAnIcon()
    {
        // A narrow clock must not squeeze the icons, which keep their full side.
        var metrics = new DockMetrics(ClockContentWidth: 10, ClockContentWidthWithSeconds: 12);
        var settings = new DockSettings { Edge = DockEdge.Left, Metrics = metrics };

        Assert.Equal(metrics.IconSize, settings.EffectiveMetrics.ContentThickness);
    }
}
