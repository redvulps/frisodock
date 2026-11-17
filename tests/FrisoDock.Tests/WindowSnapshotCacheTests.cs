using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.Tests;

public sealed class WindowSnapshotCacheTests
{
    [Fact]
    public void RepeatedMonitorAndAutohideReads_ShareOneEnumeration()
    {
        var source = new Source();
        using var cache = new WindowSnapshotCache(source);
        for (int i = 0; i < 40; i++)
        {
            cache.GetWindows();
        }

        Assert.Equal(1, source.Reads);
    }

    [Fact]
    public void WindowEvent_InvalidatesBeforeNotifyingConsumers()
    {
        var source = new Source();
        using var cache = new WindowSnapshotCache(source);
        cache.GetWindows();
        cache.WindowsChanged += (_, _) => cache.GetWindows();
        source.Change();
        cache.GetWindows();

        Assert.Equal(2, source.Reads);
    }

    [Fact]
    public void Stop_DiscardsTheSnapshot()
    {
        var source = new Source();
        using var cache = new WindowSnapshotCache(source);
        cache.GetWindows();
        cache.Stop();
        cache.Start();
        cache.GetWindows();

        Assert.Equal(2, source.Reads);
    }

    private sealed class Source : IWindowEnumerator
    {
        public int Reads { get; private set; }
        public event EventHandler? WindowsChanged;
        public void Change() => WindowsChanged?.Invoke(this, EventArgs.Empty);
        public IReadOnlyList<WindowInfo> GetWindows()
        {
            Reads++;
            return [];
        }

        public void Start() { }
        public void Stop() { }
    }
}
