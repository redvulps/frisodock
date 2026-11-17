using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.Tests;

public sealed class QuickSettingsConcurrencyTests
{
    [Fact]
    public Task Refresh_DoesNotBlockTheDispatcherOrOverlapAnotherRead() => DispatcherTest.RunAsync(async () =>
    {
        var brightness = new TaskCompletionSource<BrightnessStatus>();
        var hardware = new Hardware { ReadBrightness = () => brightness.Task };
        using QuickSettingsViewModel view = hardware.CreateView();
        Task refresh = view.RefreshAsync();
        Assert.False(refresh.IsCompleted);
        bool inputProcessed = false;
        await Dispatcher.CurrentDispatcher.InvokeAsync(() => inputProcessed = true, DispatcherPriority.Input);
        await view.RefreshAsync();
        Assert.True(inputProcessed);
        Assert.Equal(1, hardware.BrightnessReads);

        brightness.SetResult(new BrightnessStatus(25, true));
        await refresh;
        Assert.Equal(25, view.BrightnessLevel);
        Assert.Empty(hardware.Writes);
    });

    [Fact]
    public Task LateRefresh_DoesNotOverwriteTheUsersBrightness() => DispatcherTest.RunAsync(async () =>
    {
        var brightness = new TaskCompletionSource<BrightnessStatus>();
        var hardware = new Hardware { ReadBrightness = () => brightness.Task };
        using QuickSettingsViewModel view = hardware.CreateView();
        Task refresh = view.RefreshAsync();
        view.BrightnessLevel = 77;
        await view.BrightnessWriteTask;
        brightness.SetResult(new BrightnessStatus(10, true));
        await refresh;

        Assert.Equal(77, view.BrightnessLevel);
        Assert.Equal(77, Assert.Single(hardware.Writes));
    });

    [Fact]
    public Task SliderBurst_AndImmediateClose_WritesOnlyTheFinalLevel() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware();
        using QuickSettingsViewModel view = hardware.CreateView();
        view.BrightnessLevel = 10;
        view.BrightnessLevel = 20;
        view.BrightnessLevel = 30;
        view.Dispose();
        await view.BrightnessWriteTask;

        Assert.Equal(30, Assert.Single(hardware.Writes));
    });

    [Fact]
    public Task SlowWrite_CoalescesFurtherSliderChanges() => DispatcherTest.RunAsync(async () =>
    {
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var hardware = new Hardware
        {
            WriteBrightness = level =>
            {
                if (level == 10)
                {
                    entered.SetResult();
                    return release.Task;
                }

                return Task.CompletedTask;
            },
        };
        using QuickSettingsViewModel view = hardware.CreateView();
        view.BrightnessLevel = 10;
        await entered.Task;
        view.BrightnessLevel = 20;
        view.BrightnessLevel = 30;
        release.SetResult();
        await view.BrightnessWriteTask;

        Assert.Equal([10, 30], hardware.Writes);
    });

    [Fact]
    public Task DisposedPanel_IgnoresOutstandingRefreshResults() => DispatcherTest.RunAsync(async () =>
    {
        var brightness = new TaskCompletionSource<BrightnessStatus>();
        var hardware = new Hardware { ReadBrightness = () => brightness.Task };
        using QuickSettingsViewModel view = hardware.CreateView();
        Task refresh = view.RefreshAsync();
        view.Dispose();
        brightness.SetResult(new BrightnessStatus(66, true));
        await refresh;

        Assert.Equal(0, view.BrightnessLevel);
    });

    [Fact]
    public Task LateRadioPoll_DoesNotUndoACompletedToggle() => DispatcherTest.RunAsync(async () =>
    {
        var wifi = new TaskCompletionSource<RadioStatus>();
        var hardware = new Hardware
        {
            ReadRadio = kind => kind == RadioDeviceKind.WiFi
                ? wifi.Task : Task.FromResult(RadioStatus.Unavailable(kind)),
        };
        using QuickSettingsViewModel view = hardware.CreateView();
        Task refresh = view.RefreshAsync();
        await view.Tiles[0].ToggleCommand.ExecuteAsync(null);
        wifi.SetResult(new RadioStatus(RadioDeviceKind.WiFi, false, true));
        await refresh;

        Assert.True(view.Tiles[0].IsOn);
    });

    [Fact]
    public Task FailedRefresh_CanBeRetried() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware { ReadBrightness = () => Task.FromException<BrightnessStatus>(new IOException()) };
        using QuickSettingsViewModel view = hardware.CreateView();
        await view.RefreshAsync();
        Assert.True(view.IsVolumeAvailable);
        Assert.Equal(50, view.VolumeLevel);
        hardware.ReadBrightness = () => Task.FromResult(new BrightnessStatus(55, true));
        await view.RefreshAsync();

        Assert.Equal(55, view.BrightnessLevel);
    });

    [Fact]
    public Task ReopeningThePanel_DoesNotLetAnOldSliderRequestOverwriteNewInput() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware();
        using QuickSettingsViewModel oldPanel = hardware.CreateView();
        oldPanel.BrightnessLevel = 10;
        oldPanel.Dispose();
        using QuickSettingsViewModel newPanel = hardware.CreateView();
        newPanel.BrightnessLevel = 90;
        await Task.WhenAll(oldPanel.BrightnessWriteTask, newPanel.BrightnessWriteTask);

        Assert.Equal(90, Assert.Single(hardware.Writes));
    });

    [Fact]
    public Task PollStartedDuringAPendingBrightnessWrite_DoesNotRestoreAnOldLevel() => DispatcherTest.RunAsync(async () =>
    {
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hardware = new Hardware
        {
            ReadBrightness = () => Task.FromResult(new BrightnessStatus(10, true)),
            ReadNetwork = () =>
            {
                entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException();
                }
                return NetworkStatus.Disconnected;
            },
        };
        using QuickSettingsViewModel view = hardware.CreateView();
        view.BrightnessLevel = 77;
        Task refresh = view.RefreshAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            await view.BrightnessWriteTask;
        }
        finally
        {
            release.Set();
        }
        await refresh;
        Assert.Equal(77, view.BrightnessLevel);
        Assert.Equal(77, Assert.Single(hardware.Writes));
    });

    [Fact]
    public Task SlowBrightness_DoesNotDelayVolumeOrItsNextRefresh() => DispatcherTest.RunAsync(async () =>
    {
        var brightness = new TaskCompletionSource<BrightnessStatus>();
        var volumeReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hardware = new Hardware { ReadBrightness = () => brightness.Task };
        using QuickSettingsViewModel view = hardware.CreateView();
        view.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(view.VolumeLevel))
            {
                volumeReady.TrySetResult();
            }
        };
        Task refresh = view.RefreshAsync();
        try
        {
            await volumeReady.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(view.IsVolumeAvailable);
            Assert.False(refresh.IsCompleted);
            hardware.ReadVolume = () => Task.FromResult(new VolumeStatus(75, false, true));
            await view.RefreshAsync();
            Assert.Equal(75, view.VolumeLevel);
            Assert.Equal(1, hardware.BrightnessReads);
        }
        finally
        {
            brightness.TrySetResult(new BrightnessStatus(25, true));
            await refresh;
        }
    });

    [Fact]
    public Task VolumeBurstAndMute_ImmediateClosePreservesTheFinalCombinedState() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware();
        using QuickSettingsViewModel view = hardware.CreateView();
        view.VolumeLevel = 10;
        view.VolumeLevel = 20;
        view.VolumeLevel = 30;
        view.IsMuted = true;
        view.Dispose();
        await view.VolumeWriteTask;
        Assert.Equal((30, true), Assert.Single(hardware.VolumeWrites));
    });

    [Fact]
    public Task RaisingVolume_UnmutesInTheSameWrite() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware();
        using QuickSettingsViewModel view = hardware.CreateView();
        view.IsMuted = true;
        view.VolumeLevel = 25;
        await view.VolumeWriteTask;
        Assert.False(view.IsMuted);
        Assert.Equal((25, false), Assert.Single(hardware.VolumeWrites));
    });

    [Fact]
    public Task SlowVolumeWrite_LeavesInputResponsiveAndKeepsNewPanelRequestsInOrder() => DispatcherTest.RunAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int worker = 0;
        int ui = Environment.CurrentManagedThreadId;
        var hardware = new Hardware
        {
            WriteVolume = (level, _) =>
            {
                worker = Environment.CurrentManagedThreadId;
                if (level == 10)
                {
                    entered.TrySetResult();
                    return release.Task;
                }
                return Task.CompletedTask;
            },
        };
        using QuickSettingsViewModel oldPanel = hardware.CreateView();
        oldPanel.VolumeLevel = 10;
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using QuickSettingsViewModel newPanel = hardware.CreateView();
        try
        {
            oldPanel.VolumeLevel = 20;
            oldPanel.VolumeLevel = 30;
            oldPanel.Dispose();
            newPanel.VolumeLevel = 90;
            newPanel.IsMuted = true;
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            Assert.NotEqual(ui, worker);
            Assert.False(newPanel.VolumeWriteTask.IsCompleted);
        }
        finally
        {
            release.TrySetResult();
        }
        await Task.WhenAll(oldPanel.VolumeWriteTask, newPanel.VolumeWriteTask);
        Assert.Equal([(10, false), (90, true)], hardware.VolumeWrites);
    });

    [Fact]
    public Task LateVolumePoll_DoesNotUndoTheUsersLevelOrMute() => DispatcherTest.RunAsync(async () =>
    {
        var volume = new TaskCompletionSource<VolumeStatus>();
        var hardware = new Hardware { ReadVolume = () => volume.Task };
        using QuickSettingsViewModel view = hardware.CreateView();
        Task refresh = view.RefreshAsync();
        view.VolumeLevel = 77;
        await view.VolumeWriteTask;
        volume.SetResult(new VolumeStatus(10, true, true));
        await refresh;
        Assert.Equal(77, view.VolumeLevel);
        Assert.False(view.IsMuted);
        Assert.Equal((77, false), Assert.Single(hardware.VolumeWrites));
    });

    [Fact]
    public Task ClosingPanel_StatusRefreshWaitsForTheLastVolumeWrite() => DispatcherTest.RunAsync(async () =>
    {
        var hardware = new Hardware();
        using QuickSettingsViewModel view = hardware.CreateView();
        using var status = new QuickStatusViewModel(hardware.Volume, hardware, hardware);
        view.VolumeLevel = 77;
        view.Dispose();
        await status.RefreshAsync();
        Assert.Contains("77", status.Tooltip);
        Assert.Equal((77, false), Assert.Single(hardware.VolumeWrites));
    });

    [Fact]
    public Task FailedVolumeWrite_DoesNotDropNewerInputOrPreventLaterReads() => DispatcherTest.RunAsync(async () =>
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hardware = new Hardware
        {
            WriteVolume = async (level, _) =>
            {
                if (level == 10)
                {
                    entered.TrySetResult();
                    await release.Task;
                    throw new IOException();
                }
            },
        };
        Task first = hardware.Volume.SetStateAsync(10);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Task last = hardware.Volume.SetStateAsync(80, true);
        release.SetResult();
        await Assert.ThrowsAsync<IOException>(() => first);
        await Assert.ThrowsAsync<IOException>(() => last);
        VolumeStatus current = await hardware.Volume.GetStatusAsync();
        Assert.Equal(80, current.Level);
        Assert.True(current.IsMuted);
        Assert.Equal([(10, null), (80, true)], hardware.VolumeWrites);
    });

    [Fact]
    public Task VolumeReadAlreadyInFlight_RetriesIfAnotherPanelWritesBeforeItReturns() => DispatcherTest.RunAsync(async () =>
    {
        var stale = new TaskCompletionSource<VolumeStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        var hardware = new Hardware
        {
            ReadVolume = () => ++reads == 1 ? stale.Task : Task.FromResult(new VolumeStatus(77, false, true)),
        };
        Task<VolumeStatus> read = hardware.Volume.GetStatusAsync();
        Task write = hardware.Volume.SetStateAsync(77, false);
        stale.SetResult(new VolumeStatus(10, true, true));
        await write;
        VolumeStatus current = await read;
        Assert.Equal(77, current.Level);
        Assert.False(current.IsMuted);
        Assert.Equal(2, reads);
    });

    private sealed class Hardware : IRadioController, IBrightnessController, IVolumeController,
        IBatteryProvider, INetworkProvider, ISettingsPageLauncher
    {
        public Func<Task<BrightnessStatus>> ReadBrightness = () => Task.FromResult(new BrightnessStatus(50, true));
        public Func<int, Task> WriteBrightness = _ => Task.CompletedTask;
        public Func<RadioDeviceKind, Task<RadioStatus>> ReadRadio = kind => Task.FromResult(new RadioStatus(kind, false, true));
        public Func<NetworkStatus> ReadNetwork = () => NetworkStatus.Disconnected;
        public Func<Task<VolumeStatus>>? ReadVolume;
        public Func<int?, bool?, Task> WriteVolume = (_, _) => Task.CompletedTask;
        public readonly List<int> Writes = [];
        public readonly List<(int? Level, bool? IsMuted)> VolumeWrites = [];
        public int BrightnessReads;
        private BufferedBrightnessController? _buffer;
        private BufferedVolumeController? _volumeBuffer;
        private VolumeStatus _volumeStatus = new(50, false, true);

        public IVolumeController Volume => _volumeBuffer ??= new BufferedVolumeController(this);

        public QuickSettingsViewModel CreateView() => new(this, _buffer ??= new BufferedBrightnessController(this), Volume, this, this, this);
        public Task<RadioStatus> GetStatusAsync(RadioDeviceKind kind) => ReadRadio(kind);
        public Task<RadioStatus> SetStateAsync(RadioDeviceKind kind, bool isOn) => Task.FromResult(new RadioStatus(kind, isOn, true));
        public Task<BrightnessStatus> GetStatusAsync()
        {
            BrightnessReads++;
            return ReadBrightness();
        }

        public Task SetLevelAsync(int level)
        {
            Writes.Add(level);
            return WriteBrightness(level);
        }

        Task<VolumeStatus> IVolumeController.GetStatusAsync() => ReadVolume?.Invoke() ?? Task.FromResult(_volumeStatus);
        BatteryStatus IBatteryProvider.GetStatus() => BatteryStatus.None;
        NetworkStatus INetworkProvider.GetStatus() => ReadNetwork();
        public async Task SetStateAsync(int? level = null, bool? isMuted = null)
        {
            VolumeWrites.Add((level, isMuted));
            await WriteVolume(level, isMuted);
            _volumeStatus = new VolumeStatus(level ?? _volumeStatus.Level, isMuted ?? _volumeStatus.IsMuted, true);
        }
        public void Open(string page) { }
    }
}
