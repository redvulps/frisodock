using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The dock geometry: panel length, centring, DPI scaling and the band reserved
/// by the appbar.
/// </summary>
public sealed class DockLayoutCalculatorTests
{
    private static readonly DockMetrics Metrics = new(
        IconSize: 40,
        ItemSpacing: 10,
        Padding: 10,
        EdgeMargin: 5,
        BorderThickness: 1,
        SeparatorWidth: 1,
        SeparatorSpacing: 5,
        ContentPadding: 5,
        QuickStatusContentWidth: 40,
        ClockContentWidth: 50);

    // Fixed parts: Start (40) + separator (11) on the left; separator (11) + tray (40)
    // + spacing (10) + network/sound/battery (50) + spacing (10) + clock (60) on the right = 232.
    // Panel borders and inner margins: (10 + 1) * 2 = 22.
    private const int FixedLength = 232;
    private const int PanelChrome = 22;

    // Each app takes spacing + icon = 50.
    private const int PerItemLength = 50;

    // Thickness: icon (40) + 2 paddings (20) + 2 borders (2) = 62.
    private const int PanelThickness = 62;

    private readonly DockLayoutCalculator _calculator = new();

    [Fact]
    public void CalculatePanelLength_IncludesStartSeparatorsTrayClockAndBorders()
    {
        Assert.Equal(FixedLength + PanelChrome, Metrics.CalculatePanelLength(0));
        Assert.Equal(FixedLength + PanelChrome + PerItemLength, Metrics.CalculatePanelLength(1));
        Assert.Equal(FixedLength + PanelChrome + (PerItemLength * 5), Metrics.CalculatePanelLength(5));
    }

    [Fact]
    public void CalculatePanelLength_GrowsLinearlyWithTheItems()
    {
        int comQuatro = Metrics.CalculatePanelLength(4);
        int comCinco = Metrics.CalculatePanelLength(5);

        Assert.Equal(PerItemLength, comCinco - comQuatro);
    }

    [Fact]
    public void CalculatePanelRect_UsesTheLengthThatFitsAllTheContent()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 3, Metrics);

        Assert.Equal(Metrics.CalculatePanelLength(3), panel.Width);
    }

    [Fact]
    public void CalculatePanelRect_CentresHorizontallyOnTheBottomEdge()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 3, Metrics);

        Assert.Equal((1920 - panel.Width) / 2, panel.Left);
    }

    [Fact]
    public void CalculatePanelRect_RespectsTheBottomEdgeMargin()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 3, Metrics);

        Assert.Equal(PanelThickness, panel.Height);
        Assert.Equal(1080 - PanelThickness - 5, panel.Top);
    }

    [Fact]
    public void CalculatePanelRect_AnchorsAtTheTopWhenTheEdgeIsTheTop()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Top, itemCount: 2, Metrics);

        Assert.Equal(5, panel.Top);
    }

    [Fact]
    public void CalculatePanelRect_OnASideEdgeThePanelGrowsVertically()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Left, itemCount: 3, Metrics);

        Assert.Equal(PanelThickness, panel.Width);
        Assert.Equal(Metrics.CalculatePanelLength(3), panel.Height);
        Assert.Equal(5, panel.Left);
    }

    [Fact]
    public void CalculatePanelRect_AppliesTheDpiScale()
    {
        MonitorInfo monitor = CreateMonitor(3840, 2160, dpiScale: 2.0);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 3, Metrics);

        Assert.Equal(Metrics.CalculatePanelLength(3) * 2, panel.Width);
        Assert.Equal(PanelThickness * 2, panel.Height);
    }

    [Fact]
    public void CalculatePanelRect_NeverExceedsTheScreenWidth()
    {
        MonitorInfo monitor = CreateMonitor(800, 600);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 50, Metrics);

        Assert.True(panel.Width <= 800 - (Metrics.EdgeMargin * 2));
        Assert.True(panel.Left >= 0);
    }

    [Fact]
    public void CalculatePanelLength_ReservesTheClockSpace()
    {
        // Without this part the panel would come out narrower than the content and clip the clock.
        var estreito = Metrics with { ClockContentWidth = 0 };

        Assert.Equal(
            Metrics.ClockWidth - estreito.ClockWidth,
            Metrics.CalculatePanelLength(3) - estreito.CalculatePanelLength(3));
    }

    [Fact]
    public void CalculatePanelRect_WithNoItemsStillShowsStart()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 0, Metrics);

        Assert.Equal(FixedLength + PanelChrome, panel.Width);
        Assert.Equal(PanelThickness, panel.Height);
    }

    [Fact]
    public void CalculatePanelRect_RespectsTheSecondaryMonitorOffset()
    {
        var bounds = new PixelRect(1920, 0, 3840, 1080);
        var monitor = new MonitorInfo(bounds, bounds, IsPrimary: false, DpiScale: 1.0);

        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 3, Metrics);

        Assert.Equal(1920 + ((1920 - panel.Width) / 2), panel.Left);
    }

    [Fact]
    public void CalculateReservationRect_TakesTheWholeWidth()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics);

        Assert.Equal(0, reservation.Left);
        Assert.Equal(1920, reservation.Right);
    }

    [Fact]
    public void CalculateReservationRect_ReservesThePanelPlusTheEdgeMargin()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics);

        Assert.Equal(PanelThickness + 5, Metrics.ReservedThickness);
        Assert.Equal(PanelThickness + 5, reservation.Height);
        Assert.Equal(1080, reservation.Bottom);
    }

    [Fact]
    public void CalculateReservationRect_OnASideEdgeReservesAVerticalBand()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Right, Metrics);

        Assert.Equal(Metrics.ReservedThickness, reservation.Width);
        Assert.Equal(1080, reservation.Height);
        Assert.Equal(1920, reservation.Right);
    }

    [Fact]
    public void CalculateReservationRect_DiscountsTheBandTheTaskbarAlreadyReserves()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);
        var taskbar = new PixelRect(0, 1032, 1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics, taskbar);

        // The shell stacks the two bands: by asking only for the difference, the total goes back to the
        // dock's thickness, and nothing disappears behind the hidden taskbar.
        Assert.Equal(Metrics.ReservedThickness - 48, reservation.Height);
    }

    [Fact]
    public void CalculateReservationRect_DoesNotDiscountATaskbarOnAnotherEdge()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);
        var taskbarNaEsquerda = new PixelRect(0, 0, 48, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics, taskbarNaEsquerda);

        Assert.Equal(Metrics.ReservedThickness, reservation.Height);
    }

    [Fact]
    public void CalculateReservationRect_DoesNotDiscountATaskbarOnAnotherMonitor()
    {
        var bounds = new PixelRect(1920, 0, 3840, 1080);
        var monitor = new MonitorInfo(bounds, bounds, IsPrimary: false, DpiScale: 1.0);
        var taskbarNaTelaPrincipal = new PixelRect(0, 1032, 1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics, taskbarNaTelaPrincipal);

        Assert.Equal(Metrics.ReservedThickness, reservation.Height);
    }

    [Fact]
    public void CalculateReservationRect_NativeBandLargerThanTheDocksDoesNotBecomeNegativeThickness()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);
        var taskbarGorda = new PixelRect(0, 900, 1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics, taskbarGorda);

        Assert.Equal(0, reservation.Height);
    }

    [Fact]
    public void CalculateReservationRect_ThePanelFitsInsideTheReservedBand()
    {
        MonitorInfo monitor = CreateMonitor(1920, 1080);

        PixelRect reservation = _calculator.CalculateReservationRect(monitor, DockEdge.Bottom, Metrics);
        PixelRect panel = _calculator.CalculatePanelRect(monitor, DockEdge.Bottom, itemCount: 5, Metrics);

        Assert.True(panel.Top >= reservation.Top);
        Assert.True(panel.Bottom <= reservation.Bottom);
    }

    private static MonitorInfo CreateMonitor(int width, int height, double dpiScale = 1.0)
    {
        var bounds = new PixelRect(0, 0, width, height);
        return new MonitorInfo(bounds, bounds, IsPrimary: true, dpiScale);
    }
}
