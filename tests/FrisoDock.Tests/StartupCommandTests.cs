using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Whether a startup entry launches this copy of the dock.
///
/// The failure this guards is a switch that says yes about somebody else's entry: the dock was
/// registered from one folder and now runs from another, the old path no longer starts anything,
/// and the settings screen would still show the option on.
/// </summary>
public sealed class StartupCommandTests
{
    private const string Executable = @"C:\Program Files\FrisoDock\FrisoDock.exe";

    [Fact]
    public void TheCommandIsQuoted()
    {
        // The path can carry spaces, and the shell splits the command line on them.
        Assert.Equal($"\"{Executable}\"", StartupCommand.For(Executable));
    }

    [Fact]
    public void TheCommandItWritesPointsAtTheExecutable()
    {
        Assert.True(StartupCommand.PointsAt(StartupCommand.For(Executable), Executable));
    }

    [Fact]
    public void AnUnquotedCommandStillCounts()
    {
        // Not what the dock writes, but a hand-edited entry is still this install.
        Assert.True(StartupCommand.PointsAt(Executable, Executable));
    }

    [Fact]
    public void TheComparisonIgnoresCaseAndFolderSpelling()
    {
        Assert.True(StartupCommand.PointsAt(@"""c:\program files\frisodock\FrisoDock.exe""", Executable));
        Assert.True(StartupCommand.PointsAt(@"""C:\Program Files\Other\..\FrisoDock\FrisoDock.exe""", Executable));
    }

    [Fact]
    public void AnEntryLeftBehindByAMovedCopyDoesNotCount()
    {
        // The whole point: the value exists, and the logon launches a path that is not here.
        Assert.False(StartupCommand.PointsAt(@"""D:\Old\FrisoDock\FrisoDock.exe""", Executable));
    }

    [Fact]
    public void AnEntryCarryingArgumentsIsNotOurs()
    {
        Assert.False(StartupCommand.PointsAt($"\"{Executable}\" --restore-taskbar", Executable));
    }

    [Fact]
    public void NothingWrittenMeansNotRegistered()
    {
        Assert.False(StartupCommand.PointsAt(null, Executable));
        Assert.False(StartupCommand.PointsAt(string.Empty, Executable));
        Assert.False(StartupCommand.PointsAt("   ", Executable));
        Assert.False(StartupCommand.PointsAt("\"\"", Executable));
    }

    [Fact]
    public void AnUnopenedQuoteIsNotAPath()
    {
        Assert.False(StartupCommand.PointsAt($"\"{Executable}", Executable));
    }
}
