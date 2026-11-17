using System.Runtime.InteropServices;
using System.Windows.Interop;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Native;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class HostedWindowIdentityResolverTests
{
    private const string Host = @"C:\Windows\System32\ApplicationFrameHost.exe";
    private const string Calculator = @"C:\Program Files\WindowsApps\Calculator\CalculatorApp.exe";
    private const string Settings = @"C:\Windows\ImmersiveControlPanel\SystemSettings.exe";
    private const string CalculatorId = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    private const string SettingsId = "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel";

    [Fact]
    public void HostedCalculator_UsesItsContentProcessAndLocalizedWindowName()
    {
        HostedWindowIdentityResolver resolver = Create();
        WindowProcessIdentity identity = resolver.Resolve(1, "Calculadora");

        Assert.Equal(20u, identity.ProcessId);
        Assert.Equal(Calculator, identity.ExecutablePath);
        Assert.Equal("Calculadora", identity.DisplayName);
    }

    [Fact]
    public void InputServiceDescendants_AreNotMistakenForTheHostedApp()
    {
        var resolver = new HostedWindowIdentityResolver(
            handle => handle == 1 ? 10u : handle == 2 ? 99u : 20u,
            process => process == 10 ? Host : process == 99 ? @"C:\Windows\TextInputHost.exe" : Calculator,
            _ => [2, 3],
            handle => handle == 2 ? "InputSite" : "Windows.UI.Core.CoreWindow");

        Assert.Equal(Calculator, resolver.Resolve(1, "Calculadora").ExecutablePath);
    }

    [Fact]
    public void AppsSharingAFrameHost_RemainSeparateInTheSwitcher()
    {
        HostedWindowIdentityResolver resolver = Create();
        WindowProcessIdentity calculator = resolver.Resolve(1, "Calculadora");
        WindowProcessIdentity settings = resolver.Resolve(4, "Configurações");
        var windows = new[]
        {
            new WindowInfo(1, "Calculadora", (int)calculator.ProcessId, calculator.ExecutablePath,
                false, true, FriendlyName: calculator.DisplayName),
            new WindowInfo(4, "Configurações", (int)settings.ProcessId, settings.ExecutablePath,
                false, false, FriendlyName: settings.DisplayName),
        };

        var items = new WindowSwitchList().Build(windows);
        Assert.Equal(2, items.Count);
        Assert.Equal("Calculadora", items[0].DisplayName);
        Assert.Equal(Calculator, items[0].IconSource);
        Assert.Equal("Configurações", items[1].DisplayName);
        Assert.Equal(Settings, items[1].IconSource);
    }

    [Fact]
    public void InaccessibleContent_StillUsesTheVisibleAppName()
    {
        var resolver = new HostedWindowIdentityResolver(_ => 10, _ => Host, _ => [], _ => "");
        WindowProcessIdentity identity = resolver.Resolve(1, "Calculadora");

        Assert.Equal("Calculadora", identity.DisplayName);
        Assert.Equal(Host, identity.ExecutablePath);
    }

    [Fact]
    public void RegularDesktopApps_DoNotScanChildrenOrReplaceTheirProductName()
    {
        var resolver = new HostedWindowIdentityResolver(_ => 20, _ => Calculator,
            _ => throw new Exception("Unexpected child scan"), _ => "",
            _ => throw new Exception("Unexpected Shell query"),
            _ => throw new Exception("Unexpected process query"),
            () => throw new Exception("Unexpected detached window scan"));
        WindowProcessIdentity identity = resolver.Resolve(1, "A document title");

        Assert.Equal(Calculator, identity.ExecutablePath);
        Assert.Null(identity.DisplayName);
    }

    [Fact]
    public void AlreadyMinimizedApps_SharingAHostMatchTheirOwnShellIdentity()
    {
        HostedWindowIdentityResolver resolver = CreateDetached();
        WindowProcessIdentity calculator = resolver.Resolve(1, "Same title");
        WindowProcessIdentity settings = resolver.Resolve(4, "Same title");

        Assert.Equal(Calculator, calculator.ExecutablePath);
        Assert.Equal(20u, calculator.ProcessId);
        Assert.Equal(CalculatorId, calculator.ApplicationUserModelId);
        Assert.Equal(Settings, settings.ExecutablePath);
        Assert.Equal(30u, settings.ProcessId);
        Assert.Equal(SettingsId, settings.ApplicationUserModelId);
    }

    [Fact]
    public void MinimizeRestoreCycle_PreservesIconGroupingAndPinnedWindowMatching()
    {
        bool minimized = false;
        var resolver = CreateDetached(_ => minimized ? [] : [3]);
        var pin = new PinnedApp("Calculadora", Calculator);
        foreach (bool state in new[] { false, true, false, true })
        {
            minimized = state;
            WindowProcessIdentity identity = resolver.Resolve(1, "Calculadora");
            var window = new WindowInfo(1, "Calculadora", (int)identity.ProcessId, identity.ExecutablePath,
                minimized, !minimized, FriendlyName: identity.DisplayName,
                ApplicationUserModelId: identity.ApplicationUserModelId);
            DockItem item = Assert.Single(new DockItemAggregator().Build([pin], [window]));

            Assert.True(item.IsPinned);
            Assert.True(item.IsRunning);
            Assert.Equal(AppKey.FromExecutable(Calculator), item.Key);
            Assert.Equal(ApplicationShellPath.FromApplicationId(CalculatorId), item.IconSource);
            Assert.Equal(item.IconSource, Assert.Single(new WindowSwitchList().Build([window])).IconSource);
            Assert.Equal(minimized, Assert.Single(item.Windows).IsMinimized);
        }
    }

    [Fact]
    public void AttachedContent_DoesNotQueryShellOrScanDetachedWindows()
    {
        var resolver = new HostedWindowIdentityResolver(
            handle => handle == 1 ? 10u : 20u,
            process => process == 10 ? Host : Calculator,
            _ => [3], _ => "Windows.UI.Core.CoreWindow",
            _ => throw new Exception("Unexpected Shell query"), _ => CalculatorId,
            () => throw new Exception("Unexpected detached window scan"));

        Assert.Equal(Calculator, resolver.Resolve(1, "Calculadora").ExecutablePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingWindowIdentity_DoesNotGuessFromUnrelatedDetachedApps(string? applicationId)
    {
        var resolver = new HostedWindowIdentityResolver(_ => 10, _ => Host, _ => [], _ => "",
            _ => applicationId, _ => CalculatorId,
            () => throw new Exception("Unexpected detached window scan"));

        Assert.Equal(Host, resolver.Resolve(1, "Calculadora").ExecutablePath);
    }

    [Fact]
    public void MissingMatchingContent_KeepsShellIconWithoutBorrowingAnotherAppsProcess()
    {
        var resolver = new HostedWindowIdentityResolver(
            handle => handle == 1 ? 10u : 30u,
            process => process == 10 ? Host : Settings,
            _ => [], _ => "Windows.UI.Core.CoreWindow", _ => CalculatorId, _ => SettingsId, () => [5]);
        WindowProcessIdentity identity = resolver.Resolve(1, "Calculadora");
        var window = new WindowInfo(1, "Calculadora", (int)identity.ProcessId, identity.ExecutablePath,
            true, false, ApplicationUserModelId: identity.ApplicationUserModelId);

        Assert.Equal(10u, identity.ProcessId);
        Assert.Equal(ApplicationShellPath.FromApplicationId(CalculatorId), window.IconSource);
    }

    [Fact]
    public Task WindowShellIdentity_CanBeReadFromStaAndWorkerThreads() => DispatcherTest.RunAsync(async () =>
    {
        using var source = new HwndSource(new HwndSourceParameters("FrisoDock Shell identity test")
        {
            Width = 1,
            Height = 1,
            WindowStyle = 0,
        });
        Guid interfaceId = typeof(IPropertyStore).GUID;
        Assert.True(NativeMethods.SHGetPropertyStoreForWindow(source.Handle, ref interfaceId, out IPropertyStore store) >= 0);
        PROPERTYKEY key = PROPERTYKEY.AppUserModelId;
        try
        {
            using var value = new PropVariant { VarType = 31, Pointer = Marshal.StringToCoTaskMemUni(CalculatorId) };
            store.SetValue(ref key, value);
            Assert.Equal(CalculatorId, ShellAppResources.GetWindowApplicationId(source.Handle));
            nint handle = source.Handle;
            Assert.Equal(CalculatorId, await Task.Run(() => ShellAppResources.GetWindowApplicationId(handle)));
        }
        finally
        {
            using var empty = new PropVariant();
            store.SetValue(ref key, empty);
            Marshal.ReleaseComObject(store);
        }

        Assert.Null(ShellAppResources.GetWindowApplicationId(source.Handle));
    });

    private static HostedWindowIdentityResolver CreateDetached(Func<nint, IReadOnlyList<nint>>? children = null) => new(
        handle => handle is 1 or 4 or 2 ? 10u : handle == 3 ? 20u : 30u,
        process => process == 10 ? Host : process == 20 ? Calculator : Settings,
        children ?? (_ => []), _ => "Windows.UI.Core.CoreWindow",
        handle => handle == 1 ? CalculatorId : SettingsId,
        process => process == 20 ? CalculatorId : SettingsId,
        () => [2, 5, 3]);

    private static HostedWindowIdentityResolver Create() => new(
        handle => handle is 1 or 4 ? 10u : handle == 2 ? 10u : handle == 3 ? 20u : 30u,
        process => process == 10 ? Host : process == 20 ? Calculator : Settings,
        handle => handle == 1 ? [2, 3] : [5],
        _ => "Windows.UI.Core.CoreWindow");
}
