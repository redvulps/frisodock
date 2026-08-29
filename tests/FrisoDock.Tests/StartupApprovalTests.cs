using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Reading of what the Windows "Startup apps" screen wrote about the dock's entry.
///
/// The failure this guards is silent in both directions: reading a disabled entry as enabled
/// leaves the switch promising a startup that never happens, and reading a missing value as
/// disabled turns the switch off on a machine where nobody ever touched that screen.
/// </summary>
public sealed class StartupApprovalTests
{
    [Fact]
    public void AnEntryNobodyDisabledIsApproved()
    {
        // Measured on this machine, OneDrive enabled.
        byte[] value = [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        Assert.True(StartupApproval.IsApproved(value));
    }

    [Fact]
    public void AnEntryTheUserDisabledIsNotApproved()
    {
        // Measured on this machine, Steam disabled. The eight bytes after the state are the
        // FILETIME of the moment it was turned off.
        byte[] value = [0x03, 0, 0, 0, 0x61, 0xDE, 0x90, 0xB0, 0x24, 0xBA, 0xDC, 0x01];

        Assert.False(StartupApproval.IsApproved(value));
    }

    [Theory]
    [InlineData((byte)0x02)]
    [InlineData((byte)0x06)]
    public void TheEvenStatesAreEnabled(byte state)
    {
        Assert.True(StartupApproval.IsApproved([state, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Theory]
    [InlineData((byte)0x03)]
    [InlineData((byte)0x07)]
    public void TheOddStatesAreDisabled(byte state)
    {
        Assert.False(StartupApproval.IsApproved([state, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void NoValueMeansApproved()
    {
        // Windows only writes here on the first toggle, so most machines have nothing to read.
        Assert.True(StartupApproval.IsApproved(null));
        Assert.True(StartupApproval.IsApproved([]));
    }

    [Fact]
    public void ATruncatedValueStillCarriesTheState()
    {
        Assert.False(StartupApproval.IsApproved([0x03]));
        Assert.True(StartupApproval.IsApproved([0x02, 0, 0]));
    }

    [Fact]
    public void TheApprovedValueClearsAPreviousDisable()
    {
        byte[] value = StartupApproval.Approved();

        Assert.Equal(12, value.Length);
        Assert.True(StartupApproval.IsApproved(value));

        // The disable timestamp has to go with it: leaving it behind would keep the entry
        // looking like one Windows turned off.
        Assert.All(value[1..], b => Assert.Equal(0, b));
    }
}
