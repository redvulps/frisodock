using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// The dock settings: value normalization, persistence and the headroom magnification
/// demands from the window.
/// </summary>
public sealed class DockSettingsTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), $"dockin-settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void MagnificationOff_NeutralizesTheFactor()
    {
        var settings = new DockSettings { EnableMagnification = false, MagnificationScale = 1.8 };

        Assert.Equal(1.0, settings.EffectiveMagnification);
    }

    [Fact]
    public void MagnificationFactor_IsClampedToAUsableRange()
    {
        // A hand-edited file must not shrink the icon or make the dock cover the screen.
        Assert.Equal(1.0, new DockSettings { MagnificationScale = 0.2 }.EffectiveMagnification);
        Assert.Equal(2.0, new DockSettings { MagnificationScale = 9.0 }.EffectiveMagnification);
    }

    [Fact]
    public void Headroom_IsComputedFromTheIconSize()
    {
        var metrics = new DockMetrics(IconSize: 40);

        // 40 px growing by 50% need 20 px of headroom outside the panel.
        Assert.Equal(20, metrics.CalculateMagnificationHeadroom(1.5));
    }

    [Fact]
    public void WithoutMagnification_ThereIsNoHeadroom()
    {
        Assert.Equal(0, new DockMetrics(IconSize: 40).CalculateMagnificationHeadroom(1.0));
    }

    [Fact]
    public void TheWindowIsLargerThanThePanel_ExactlyByTheHeadroom()
    {
        var metrics = new DockMetrics(IconSize: 40);
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var monitor = new MonitorInfo(bounds, bounds, IsPrimary: true, DpiScale: 1.0);
        var calculator = new DockLayoutCalculator();

        PixelRect panel = calculator.CalculatePanelRect(monitor, DockEdge.Bottom, 3, metrics);
        PixelRect window = calculator.CalculateWindowRect(monitor, DockEdge.Bottom, 3, metrics, 1.5);

        Assert.Equal(panel.Width, window.Width);
        Assert.Equal(panel.Height + 20, window.Height);

        // The headroom sits above the panel: the base stays on the same line.
        Assert.Equal(panel.Bottom, window.Bottom);
        Assert.Equal(panel.Top - 20, window.Top);
    }

    [Fact]
    public void WithoutMagnification_TheWindowIsThePanel()
    {
        var metrics = DockMetrics.Default;
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var monitor = new MonitorInfo(bounds, bounds, IsPrimary: true, DpiScale: 1.0);
        var calculator = new DockLayoutCalculator();

        PixelRect panel = calculator.CalculatePanelRect(monitor, DockEdge.Bottom, 3, metrics);
        PixelRect window = calculator.CalculateWindowRect(monitor, DockEdge.Bottom, 3, metrics, 1.0);

        Assert.Equal(panel, window);
    }

    [Fact]
    public void TheHeadroomDoesNotEnterTheSpaceReservation()
    {
        // The headroom is transparent area: reserving it would steal screen space for no reason.
        var metrics = DockMetrics.Default;
        var bounds = new PixelRect(0, 0, 1920, 1080);
        var monitor = new MonitorInfo(bounds, bounds, IsPrimary: true, DpiScale: 1.0);
        var calculator = new DockLayoutCalculator();

        PixelRect semAmpliacao = calculator.CalculateReservationRect(monitor, DockEdge.Bottom, metrics);

        Assert.Equal(metrics.ReservedThickness, semAmpliacao.Height);
    }

    [Fact]
    public void Store_WithNoFile_ReturnsTheDefaults()
    {
        DockSettings settings = new JsonDockSettingsStore(_filePath).Load();

        Assert.True(settings.EnableMagnification);
        Assert.True(settings.HideNativeTaskbar);
    }

    [Fact]
    public void Store_WritesAndReadsBack()
    {
        var store = new JsonDockSettingsStore(_filePath);
        var saved = new DockSettings
        {
            EnableMagnification = false,
            MagnificationScale = 1.8,
            EnableWindowPreviews = false,
            HideNativeTaskbar = false,
            ReserveScreenSpace = false,
        };

        store.Save(saved);
        DockSettings loaded = new JsonDockSettingsStore(_filePath).Load();

        Assert.False(loaded.EnableMagnification);
        Assert.Equal(1.8, loaded.MagnificationScale);
        Assert.False(loaded.EnableWindowPreviews);
        Assert.False(loaded.HideNativeTaskbar);
        Assert.False(loaded.ReserveScreenSpace);
    }

    [Fact]
    public void Store_WithACorruptFile_ReturnsTheDefaults()
    {
        File.WriteAllText(_filePath, "{ isto não é json");

        Assert.True(new JsonDockSettingsStore(_filePath).Load().EnableMagnification);
    }

    [Fact]
    public void Service_NotifiesAndWritesOnChange()
    {
        var store = new JsonDockSettingsStore(_filePath);
        var service = new DockSettingsService(store, new DockSettings());
        DockSettingsChangedEventArgs? received = null;

        service.Changed += (_, args) => received = args;
        service.Update(service.Current with { EnableMagnification = false });

        Assert.NotNull(received);
        Assert.True(received!.LayoutChanged);
        Assert.False(new JsonDockSettingsStore(_filePath).Load().EnableMagnification);
    }

    [Fact]
    public void Service_IgnoresAnUpdateWithNoChange()
    {
        var service = new DockSettingsService(new JsonDockSettingsStore(_filePath), new DockSettings());
        int notifications = 0;

        service.Changed += (_, _) => notifications++;
        service.Update(service.Current with { });

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Service_TellsApartWhatHasToBeReapplied()
    {
        var service = new DockSettingsService(new JsonDockSettingsStore(_filePath), new DockSettings());
        DockSettingsChangedEventArgs? received = null;
        service.Changed += (_, args) => received = args;

        service.Update(service.Current with { EnableWindowPreviews = false });

        // Thumbnails touch neither the geometry nor the environment.
        Assert.NotNull(received);
        Assert.False(received!.LayoutChanged);
        Assert.False(received.TaskbarVisibilityChanged);
        Assert.False(received.ScreenReservationChanged);
    }

    [Fact]
    public void CommandLineFlags_PreserveTheRest()
    {
        // Regression: ApplyTo built a DockSettings from scratch and silently zeroed
        // any new field that was not listed there.
        var carregado = new DockSettings { EnableMagnification = false, MagnificationScale = 1.9 };
        CommandLineOptions options = CommandLineOptions.Parse(["--keep-taskbar"]);

        DockSettings resultado = options.ApplyTo(carregado);

        Assert.False(resultado.HideNativeTaskbar);
        Assert.False(resultado.EnableMagnification);
        Assert.Equal(1.9, resultado.MagnificationScale);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }
    }
}
