using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.Tests;

public sealed class DockRefreshTests
{
    [Fact]
    public Task ContinuousWindowEvents_DoNotPostponeTheFirstRefresh() => DispatcherTest.RunAsync(async () =>
    {
        var services = new Services();
        var settings = new DockSettingsService(services, new DockSettings());
        using var clock = new ClockViewModel(new SystemClock(), new ClockFormatter(), settings);
        using var status = new QuickStatusViewModel(services, services, services);
        var monitor = new MonitorInfo(new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true, 1);
        using var dock = new DockViewModel(
            services, new PinnedAppsService(services, new PinnedAppsReorder()), services, services, services,
            new DockItemAggregator(), new WindowMonitorMatcher(), new IconImageProvider(services, settings),
            services, clock, status, settings, new DockMonitorHolder { Monitor = DockMonitor.Single(monitor) });
        dock.Start();
        int events = 0;
        var producer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        producer.Tick += (_, _) =>
        {
            events++;
            services.Change();
        };
        producer.Start();
        try
        {
            await services.Refreshed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(events > 1);
            Assert.True(producer.IsEnabled);
        }
        finally
        {
            producer.Stop();
        }
    });

    [Fact]
    public void IconCache_EvictsOldDocuments_AndKeepsRecentOnes()
    {
        var services = new Services();
        var icons = new IconImageProvider(services, new DockSettingsService(services, new DockSettings()));
        for (int i = 0; i <= 512; i++)
        {
            icons.GetIcon($"document-{i}.pdf", 0, 32);
        }

        Assert.Equal(513, services.IconReads);
        icons.GetIcon("document-512.pdf", 0, 32);
        Assert.Equal(513, services.IconReads);
        icons.GetIcon("document-0.pdf", 0, 32);
        Assert.Equal(514, services.IconReads);
    }

    private sealed class Services : IWindowEnumerator, IPinnedAppStore, IDockSettingsStore,
        IWindowActivator, IAppLauncher, IStartMenuInvoker, IIconExtractor, IApplicationLifetime,
        IVolumeController, IBatteryProvider, INetworkProvider
    {
        public int IconReads;
        private int _windowReads;
        public readonly TaskCompletionSource Refreshed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event EventHandler? WindowsChanged;
        public void Change() => WindowsChanged?.Invoke(this, EventArgs.Empty);
        public IReadOnlyList<WindowInfo> GetWindows()
        {
            if (++_windowReads >= 2)
            {
                Refreshed.TrySetResult();
            }

            return [];
        }

        public void Start() { }
        public void Stop() { }
        IReadOnlyList<PinnedApp> IPinnedAppStore.Load() => [];
        DockSettings IDockSettingsStore.Load() => new();
        public void Save(IReadOnlyList<PinnedApp> apps) { }
        public void Save(DockSettings settings) { }
        public void Activate(WindowInfo window) { }
        public void Activate(nint windowHandle) { }
        public void Minimize(WindowInfo window) { }
        public void ToggleActivation(WindowInfo window) { }
        public void Close(WindowInfo window) { }
        public bool Launch(PinnedApp app) => false;
        public bool Launch(JumpListEntry entry) => false;
        public void Toggle() { }
        public IconHandle? FromFile(string path, int preferredSize, int iconIndex = 0)
        {
            IconReads++;
            return null;
        }

        public IconHandle? FromWindow(nint windowHandle) => null;
        public void Shutdown() { }
        Task<VolumeStatus> IVolumeController.GetStatusAsync() => Task.FromResult(VolumeStatus.Unavailable);
        BatteryStatus IBatteryProvider.GetStatus() => BatteryStatus.None;
        NetworkStatus INetworkProvider.GetStatus() => NetworkStatus.Disconnected;
        public Task SetStateAsync(int? level = null, bool? isMuted = null) => Task.CompletedTask;
    }
}
