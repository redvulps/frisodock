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

    private sealed class Hardware : IRadioController, IBrightnessController, IVolumeController,
        IBatteryProvider, INetworkProvider, ISettingsPageLauncher
    {
        public Func<Task<BrightnessStatus>> ReadBrightness = () => Task.FromResult(new BrightnessStatus(50, true));
        public Func<int, Task> WriteBrightness = _ => Task.CompletedTask;
        public Func<RadioDeviceKind, Task<RadioStatus>> ReadRadio = kind => Task.FromResult(new RadioStatus(kind, false, true));
        public readonly List<int> Writes = [];
        public int BrightnessReads;
        private BufferedBrightnessController? _buffer;

        public QuickSettingsViewModel CreateView() => new(this, _buffer ??= new BufferedBrightnessController(this), this, this, this, this);
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

        VolumeStatus IVolumeController.GetStatus() => new(50, false, true);
        BatteryStatus IBatteryProvider.GetStatus() => BatteryStatus.None;
        NetworkStatus INetworkProvider.GetStatus() => NetworkStatus.Disconnected;
        public void SetLevel(int level) { }
        public void SetMuted(bool isMuted) { }
        public void Open(string page) { }
    }
}
