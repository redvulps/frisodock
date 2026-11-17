using System.Windows.Media;
using System.Windows.Threading;
using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class PackagedAppIconTests
{
    private const string AppId = "Test.Calculator_publisher!App";
    private const string Executable = @"C:\Program Files\WindowsApps\Calculator\CalculatorApp.exe";
    private static readonly string ShellPath = ApplicationShellPath.FromApplicationId(AppId)!;

    [Fact]
    public void DockAndSwitcher_UseTheRegisteredAppIcon_AndKeepExecutableMatching()
    {
        WindowInfo window = CreateWindow();
        DockItem dock = Assert.Single(new DockItemAggregator().Build([], [window]));
        DockItem switcher = Assert.Single(new WindowSwitchList().Build([window]));

        Assert.Equal(ShellPath, dock.IconSource);
        Assert.Equal(ShellPath, switcher.IconSource);
        Assert.Equal(AppKey.FromExecutable(Executable), dock.Key);
        Assert.Equal(Executable, dock.JumpListExecutable);
    }

    [Fact]
    public void ExistingExecutablePin_UsesTheRunningAppsShellIcon()
    {
        var pin = new PinnedApp("Calculadora", Executable);
        DockItem item = Assert.Single(new DockItemAggregator().Build([pin], [CreateWindow()]));

        Assert.True(item.IsPinned);
        Assert.True(item.IsRunning);
        Assert.Equal(ShellPath, item.IconSource);
    }

    [Fact]
    public void ExplicitPinnedIcon_IsPreserved()
    {
        var pin = new PinnedApp("Calculadora", Executable, IconPath: @"C:\icons\custom.ico");
        DockItem item = Assert.Single(new DockItemAggregator().Build([pin], [CreateWindow()]));

        Assert.Equal(pin.IconPath, item.IconSource);
    }

    [Fact]
    public void PinningPackagedApp_PersistsItsShellIconAndLaunchTarget()
    {
        var store = new Store();
        var editor = new PinnedAppsService(store, new PinnedAppsReorder());
        DockItem running = Assert.Single(new DockItemAggregator().Build([], [CreateWindow()]));
        editor.TogglePin(running);
        PinnedApp pin = Assert.Single(store.Pins);

        Assert.Equal(ShellPath, pin.LaunchTarget);
        Assert.Equal(ShellPath, pin.IconSource);
        Assert.Equal(Executable, pin.MatchExecutablePath);
        Assert.True(Assert.Single(new DockItemAggregator().Build(store.Pins, [CreateWindow()])).IsRunning);
    }

    [Fact]
    public void DesktopApp_UsesItsExecutableWhenItHasNoRegisteredIdentity()
    {
        WindowInfo window = CreateWindow() with { ApplicationUserModelId = null };
        Assert.Equal(Executable, window.IconSource);
        Assert.Null(window.ShellLaunchTarget);
    }

    [Fact]
    public void MissingRegisteredApp_DoesNotReturnAGenericExecutableIcon()
    {
        string path = ApplicationShellPath.FromApplicationId("FrisoDock.NonexistentPackage_0000000000000!Missing")!;
        Assert.Null(ShellAppResources.GetDisplayName(path));
        using IconHandle? icon = new IconExtractor().FromFile(path, 64);
        Assert.Null(icon);
    }

    [Fact]
    public Task SlowPackageIcon_LoadsOnceOffTheDispatcher_AndPublishesAFrozenImage() => DispatcherTest.RunAsync(async () =>
    {
        var extractor = new SlowExtractor();
        using var release = extractor.Release;
        var store = new Store();
        var icons = new IconImageProvider(extractor, new DockSettingsService(store, new DockSettings()));
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        icons.IconReady += (_, _) => ready.TrySetResult();
        DockItem item = Assert.Single(new DockItemAggregator().Build([], [CreateWindow()]));
        int uiThread = Environment.CurrentManagedThreadId;
        try
        {
            Assert.Null(icons.GetIcon(item));
            await extractor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Null(icons.GetIcon(item));
            await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            Assert.NotEqual(uiThread, extractor.WorkerThread);
            Assert.Equal(1, extractor.Calls);
        }
        finally
        {
            release.Set();
        }

        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        ImageSource image = Assert.IsAssignableFrom<ImageSource>(icons.GetIcon(item));
        Assert.True(image.IsFrozen);
        Assert.Same(image, icons.GetIcon(item));
        Assert.Equal(1, extractor.Calls);
    });

    private static WindowInfo CreateWindow() => new(1, "Calculadora", 20, Executable,
        false, true, FriendlyName: "Calculadora", ApplicationUserModelId: AppId);

    private sealed class Store : IDockSettingsStore, IPinnedAppStore
    {
        public IReadOnlyList<PinnedApp> Pins = [];
        DockSettings IDockSettingsStore.Load() => new();
        IReadOnlyList<PinnedApp> IPinnedAppStore.Load() => Pins;
        public void Save(DockSettings settings) { }
        public void Save(IReadOnlyList<PinnedApp> apps) => Pins = apps;
    }

    private sealed class SlowExtractor : IIconExtractor
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ManualResetEventSlim Release = new();
        public int WorkerThread;
        public int Calls;
        public IconHandle? FromFile(string path, int preferredSize, int iconIndex = 0)
        {
            Interlocked.Increment(ref Calls);
            WorkerThread = Environment.CurrentManagedThreadId;
            Entered.TrySetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Test did not release icon extraction");
            }

            return new IconExtractor().FromFile(Environment.ProcessPath!, preferredSize);
        }

        public IconHandle? FromWindow(nint windowHandle) => null;
    }
}
