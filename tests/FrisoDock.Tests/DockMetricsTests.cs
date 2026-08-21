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
}
