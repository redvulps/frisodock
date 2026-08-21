using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The taskbar state record is what keeps the dock from "learning" the autohide it left behind
/// itself after an abnormal shutdown.
/// </summary>
public sealed class FileTaskbarStateStoreTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"dockin-test-{Guid.NewGuid():N}");

    [Fact]
    public void Load_WithNoFile_ReturnsNull()
    {
        ITaskbarStateStore store = CreateStore();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_ReturnsTheSameState()
    {
        ITaskbarStateStore store = CreateStore();

        store.Save(2);

        Assert.Equal(2, store.Load());
    }

    [Fact]
    public void Clear_ErasesTheState()
    {
        ITaskbarStateStore store = CreateStore();
        store.Save(2);

        store.Clear();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Clear_WithNoFile_DoesNotThrow()
    {
        ITaskbarStateStore store = CreateStore();

        store.Clear();

        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_WithInvalidContent_ReturnsNull()
    {
        // A hand-edited, broken file must not bring startup down.
        File.WriteAllText(_filePath, "isto não é um número");

        Assert.Null(CreateStore().Load());
    }

    [Fact]
    public void Save_OverwritesThePreviousValue()
    {
        ITaskbarStateStore store = CreateStore();

        store.Save(2);
        store.Save(0);

        Assert.Equal(0, store.Load());
    }

    [Fact]
    public void State_SurvivesANewInstance()
    {
        // It is the real scenario: the process died and another starts from scratch.
        CreateStore().Save(2);

        Assert.Equal(2, CreateStore().Load());
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }

    private FileTaskbarStateStore CreateStore()
    {
        return new FileTaskbarStateStore(_filePath);
    }
}
