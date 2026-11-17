using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Native;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class FlyoutPresentationTests
{
    public enum FlyoutKind { Calendar, Tray, QuickSettings }

    [Theory]
    [InlineData(FlyoutKind.Calendar)]
    [InlineData(FlyoutKind.Tray)]
    [InlineData(FlyoutKind.QuickSettings)]
    public Task NativeComposition_KeepsTheFlyoutCloakedUntilItsFirstFrameIsReady(FlyoutKind kind) => DispatcherTest.RunAsync(async () =>
    {
        var presentation = new Presentation { Native = new DwmWindowBackdrop() };
        Window window = CreateWindow(kind, presentation);
        try
        {
            window.Show();
            await presentation.Revealed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(0, presentation.CloakQueryResult);
            Assert.NotEqual(0, presentation.CloakDuringFlush & 1);
            Assert.Equal(0, presentation.CloakAfterReveal & 1);
            AssertContentReady(window, kind);
        }
        finally
        {
            window.Close();
        }
    });

    [Theory]
    [InlineData(FlyoutKind.Calendar)]
    [InlineData(FlyoutKind.Tray)]
    [InlineData(FlyoutKind.QuickSettings)]
    public Task FirstFrame_StaysCloakedUntilTheControlsAndCompositionAreReady(FlyoutKind kind) => DispatcherTest.RunAsync(async () =>
    {
        var presentation = new Presentation();
        Window window = CreateWindow(kind, presentation);
        try
        {
            window.Show();
            await presentation.Flushing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(presentation.Cloaked);
            Assert.Equal(["cloak", "appearance", "position", "flush"], presentation.Events);
            AssertContentReady(window, kind);

            presentation.Composed.SetResult();
            await presentation.Revealed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(presentation.Cloaked);
            Assert.Equal("reveal", presentation.Events[^1]);
        }
        finally
        {
            window.Close();
            presentation.Composed.TrySetResult();
        }
    });

    [Theory]
    [InlineData(FlyoutKind.Calendar)]
    [InlineData(FlyoutKind.Tray)]
    [InlineData(FlyoutKind.QuickSettings)]
    public Task ClosedDuringComposition_DoesNotRevealTheDismissedWindow(FlyoutKind kind) => DispatcherTest.RunAsync(async () =>
    {
        var presentation = new Presentation();
        Window window = CreateWindow(kind, presentation);
        try
        {
            window.Show();
            await presentation.Flushing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            window.Close();
            presentation.Composed.SetResult();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            Assert.DoesNotContain("reveal", presentation.Events);
            Assert.False(window.IsVisible);
        }
        finally
        {
            window.Close();
            presentation.Composed.TrySetResult();
        }
    });

    [Fact]
    public Task QuickSettings_SlowHardwareDoesNotDelayCompositionOrReviveAClosedPanel() => DispatcherTest.RunAsync(async () =>
    {
        var brightness = new TaskCompletionSource<BrightnessStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hardware = new Hardware { Brightness = brightness.Task };
        var presentation = new Presentation();
        Window window = CreateWindow(FlyoutKind.QuickSettings, presentation, hardware);
        var model = (QuickSettingsViewModel)window.DataContext;
        try
        {
            window.Show();
            await presentation.Flushing.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, hardware.BrightnessReads);
            Assert.False(brightness.Task.IsCompleted);
            AssertContentReady(window, FlyoutKind.QuickSettings);

            presentation.Composed.SetResult();
            await presentation.Revealed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(brightness.Task.IsCompleted);

            window.Close();
            brightness.SetResult(new BrightnessStatus(80, true));
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.False(window.IsVisible);
            Assert.False(model.IsBrightnessAvailable);
        }
        finally
        {
            window.Close();
            presentation.Composed.TrySetResult();
            brightness.TrySetResult(BrightnessStatus.Unavailable);
        }
    });

    private static Window CreateWindow(FlyoutKind kind, Presentation presentation, Hardware? hardware = null)
    {
        var anchor = new PixelRect(100, 900, 200, 950);
        hardware ??= new Hardware();
        Window window = kind switch
        {
            FlyoutKind.Calendar => new CalendarFlyoutWindow(
                new CalendarFlyoutViewModel(DateTimeOffset.Now, new CalendarMonthBuilder()), anchor, DockEdge.Bottom,
                presentation, presentation, presentation, presentation),
            FlyoutKind.Tray => new TrayFlyoutWindow(CreateTrayModel(), anchor, DockEdge.Bottom,
                presentation, presentation, presentation, presentation),
            FlyoutKind.QuickSettings => new QuickSettingsWindow(
                new QuickSettingsViewModel(hardware, hardware, hardware, hardware, hardware, hardware), anchor, DockEdge.Bottom,
                presentation, presentation, presentation, presentation),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        window.ShowActivated = false;
        return window;
    }

    private static TrayFlyoutViewModel CreateTrayModel()
    {
        var image = new DrawingImage(new GeometryDrawing(Brushes.CornflowerBlue, null,
            new RectangleGeometry(new Rect(0, 0, 16, 16))));
        image.Freeze();
        var host = new TrayHost();
        return new TrayFlyoutViewModel(Enumerable.Range(1, 2).Select(id => new TrayIconViewModel(
            new TrayIcon(new TrayIconKey(1, (uint)id, Guid.Empty), "Test icon", 0, 0, 4, false),
            image, host, () => default)).ToArray());
    }

    private static void AssertContentReady(Window window, FlyoutKind kind)
    {
        switch (kind)
        {
            case FlyoutKind.Calendar:
                Assert.Equal(42, Descendants(window).OfType<TextBlock>().Count(text => text.DataContext is CalendarDay));
                break;
            case FlyoutKind.Tray:
                Image[] icons = Descendants(window).OfType<Image>().Where(image => image.DataContext is TrayIconViewModel).ToArray();
                Assert.Equal(2, icons.Length);
                Assert.All(icons, image =>
                {
                    Assert.NotNull(image.Source);
                    Assert.True(image.ActualWidth > 0);
                });
                break;
            case FlyoutKind.QuickSettings:
                Button[] tiles = Descendants(window).OfType<Button>().Where(button => button.Name == "ToggleHalf").ToArray();
                Assert.Equal(6, tiles.Length);
                Assert.All(tiles, tile => Assert.True(tile.ActualWidth > 0));
                break;
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class Hardware : IRadioController, IBrightnessController, IVolumeController,
        IBatteryProvider, INetworkProvider, ISettingsPageLauncher
    {
        public Task<BrightnessStatus> Brightness = Task.FromResult(BrightnessStatus.Unavailable);
        public int BrightnessReads;
        public Task<RadioStatus> GetStatusAsync(RadioDeviceKind kind) => Task.FromResult(RadioStatus.Unavailable(kind));
        public Task<RadioStatus> SetStateAsync(RadioDeviceKind kind, bool isOn) => throw new InvalidOperationException("Unexpected hardware write");
        public Task<BrightnessStatus> GetStatusAsync()
        {
            BrightnessReads++;
            return Brightness;
        }

        public Task SetLevelAsync(int level) => throw new InvalidOperationException("Unexpected hardware write");
        VolumeStatus IVolumeController.GetStatus() => VolumeStatus.Unavailable;
        BatteryStatus IBatteryProvider.GetStatus() => BatteryStatus.None;
        NetworkStatus INetworkProvider.GetStatus() => NetworkStatus.Disconnected;
        public void SetLevel(int level) => throw new InvalidOperationException("Unexpected hardware write");
        public void SetMuted(bool isMuted) => throw new InvalidOperationException("Unexpected hardware write");
        public void Open(string page) => throw new InvalidOperationException("Unexpected settings launch");
    }

    private sealed class TrayHost : ITrayHost
    {
        public event EventHandler? IconsChanged { add { } remove { } }
        public IReadOnlyList<TrayIcon> Icons => [];
        public bool IsHosting => false;
        public void Start() { }
        public void Stop() { }
        public void Maintain() { }
        public void ForwardMouseEvent(TrayIcon icon, TrayMouseEvent mouseEvent, PixelPoint screenPoint) =>
            throw new InvalidOperationException("Unexpected tray interaction");
        public bool IsAppInForeground(TrayIcon icon) => false;
    }

    private sealed class Presentation : IWindowBackdrop, IWindowPositioner, IWindowSwitcherExclusion, IScreenProvider
    {
        public readonly List<string> Events = [];
        public readonly TaskCompletionSource Flushing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Composed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Revealed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Cloaked;
        public IWindowBackdrop? Native;
        public int CloakQueryResult;
        public int CloakDuringFlush;
        public int CloakAfterReveal;
        private nint _handle;

        public void Apply(nint windowHandle, WindowBackdropMaterial material, bool darkMode)
        {
            Events.Add("appearance");
            Native?.Apply(windowHandle, material, darkMode);
        }

        public void SetCloaked(nint windowHandle, bool cloaked)
        {
            _handle = windowHandle;
            Native?.SetCloaked(windowHandle, cloaked);
            Cloaked = cloaked;
            Events.Add(cloaked ? "cloak" : "reveal");
            if (!cloaked)
            {
                if (Native is not null)
                {
                    NativeMethods.DwmGetWindowAttribute(_handle, NativeConstants.DWMWA_CLOAKED, out CloakAfterReveal, sizeof(int));
                }

                Revealed.TrySetResult();
            }
        }

        public Task FlushAsync()
        {
            Events.Add("flush");
            Flushing.TrySetResult();
            if (Native is not null)
            {
                CloakQueryResult = NativeMethods.DwmGetWindowAttribute(_handle, NativeConstants.DWMWA_CLOAKED,
                    out CloakDuringFlush, sizeof(int));
                return Native.FlushAsync();
            }

            return Composed.Task;
        }

        public void SetBounds(nint windowHandle, PixelRect bounds, bool topMost) => Events.Add("position");
        public void BringToTop(nint windowHandle) { }
        public void Exclude(nint windowHandle) { }
        public MonitorInfo GetPrimaryMonitor() => new(new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), true, 1);
        public IReadOnlyList<MonitorInfo> GetMonitors() => [GetPrimaryMonitor()];
    }
}
