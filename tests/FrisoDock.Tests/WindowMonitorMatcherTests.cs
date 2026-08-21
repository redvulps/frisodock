using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Which monitor each window is on. It is the rule behind "show only apps present on the
/// monitor", and it is the only part of that which can be checked without a second screen connected.
/// </summary>
public sealed class WindowMonitorMatcherTests
{
    // Two monitors side by side, the second to the right of the first.
    private static readonly PixelRect Esquerdo = new(0, 0, 1920, 1080);
    private static readonly PixelRect Direito = new(1920, 0, 3840, 1080);
    private static readonly PixelRect[] Monitores = [Esquerdo, Direito];

    private readonly WindowMonitorMatcher _matcher = new();

    [Fact]
    public void WindowFullyOnOneMonitor_StaysOnIt()
    {
        Assert.Equal(0, _matcher.FindMonitorIndex(new PixelRect(100, 100, 900, 700), Monitores));
        Assert.Equal(1, _matcher.FindMonitorIndex(new PixelRect(2000, 100, 2800, 700), Monitores));
    }

    [Fact]
    public void WindowBetweenTwoMonitors_GoesToTheOneWithMostOfIt()
    {
        // It starts on the left one and ends on the right one, with most of it on the right.
        var janela = new PixelRect(1820, 100, 2620, 700);

        Assert.Equal(1, _matcher.FindMonitorIndex(janela, Monitores));
    }

    [Fact]
    public void TheTopLeftCornerDoesNotDecide()
    {
        // The corner falls on the left monitor, but only 20 px of the window are there against 780 on the right.
        // A criterion looking at the corner would put this window in the wrong dock.
        var janela = new PixelRect(1900, 100, 2700, 700);

        Assert.True(janela.Left < Direito.Left);
        Assert.Equal(1, _matcher.FindMonitorIndex(janela, Monitores));
    }

    [Fact]
    public void WindowOutsideEveryMonitor_HasNoMonitor()
    {
        Assert.Equal(-1, _matcher.FindMonitorIndex(new PixelRect(-32000, -32000, -31000, -31000), Monitores));
    }

    [Fact]
    public void WindowWithNoBoundsRead_HasNoMonitor()
    {
        Assert.Equal(-1, _matcher.FindMonitorIndex(default, Monitores));
    }

    [Fact]
    public void TheFilterLeavesOnlyTheMonitorsWindows()
    {
        WindowInfo esquerda = Window(1, new PixelRect(100, 100, 900, 700));
        WindowInfo direita = Window(2, new PixelRect(2000, 100, 2800, 700));

        IReadOnlyList<WindowInfo> noEsquerdo = _matcher.ForMonitor([esquerda, direita], 0, Monitores);
        IReadOnlyList<WindowInfo> noDireito = _matcher.ForMonitor([esquerda, direita], 1, Monitores);

        Assert.Equal([esquerda], noEsquerdo);
        Assert.Equal([direita], noDireito);
    }

    [Fact]
    public void MinimizedWindow_AppearsOnEveryMonitor()
    {
        // Win32 does not say where it will come back. Dropping it from every dock would be worse than
        // showing it once too often: the user would lose the way back.
        var minimizada = new WindowInfo(3, "Minimizada", 3, null, IsMinimized: true, false, default);

        Assert.Single(_matcher.ForMonitor([minimizada], 0, Monitores));
        Assert.Single(_matcher.ForMonitor([minimizada], 1, Monitores));
    }

    [Fact]
    public void WithASingleMonitor_EverythingStaysOnIt()
    {
        WindowInfo janela = Window(1, new PixelRect(100, 100, 900, 700));

        Assert.Single(_matcher.ForMonitor([janela], 0, [Esquerdo]));
    }

    [Fact]
    public void IsolationOnlyAppliesWithADockOnEveryMonitor()
    {
        var settings = new DockSettings { IsolateMonitorApps = true };

        Assert.False(settings.IsolatesMonitorApps);
        Assert.True((settings with { ShowOnAllMonitors = true }).IsolatesMonitorApps);
    }

    private static WindowInfo Window(nint handle, PixelRect bounds)
    {
        return new WindowInfo(handle, $"Janela {handle}", (int)handle, null, false, false, bounds);
    }
}
