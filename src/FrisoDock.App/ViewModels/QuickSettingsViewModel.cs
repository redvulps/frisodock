using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Resources;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// State of the quick settings panel, in the spirit of the Windows 11 panel.
///
/// What can be done without administrator is really here: the radios toggle, and
/// brightness and volume change. What Windows does not expose — airplane mode, battery saver —
/// becomes a shortcut to the matching settings page, which is the same destination as the arrow
/// button of the native panel. A tile that does nothing would be worse than one that leads to who does.
///
/// The state is reread periodically because everything here changes from outside: a function key,
/// the Windows panel itself, another app.
/// </summary>
public sealed partial class QuickSettingsViewModel : ObservableObject, IDisposable
{
    private const string WifiPage = "ms-settings:network-wifi";
    private const string BluetoothPage = "ms-settings:bluetooth";
    private const string AirplanePage = "ms-settings:network-airplanemode";
    private const string AccessibilityPage = "ms-settings:easeofaccess";
    private const string VpnPage = "ms-settings:network-vpn";
    private const string PowerPage = "ms-settings:batterysaver";
    private const string SoundPage = "ms-settings:sound";
    private const string HomePage = "ms-settings:";

    private readonly IRadioController _radios;
    private readonly IBrightnessController _brightness;
    private readonly IVolumeController _volume;
    private readonly IBatteryProvider _battery;
    private readonly INetworkProvider _network;
    private readonly ISettingsPageLauncher _launcher;
    private readonly DispatcherTimer _refreshTimer;

    private readonly QuickTileViewModel _wifiTile;
    private readonly QuickTileViewModel _bluetoothTile;
    private readonly QuickTileViewModel _batterySaverTile;

    // While the state is being reread, touching the properties must not fire commands back to the
    // system — otherwise each update would rewrite the volume and brightness it just read.
    private bool _applying;
    private bool _disposed;
    private readonly RefreshOperation<RadioStatus> _wifiRefresh = new("Wi-Fi");
    private readonly RefreshOperation<RadioStatus> _bluetoothRefresh = new("Bluetooth");
    private readonly RefreshOperation<BrightnessStatus> _brightnessRefresh = new("Brightness");
    private readonly RefreshOperation<VolumeStatus> _volumeRefresh = new("Volume");
    private readonly RefreshOperation<NetworkStatus> _networkRefresh = new("Network");
    private readonly RefreshOperation<BatteryStatus> _batteryRefresh = new("Battery");
    private NetworkStatus _networkStatus = NetworkStatus.Disconnected;
    private int _brightnessRevision;
    private int _volumeRevision;
    private int _wifiRevision;
    private int _bluetoothRevision;
    private Task _brightnessWriteTask = Task.CompletedTask;
    private Task _volumeWriteTask = Task.CompletedTask;

    [ObservableProperty]
    private int _brightnessLevel;

    [ObservableProperty]
    private bool _isBrightnessAvailable;

    [ObservableProperty]
    private int _volumeLevel;

    [ObservableProperty]
    private bool _isMuted;

    [ObservableProperty]
    private bool _isVolumeAvailable;

    [ObservableProperty]
    private bool _hasBattery;

    [ObservableProperty]
    private string _batteryText = string.Empty;

    [ObservableProperty]
    private string _batteryGlyph = string.Empty;

    public QuickSettingsViewModel(
        IRadioController radios,
        IBrightnessController brightness,
        IVolumeController volume,
        IBatteryProvider battery,
        INetworkProvider network,
        ISettingsPageLauncher launcher)
    {
        _radios = radios;
        _brightness = brightness;
        _volume = volume;
        _battery = battery;
        _network = network;
        _launcher = launcher;

        _wifiTile = new QuickTileViewModel("", Strings.QuickWifi, ToggleWifiAsync, () => Open(WifiPage));
        _bluetoothTile = new QuickTileViewModel("", Strings.QuickBluetooth, ToggleBluetoothAsync, () => Open(BluetoothPage));
        _batterySaverTile = new QuickTileViewModel("", Strings.QuickBatterySaver, null, () => Open(PowerPage));

        Tiles =
        [
            _wifiTile,
            _bluetoothTile,
            new QuickTileViewModel("", Strings.QuickAirplaneMode, null, () => Open(AirplanePage)),
            new QuickTileViewModel("", Strings.QuickAccessibility, null, () => Open(AccessibilityPage)),
            new QuickTileViewModel("", Strings.QuickVpn, null, () => Open(VpnPage)),
            _batterySaverTile,
        ];

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += OnRefreshTick;
    }

    public IReadOnlyList<QuickTileViewModel> Tiles { get; }

    internal Task BrightnessWriteTask => _brightnessWriteTask;
    internal Task VolumeWriteTask => _volumeWriteTask;

    /// <summary>Starts tracking the changes. Only while the panel is open.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _refreshTimer.Start();
        _ = RefreshAsync();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTick;
        _disposed = true;
    }

    partial void OnBrightnessLevelChanged(int value)
    {
        if (!_applying && !_disposed)
        {
            _brightnessRevision++;
            _brightnessWriteTask = WriteBrightnessAsync(value);
        }
    }

    partial void OnVolumeLevelChanged(int value)
    {
        if (_applying || _disposed)
        {
            return;
        }

        // Touching the volume unmutes, as the Windows control does.
        if (IsMuted && value > 0)
        {
            ApplyRead(() => IsMuted = false);
        }

        _volumeRevision++;
        _volumeWriteTask = WriteVolumeAsync(value, IsMuted);
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (!_applying && !_disposed)
        {
            _volumeRevision++;
            _volumeWriteTask = WriteVolumeAsync(null, value);
        }
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private void OpenSound()
    {
        Open(SoundPage);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        Open(HomePage);
    }

    [RelayCommand]
    private void OpenPower()
    {
        Open(PowerPage);
    }

    private async Task ToggleWifiAsync()
    {
        int revision = ++_wifiRevision;
        RadioStatus status = await _radios.SetStateAsync(RadioDeviceKind.WiFi, !_wifiTile.IsOn);
        if (!_disposed && revision == _wifiRevision)
        {
            ApplyRadio(_wifiTile, status);
            UpdateWifiDetail();
        }
    }

    private async Task ToggleBluetoothAsync()
    {
        int revision = ++_bluetoothRevision;
        RadioStatus status = await _radios.SetStateAsync(RadioDeviceKind.Bluetooth, !_bluetoothTile.IsOn);
        if (!_disposed && revision == _bluetoothRevision)
        {
            ApplyRadio(_bluetoothTile, status);
        }
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    /// <summary>Refreshes sources independently, skipping only sources with an outstanding query or write.</summary>
    public Task RefreshAsync()
    {
        if (_disposed)
        {
            return Task.CompletedTask;
        }

        int brightnessRevision = _brightnessRevision;
        int volumeRevision = _volumeRevision;
        int wifiRevision = _wifiRevision;
        int bluetoothRevision = _bluetoothRevision;
        return Task.WhenAll(
            _wifiRefresh.RunAsync(() => _radios.GetStatusAsync(RadioDeviceKind.WiFi), status =>
            {
                if (!_disposed && wifiRevision == _wifiRevision)
                {
                    ApplyRadio(_wifiTile, status);
                    UpdateWifiDetail();
                }
            }),
            _bluetoothRefresh.RunAsync(() => _radios.GetStatusAsync(RadioDeviceKind.Bluetooth), status =>
            {
                if (!_disposed && bluetoothRevision == _bluetoothRevision)
                {
                    ApplyRadio(_bluetoothTile, status);
                }
            }),
            _brightnessWriteTask.IsCompleted
                ? _brightnessRefresh.RunAsync(_brightness.GetStatusAsync, status => ApplyRead(() =>
                {
                    IsBrightnessAvailable = status.IsAvailable;
                    if (brightnessRevision == _brightnessRevision && _brightnessWriteTask.IsCompleted)
                    {
                        BrightnessLevel = status.Level;
                    }
                })) : Task.CompletedTask,
            _volumeWriteTask.IsCompleted
                ? _volumeRefresh.RunAsync(_volume.GetStatusAsync, status => ApplyRead(() =>
                {
                    IsVolumeAvailable = status.IsAvailable;
                    if (volumeRevision == _volumeRevision && _volumeWriteTask.IsCompleted)
                    {
                        VolumeLevel = status.Level;
                        IsMuted = status.IsMuted;
                    }
                })) : Task.CompletedTask,
            _networkRefresh.RunAsync(() => Task.Run(_network.GetStatus), status =>
            {
                if (!_disposed)
                {
                    _networkStatus = status;
                    UpdateWifiDetail();
                }
            }),
            _batteryRefresh.RunAsync(() => Task.Run(_battery.GetStatus), status =>
            {
                if (!_disposed)
                {
                    ApplyBattery(status);
                }
            }));
    }

    private void UpdateWifiDetail() => _wifiTile.Detail = _wifiTile.IsOn && _networkStatus.IsWireless ? _networkStatus.Name : null;

    private void ApplyRead(Action apply)
    {
        if (_disposed)
        {
            return;
        }

        bool wasApplying = _applying;
        _applying = true;
        try
        {
            apply();
        }
        finally
        {
            _applying = wasApplying;
        }
    }

    private async Task WriteBrightnessAsync(int level)
    {
        try
        {
            await _brightness.SetLevelAsync(level);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.TraceError("Brightness write failed: {0}", exception);
        }
    }

    private async Task WriteVolumeAsync(int? level, bool? isMuted)
    {
        try
        {
            await _volume.SetStateAsync(level, isMuted);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.TraceError("Volume write failed: {0}", exception);
        }
    }

    private void ApplyBattery(BatteryStatus battery)
    {
        HasBattery = battery.HasBattery;
        _batterySaverTile.IsOn = battery.IsSaverOn;

        if (!battery.HasBattery)
        {
            return;
        }

        BatteryText = $"{battery.Percent}%";
        BatteryGlyph = BatteryGlyphs.For(battery);
    }

    private static void ApplyRadio(QuickTileViewModel tile, RadioStatus status)
    {
        tile.IsAvailable = status.IsAvailable;
        tile.IsOn = status.IsOn;
    }

    private void Open(string page)
    {
        _launcher.Open(page);
    }
}
