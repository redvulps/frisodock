using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Tests;

public sealed class QuickStatusConcurrencyTests
{
    [Fact]
    public Task SlowNetwork_DoesNotBlockInputOrOverlapReads_AndVolumeKeepsUpdating() => DispatcherTest.RunAsync(async () =>
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        int worker = 0;
        var hardware = new Hardware
        {
            ReadNetwork = () =>
            {
                worker = Environment.CurrentManagedThreadId;
                Interlocked.Increment(ref reads);
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException();
                }
                return new NetworkStatus("Test network", true);
            },
        };
        using var view = new QuickStatusViewModel(hardware, hardware, hardware);
        Assert.Equal(0, reads);
        int ui = Environment.CurrentManagedThreadId;
        Task refresh = view.RefreshAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            Assert.NotEqual(ui, worker);
            hardware.Volume = new VolumeStatus(75, false, true);
            await view.RefreshAsync();
            Assert.Equal(1, reads);
            Assert.Contains("75", view.Tooltip);
            Assert.False(refresh.IsCompleted);
        }
        finally
        {
            release.Set();
            await refresh;
        }
        Assert.Contains("Test network", view.Tooltip);
    });

    [Fact]
    public Task FailedNetwork_DoesNotDiscardVolumeOrBattery_AndCanRecover() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware { ReadNetwork = () => throw new IOException() };
        using var view = new QuickStatusViewModel(hardware, hardware, hardware);
        await view.RefreshAsync();
        Assert.Contains("60", view.Tooltip);
        Assert.True(view.HasBattery);
        hardware.ReadNetwork = () => new NetworkStatus("Recovered network", true);
        await view.RefreshAsync();
        Assert.Contains("Recovered network", view.Tooltip);
    });

    [Fact]
    public Task DisposedIndicator_DoesNotApplyOutstandingReadsOrStartNewOnes() => DispatcherTest.RunAsync(async () =>
    {
        var volume = new TaskCompletionSource<VolumeStatus>();
        int reads = 0;
        var hardware = new Hardware
        {
            ReadVolume = () =>
            {
                reads++;
                return volume.Task;
            },
        };
        using var view = new QuickStatusViewModel(hardware, hardware, hardware);
        string original = view.VolumeGlyph;
        Task refresh = view.RefreshAsync();
        view.Dispose();
        volume.SetResult(new VolumeStatus(0, true, true));
        await refresh;
        await view.RefreshAsync();
        Assert.Equal(original, view.VolumeGlyph);
        Assert.Equal(1, reads);
    });

    private sealed class Hardware : IVolumeController, IBatteryProvider, INetworkProvider
    {
        public Func<NetworkStatus> ReadNetwork = () => NetworkStatus.Disconnected;
        public Func<Task<VolumeStatus>>? ReadVolume;
        public VolumeStatus Volume = new(60, false, true);
        public Task<VolumeStatus> GetStatusAsync() => ReadVolume?.Invoke() ?? Task.FromResult(Volume);
        BatteryStatus IBatteryProvider.GetStatus() => new(true, 80, true, false);
        NetworkStatus INetworkProvider.GetStatus() => ReadNetwork();
        public Task SetStateAsync(int? level = null, bool? isMuted = null) => throw new InvalidOperationException("Unexpected write");
    }
}
