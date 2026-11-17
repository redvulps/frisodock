using FrisoDock.Core.Services;

namespace FrisoDock.Tests;

public sealed class BoundedCacheTests
{
    [Fact]
    public async Task Clear_DuringLoad_DoesNotRepublishTheOldValue()
    {
        var cache = new BoundedCache<string, string>(2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        Task<string> old = Task.Run(() => cache.GetOrAdd("app", () =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            return "old";
        }));

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cache.Clear();
            Assert.Equal("new", cache.GetOrAdd("app", () => "new"));
        }
        finally
        {
            release.Set();
        }

        Assert.Equal("old", await old);
        Assert.Equal("new", cache.GetOrAdd("app", () => throw new Exception("Reloaded a current value")));
    }

    [Fact]
    public async Task ConcurrentRequests_ShareOneLoad()
    {
        var cache = new BoundedCache<string, int>(2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        int loads = 0;
        Task<int>[] requests = Enumerable.Range(0, 8).Select(_ => Task.Run(() => cache.GetOrAdd("app", () =>
        {
            Interlocked.Increment(ref loads);
            entered.TrySetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            return 42;
        }))).ToArray();

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            release.Set();
        }

        Assert.All(await Task.WhenAll(requests), value => Assert.Equal(42, value));
        Assert.Equal(1, loads);
    }

    [Fact]
    public void Capacity_EvictsTheLeastRecentlyUsedEntry()
    {
        var cache = new BoundedCache<string, int>(2);
        cache.GetOrAdd("a", () => 1);
        cache.GetOrAdd("b", () => 2);
        Assert.Equal(1, cache.GetOrAdd("a", () => 99));
        cache.GetOrAdd("c", () => 3);

        Assert.Equal(1, cache.GetOrAdd("a", () => 99));
        Assert.Equal(20, cache.GetOrAdd("b", () => 20));
    }

    [Fact]
    public void FailedLoad_CanBeRetried()
    {
        var cache = new BoundedCache<string, int>(1);
        Assert.Throws<IOException>(() => cache.GetOrAdd("app", () => throw new IOException()));
        Assert.Equal(42, cache.GetOrAdd("app", () => 42));
    }

    [Fact]
    public async Task FailureFromDetachedLoad_DoesNotRemoveItsReplacement()
    {
        var cache = new BoundedCache<string, int>(1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        Task<int> old = Task.Run(() => cache.GetOrAdd("app", () =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5)));
            throw new IOException();
        }));

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cache.Clear();
            Assert.Equal(42, cache.GetOrAdd("app", () => 42));
        }
        finally
        {
            release.Set();
        }

        await Assert.ThrowsAsync<IOException>(async () => await old);
        Assert.Equal(42, cache.GetOrAdd("app", () => 99));
    }

    [Fact]
    public void NullValues_AreCachedAndKeysUseTheConfiguredComparer()
    {
        var cache = new BoundedCache<string, string?>(2, StringComparer.OrdinalIgnoreCase);
        cache.GetOrAdd("APP", () => null);
        Assert.Null(cache.GetOrAdd("app", () => "unexpected reload"));
    }
}
