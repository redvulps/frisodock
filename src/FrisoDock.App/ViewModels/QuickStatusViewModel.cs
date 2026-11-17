using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// The icon group that opens the quick settings panel: network, volume and battery.
///
/// It plays the same role as the right corner of the Windows taskbar — the icons are not decoration,
/// they are the button, and they have to tell the state without the panel being open.
///
/// The read happens every five seconds, and not every second: volume costs a COM activation, and
/// nobody stares at a 16 pixel icon waiting for the bar to change. With the panel open,
/// it is the panel that updates every second.
/// </summary>
public sealed partial class QuickStatusViewModel : ObservableObject, IDisposable
{
    private const string NetworkConnected = "";
    private const string NetworkWired = "";
    private const string NetworkOffline = "";
    private const string VolumeAudible = "";
    private const string VolumeMuted = "";

    private readonly IVolumeController _volume;
    private readonly IBatteryProvider _battery;
    private readonly INetworkProvider _network;
    private readonly DispatcherTimer _timer;
    private readonly RefreshOperation<NetworkStatus> _networkRefresh = new("Dock network");
    private readonly RefreshOperation<VolumeStatus> _volumeRefresh = new("Dock volume");
    private readonly RefreshOperation<BatteryStatus> _batteryRefresh = new("Dock battery");
    private NetworkStatus _networkStatus = NetworkStatus.Disconnected;
    private VolumeStatus _volumeStatus = VolumeStatus.Unavailable;
    private BatteryStatus _batteryStatus = BatteryStatus.None;

    private bool _disposed;

    [ObservableProperty]
    private string _networkGlyph = NetworkOffline;

    [ObservableProperty]
    private string _volumeGlyph = VolumeAudible;

    [ObservableProperty]
    private string _batteryGlyph = string.Empty;

    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    private string _tooltip = Strings.QuickTitle;

    public QuickStatusViewModel(IVolumeController volume, IBatteryProvider battery, INetworkProvider network)
    {
        _volume = volume;
        _battery = battery;
        _network = network;

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        _timer.Tick += OnTick;

    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Start();
        _ = RefreshAsync();
    }

    /// <summary>Rereads now. Used when the panel closes, so the group reflects what was changed.</summary>
    public Task RefreshAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(
            _networkRefresh.RunAsync(() => Task.Run(_network.GetStatus), network =>
            {
                if (!_disposed)
                {
                    _networkStatus = network;
                    NetworkGlyph = network switch
                    {
                        { Name: null } => NetworkOffline,
                        { IsWireless: true } => NetworkConnected,
                        _ => NetworkWired,
                    };
                    UpdateTooltip();
                }
            }),
            _volumeRefresh.RunAsync(_volume.GetStatusAsync, volume =>
            {
                if (!_disposed)
                {
                    _volumeStatus = volume;
                    VolumeGlyph = volume.IsMuted ? VolumeMuted : VolumeAudible;
                    UpdateTooltip();
                }
            }),
            _batteryRefresh.RunAsync(() => Task.Run(_battery.GetStatus), battery =>
            {
                if (!_disposed)
                {
                    _batteryStatus = battery;
                    HasBattery = battery.HasBattery;
                    BatteryGlyph = battery.HasBattery ? BatteryGlyphs.For(battery) : string.Empty;
                    UpdateTooltip();
                }
            }));
    }

    private void UpdateTooltip() => Tooltip = BuildTooltip(_networkStatus, _volumeStatus, _batteryStatus);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }

    private static string BuildTooltip(NetworkStatus network, VolumeStatus volume, BatteryStatus battery)
    {
        var parts = new List<string> { network.Name ?? Strings.QuickNoConnection };

        if (volume.IsAvailable)
        {
            parts.Add(volume.IsMuted
                ? Strings.QuickMuted
                : string.Format(CultureInfo.CurrentCulture, Strings.QuickVolumeFormat, volume.Level));
        }

        if (battery.HasBattery)
        {
            parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.QuickBatteryFormat, battery.Percent));
        }

        return string.Join("  ·  ", parts);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }
}
