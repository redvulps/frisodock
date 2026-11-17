using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;
using FrisoDock.Core.Services;

namespace FrisoDock.Tests;

public sealed class PinnedAppsInteractionTests
{
    private static readonly PinnedApp Alpha = new("Alpha", @"C:\Apps\Alpha.exe");
    private static readonly PinnedApp Beta = new("Beta", @"C:\Apps\Beta.exe");
    private static readonly PinnedApp Gamma = new("Gamma", @"C:\Apps\Gamma.exe");

    [Fact]
    public void AppMenu_PinsAndUnpinsARunningAppUsingCurrentState()
    {
        var services = new Services();
        var pins = new PinnedAppsService(services, new PinnedAppsReorder());
        var settings = new DockSettingsService(services, new DockSettings());
        var menu = new JumpListFlyoutFactory(services, services, services, pins, new IconImageProvider(services, settings));
        var window = new WindowInfo(1, "Alpha", 10, Alpha.LaunchTarget, false, true, FriendlyName: "Alpha");
        DockItem running = Assert.Single(new DockItemAggregator().Build([], [window]));

        var pinAction = Assert.Single(menu.Create(running).Sections.SelectMany(section => section.Items),
            item => item.Title == Strings.JumpListPin);
        pinAction.InvokeCommand.Execute(null);
        Assert.True(pins.IsPinned(running.Key));
        Assert.Equal(Alpha.LaunchTarget, Assert.Single(pins.Current).LaunchTarget);

        // The open window model can still be unpinned; the menu asks the shared editor for the current state.
        var unpinAction = Assert.Single(menu.Create(running).Sections.SelectMany(section => section.Items),
            item => item.Title == Strings.JumpListUnpin);
        unpinAction.InvokeCommand.Execute(null);
        Assert.Empty(pins.Current);
        DockItem remaining = Assert.Single(new DockItemAggregator().Build(pins.Current, [window]));
        Assert.False(remaining.IsPinned);
        Assert.True(remaining.IsRunning);
        Assert.Equal(2, services.Saves);
    }

    [Fact]
    public void UnpinningAClosedApp_RemovesItsDockItem()
    {
        var services = new Services { Pins = [Alpha] };
        var pins = new PinnedAppsService(services, new PinnedAppsReorder());
        DockItem item = Assert.Single(new DockItemAggregator().Build(pins.Current, []));
        pins.TogglePin(item);
        Assert.Empty(new DockItemAggregator().Build(pins.Current, []));
        Assert.Empty(new PinnedAppsService(services, new PinnedAppsReorder()).Current);
    }

    [Fact]
    public void Drag_ReordersLiveForAllObservers_AndSavesOnceOnRelease()
    {
        var services = new Services { Pins = [Alpha, Beta, Gamma] };
        var pins = new PinnedAppsService(services, new PinnedAppsReorder());
        AppKey[]? firstDock = null;
        AppKey[]? secondDock = null;
        pins.Changed += (_, _) => firstDock = pins.Current.Select(app => app.Key).ToArray();
        pins.Changed += (_, _) => secondDock = pins.Current.Select(app => app.Key).ToArray();

        pins.Reorder(Alpha.Key, 1, persist: false);
        pins.Reorder(Alpha.Key, 2, persist: false);
        AppKey[] expected = [Beta.Key, Gamma.Key, Alpha.Key];
        Assert.Equal(expected, firstDock);
        Assert.Equal(expected, secondDock);
        Assert.Equal(0, services.Saves);
        pins.CommitReorder();
        pins.CommitReorder();
        Assert.Equal(1, services.Saves);
        Assert.Equal(expected, new PinnedAppsService(services, new PinnedAppsReorder()).Current.Select(app => app.Key));
    }

    [Fact]
    public void NoOpDrag_DoesNotWriteOrNotify()
    {
        var services = new Services { Pins = [Alpha, Beta] };
        var pins = new PinnedAppsService(services, new PinnedAppsReorder());
        int changes = 0;
        pins.Changed += (_, _) => changes++;
        pins.Reorder(Alpha.Key, 0, persist: false);
        pins.Reorder(Gamma.Key, 1, persist: false);
        pins.CommitReorder();
        Assert.Equal(0, services.Saves);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void PinChange_AlsoPersistsAnyPendingOrderWithoutADuplicateSave()
    {
        var services = new Services { Pins = [Alpha, Beta] };
        var pins = new PinnedAppsService(services, new PinnedAppsReorder());
        pins.Reorder(Beta.Key, 0, persist: false);
        pins.TogglePin(new DockItem(Alpha.Key, Alpha.DisplayName, Alpha.IconSource, Alpha, []));
        pins.CommitReorder();
        Assert.Equal(Beta, Assert.Single(services.Pins));
        Assert.Equal(1, services.Saves);
    }

    private sealed class Services : IPinnedAppStore, IDockSettingsStore, IAppLauncher, IWindowActivator,
        IIconExtractor, IJumpListProvider
    {
        public IReadOnlyList<PinnedApp> Pins = [];
        public int Saves;
        IReadOnlyList<PinnedApp> IPinnedAppStore.Load() => Pins;
        public void Save(IReadOnlyList<PinnedApp> apps) { Pins = apps; Saves++; }
        DockSettings IDockSettingsStore.Load() => new();
        public void Save(DockSettings settings) { }
        public bool Launch(PinnedApp app) => false;
        public bool Launch(JumpListEntry entry) => false;
        public void Activate(WindowInfo window) { }
        public void Activate(nint windowHandle) { }
        public void Minimize(WindowInfo window) { }
        public void ToggleActivation(WindowInfo window) { }
        public void Close(WindowInfo window) { }
        public IconHandle? FromFile(string path, int preferredSize, int iconIndex = 0) => null;
        public IconHandle? FromWindow(nint windowHandle) => null;
        public JumpList GetFor(string executablePath) => JumpList.Empty;
        public void Invalidate() { }
    }
}
