using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// When the dock appears and when it goes. It is a pure business decision — the controller in the UI
/// only handles timing and animation.
/// </summary>
public sealed class DockVisibilityPolicyTests
{
    private static readonly PixelRect DockArea = new(500, 1000, 1400, 1070);

    private readonly DockVisibilityPolicy _policy = new();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void AlwaysVisible_IgnoresWindowsAndCursor(bool pointerOver, bool overlaps)
    {
        var input = new DockVisibilityInput(DockHideMode.Never, pointerOver, false, overlaps);

        Assert.True(_policy.ShouldReveal(input));
    }

    [Fact]
    public void Autohide_HidesWhenTheCursorLeaves()
    {
        Assert.False(_policy.ShouldReveal(new DockVisibilityInput(DockHideMode.Always, false, false, false)));
    }

    [Fact]
    public void Autohide_AppearsWithTheCursorOnIt()
    {
        Assert.True(_policy.ShouldReveal(new DockVisibilityInput(DockHideMode.Always, true, false, false)));
    }

    [Fact]
    public void AnOpenPanel_HoldsTheDockOnScreen()
    {
        // The jump list and the thumbnails are positioned relative to the panel: hiding the dock
        // with one of them open would leave the panel floating alone.
        var input = new DockVisibilityInput(DockHideMode.Always, PointerOverDock: false, HasOpenFlyout: true, false);

        Assert.True(_policy.ShouldReveal(input));
    }

    [Fact]
    public void Intellihide_WithTheAreaFree_TheDockStays()
    {
        var input = new DockVisibilityInput(DockHideMode.WhenWindowOverlaps, false, false, WindowOverlapsDock: false);

        Assert.True(_policy.ShouldReveal(input));
    }

    [Fact]
    public void Intellihide_WithAWindowInTheDocksPlace_TheDockHides()
    {
        var input = new DockVisibilityInput(DockHideMode.WhenWindowOverlaps, false, false, WindowOverlapsDock: true);

        Assert.False(_policy.ShouldReveal(input));
    }

    [Fact]
    public void Intellihide_TheCursorBeatsTheWindow()
    {
        var input = new DockVisibilityInput(DockHideMode.WhenWindowOverlaps, true, false, true);

        Assert.True(_policy.ShouldReveal(input));
    }

    [Fact]
    public void WindowOverTheDock_Counts()
    {
        WindowInfo window = CreateWindow(new PixelRect(300, 900, 1600, 1040));

        Assert.True(_policy.AnyWindowOverlaps([window], DockArea));
    }

    [Fact]
    public void WindowFarFromTheDock_DoesNotCount()
    {
        WindowInfo window = CreateWindow(new PixelRect(0, 0, 800, 600));

        Assert.False(_policy.AnyWindowOverlaps([window], DockArea));
    }

    [Fact]
    public void WindowThatOnlyTouchesTheDock_DoesNotCount()
    {
        // Exclusive edges: a window ending exactly where the dock begins does not cover it.
        WindowInfo window = CreateWindow(new PixelRect(500, 400, 1400, 1000));

        Assert.False(_policy.AnyWindowOverlaps([window], DockArea));
    }

    [Fact]
    public void MinimizedWindow_DoesNotCount()
    {
        // Win32 still returns a rectangle for minimized windows, and it may land
        // on the dock. Counting it would hide the dock because of a window nobody sees.
        var window = new WindowInfo(1, "Minimizada", 1, null, IsMinimized: true, false, DockArea);

        Assert.False(_policy.AnyWindowOverlaps([window], DockArea));
    }

    [Fact]
    public void WindowWithNoBoundsRead_DoesNotCount()
    {
        WindowInfo window = CreateWindow(default);

        Assert.False(_policy.AnyWindowOverlaps([window], DockArea));
    }

    [Fact]
    public void OneWindowIsEnough()
    {
        WindowInfo longe = CreateWindow(new PixelRect(0, 0, 200, 200));
        WindowInfo emCima = CreateWindow(new PixelRect(600, 1010, 900, 1500));

        Assert.True(_policy.AnyWindowOverlaps([longe, emCima], DockArea));
    }

    private static WindowInfo CreateWindow(PixelRect bounds)
    {
        return new WindowInfo(1, "Janela", 1, null, IsMinimized: false, IsForeground: false, bounds);
    }
}
