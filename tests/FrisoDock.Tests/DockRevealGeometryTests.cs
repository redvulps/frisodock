using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Where the window goes when the dock hides. What matters is the sliver: without it there is no
/// way to call the dock back without a global mouse hook, which the architecture forbids.
/// </summary>
public sealed class DockRevealGeometryTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

    private readonly DockLayoutCalculator _calculator = new();
    private readonly DockMetrics _metrics = DockMetrics.Default;

    [Fact]
    public void FullyInView_DoesNotMoveTheWindow()
    {
        PixelRect window = Window();

        Assert.Equal(window, _calculator.ApplyReveal(window, DockEdge.Bottom, _metrics, 1.0, 1.0));
    }

    [Fact]
    public void Hidden_LeavesExactlyTheSliverOnScreen()
    {
        PixelRect window = Window();
        PixelRect hidden = _calculator.ApplyReveal(window, DockEdge.Bottom, _metrics, 0.0, 1.0);

        // The window has the magnification headroom above the panel; the sliver is measured on the panel.
        int panelTop = hidden.Bottom - _metrics.PanelThickness;

        Assert.Equal(Screen.Bottom - _metrics.HiddenSliver, panelTop);
    }

    [Fact]
    public void Hidden_DoesNotChangeTheWindowSize()
    {
        PixelRect window = Window();
        PixelRect hidden = _calculator.ApplyReveal(window, DockEdge.Bottom, _metrics, 0.0, 1.0);

        Assert.Equal(window.Width, hidden.Width);
        Assert.Equal(window.Height, hidden.Height);
    }

    [Fact]
    public void Midway_SitsBetweenTheTwoPositions()
    {
        PixelRect window = Window();
        PixelRect meio = _calculator.ApplyReveal(window, DockEdge.Bottom, _metrics, 0.5, 1.0);

        Assert.Equal(window.Top + (_metrics.HiddenDistance / 2), meio.Top);
    }

    [Theory]
    [InlineData(DockEdge.Bottom, 0, 1)]
    [InlineData(DockEdge.Top, 0, -1)]
    [InlineData(DockEdge.Left, -1, 0)]
    [InlineData(DockEdge.Right, 1, 0)]
    public void TheDockLeavesThroughTheEdgeItIsAnchoredTo(DockEdge edge, int directionX, int directionY)
    {
        PixelRect window = Window();
        PixelRect hidden = _calculator.ApplyReveal(window, edge, _metrics, 0.0, 1.0);

        Assert.Equal(window.Left + (directionX * _metrics.HiddenDistance), hidden.Left);
        Assert.Equal(window.Top + (directionY * _metrics.HiddenDistance), hidden.Top);
    }

    [Fact]
    public void TheDistanceFollowsTheDpi()
    {
        PixelRect window = Window();
        PixelRect hidden = _calculator.ApplyReveal(window, DockEdge.Bottom, _metrics, 0.0, 1.5);

        Assert.Equal((int)Math.Round(_metrics.HiddenDistance * 1.5), hidden.Top - window.Top);
    }

    [Fact]
    public void HidingTheDock_DropsTheSpaceReservation()
    {
        // Reserving the band and hiding the dock would leave an empty strip on screen — the worst of both.
        var settings = new DockSettings { ReserveScreenSpace = true };

        Assert.True(settings.ReservesScreenSpace);
        Assert.False((settings with { HideMode = DockHideMode.Always }).ReservesScreenSpace);
        Assert.False((settings with { HideMode = DockHideMode.WhenWindowOverlaps }).ReservesScreenSpace);
    }

    [Fact]
    public void TheRevealingZone_IsOnlyTheSliverAtTheEdge()
    {
        PixelRect panel = Panel();
        PixelRect zone = _calculator.CalculateRevealZone(panel, Monitor(), DockEdge.Bottom, _metrics);

        Assert.Equal(Screen.Bottom - _metrics.HiddenSliver, zone.Top);
        Assert.Equal(Screen.Bottom, zone.Bottom);

        // In the panel's width: touching the edge far from the dock must not call it.
        Assert.Equal(panel.Left, zone.Left);
        Assert.Equal(panel.Right, zone.Right);
    }

    [Fact]
    public void TheHoldingZone_ReachesTheScreenEdge()
    {
        // It is the gap between panel and edge that caused the oscillation: once revealed, the dock rose and
        // left the cursor outside it, down there.
        PixelRect window = Window();
        PixelRect zone = _calculator.CalculateHoverZone(window, Monitor(), DockEdge.Bottom);

        Assert.Equal(Screen.Bottom, zone.Bottom);
        Assert.True(zone.Bottom > window.Bottom);
        Assert.Equal(window.Top, zone.Top);
    }

    [Fact]
    public void TheHoldingZoneContainsTheRevealingOne()
    {
        // Without this the revealed dock would hide the very instant it was called.
        PixelRect reveal = _calculator.CalculateRevealZone(Panel(), Monitor(), DockEdge.Bottom, _metrics);
        PixelRect hover = _calculator.CalculateHoverZone(Window(), Monitor(), DockEdge.Bottom);

        Assert.True(reveal.Left >= hover.Left);
        Assert.True(reveal.Right <= hover.Right);
        Assert.True(reveal.Top >= hover.Top);
        Assert.True(reveal.Bottom <= hover.Bottom);
    }

    [Fact]
    public void TheCursorOnTheLastScreenLine_IsInBothZones()
    {
        var cursor = new PixelPoint(960, Screen.Bottom - 1);

        Assert.True(_calculator.CalculateRevealZone(Panel(), Monitor(), DockEdge.Bottom, _metrics).Contains(cursor));
        Assert.True(_calculator.CalculateHoverZone(Window(), Monitor(), DockEdge.Bottom).Contains(cursor));
    }

    private static MonitorInfo Monitor()
    {
        return new MonitorInfo(Screen, Screen, IsPrimary: true, DpiScale: 1.0);
    }

    private PixelRect Panel()
    {
        return _calculator.CalculatePanelRect(Monitor(), DockEdge.Bottom, 5, _metrics);
    }

    private PixelRect Window()
    {
        return _calculator.CalculateWindowRect(Monitor(), DockEdge.Bottom, 5, _metrics, 1.5);
    }
}
