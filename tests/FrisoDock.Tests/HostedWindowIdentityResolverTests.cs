using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Services;

namespace FrisoDock.Tests;

public sealed class HostedWindowIdentityResolverTests
{
    private const string Host = @"C:\Windows\System32\ApplicationFrameHost.exe";
    private const string Calculator = @"C:\Program Files\WindowsApps\Calculator\CalculatorApp.exe";
    private const string Settings = @"C:\Windows\ImmersiveControlPanel\SystemSettings.exe";

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
            _ => throw new Exception("Unexpected child scan"), _ => "");
        WindowProcessIdentity identity = resolver.Resolve(1, "A document title");

        Assert.Equal(Calculator, identity.ExecutablePath);
        Assert.Null(identity.DisplayName);
    }

    private static HostedWindowIdentityResolver Create() => new(
        handle => handle is 1 or 4 ? 10u : handle == 2 ? 10u : handle == 3 ? 20u : 30u,
        process => process == 10 ? Host : process == 20 ? Calculator : Settings,
        handle => handle == 1 ? [2, 3] : [5],
        _ => "Windows.UI.Core.CoreWindow");
}
