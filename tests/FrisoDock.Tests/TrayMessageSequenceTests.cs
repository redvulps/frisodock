using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Which messages a tray interaction turns into.
///
/// The failure this guards is the click that does nothing: the app listens to one message and
/// the dock sends another, and there is no error anywhere, just an icon that ignores the user.
/// The shape checked here is the shell's own, cross read against ManagedShell, which hosts the
/// tray the same way.
/// </summary>
public sealed class TrayMessageSequenceTests
{
    private const uint WmLeftDown = 0x0201;
    private const uint WmLeftUp = 0x0202;
    private const uint WmLeftDoubleClick = 0x0203;
    private const uint WmRightDown = 0x0204;
    private const uint WmRightUp = 0x0205;
    private const uint WmMiddleDown = 0x0207;
    private const uint WmMiddleUp = 0x0208;
    private const uint WmContextMenu = 0x007B;
    private const uint NinSelect = 0x0400;

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    public void AnOldAppGetsTheButtonPairAndNothingElse(uint version)
    {
        Assert.Equal([WmLeftDown, WmLeftUp], TrayMessageSequence.For(TrayMouseEvent.LeftClick, version));
        Assert.Equal([WmRightDown, WmRightUp], TrayMessageSequence.For(TrayMouseEvent.RightClick, version));
    }

    [Theory]
    [InlineData(3u)]
    [InlineData(4u)]
    public void FromVersionThreeOnTheModernNotificationComesAfterThePair(uint version)
    {
        // Both halves go out, and that is the point: the app picks which one it listens to and
        // never says which. Sending only NIN_SELECT left the apps that wait for the release deaf.
        Assert.Equal(
            [WmLeftDown, WmLeftUp, NinSelect],
            TrayMessageSequence.For(TrayMouseEvent.LeftClick, version));

        Assert.Equal(
            [WmRightDown, WmRightUp, WmContextMenu],
            TrayMessageSequence.For(TrayMouseEvent.RightClick, version));
    }

    [Fact]
    public void TheReleaseIsNeverDropped()
    {
        // The regression this exists for: version 4 used to get the press and NIN_SELECT, with
        // WM_LBUTTONUP missing, so an app reacting to the release did nothing at all.
        foreach (uint version in new uint[] { 0, 3, 4 })
        {
            Assert.Contains(WmLeftUp, TrayMessageSequence.For(TrayMouseEvent.LeftClick, version));
            Assert.Contains(WmLeftUp, TrayMessageSequence.For(TrayMouseEvent.LeftDoubleClick, version));
            Assert.Contains(WmRightUp, TrayMessageSequence.For(TrayMouseEvent.RightClick, version));
            Assert.Contains(WmMiddleUp, TrayMessageSequence.For(TrayMouseEvent.MiddleClick, version));
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(4u)]
    public void TheDoubleClickReplacesThePressAndNotTheRelease(uint version)
    {
        IReadOnlyList<uint> messages = TrayMessageSequence.For(TrayMouseEvent.LeftDoubleClick, version);

        Assert.Equal(WmLeftDoubleClick, messages[0]);
        Assert.Equal(WmLeftUp, messages[1]);
        Assert.DoesNotContain(WmLeftDown, messages);
    }

    [Fact]
    public void TheMiddleClickCarriesItsPress()
    {
        Assert.Equal([WmMiddleDown, WmMiddleUp], TrayMessageSequence.For(TrayMouseEvent.MiddleClick, 4));
    }

    [Fact]
    public void EveryInteractionSaysSomething()
    {
        foreach (TrayMouseEvent mouseEvent in Enum.GetValues<TrayMouseEvent>())
        {
            Assert.NotEmpty(TrayMessageSequence.For(mouseEvent, 4));
            Assert.NotEmpty(TrayMessageSequence.For(mouseEvent, 0));
        }
    }
}
