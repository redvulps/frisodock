using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private bool _refreshing;
    private int _brightnessRevision;
    private int _volumeRevision;
    private int _wifiRevision;
    private int _bluetoothRevision;
    private Task _brightnessWriteTask = Task.CompletedTask;

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
        if (_applying)
        {
            return;
        }

        _volume.SetLevel(value);
        _volumeRevision++;

        // Touching the volume unmutes, as the Windows control does.
        if (IsMuted && value > 0)
        {
            IsMuted = false;
        }
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (!_applying)
        {
            _volumeRevision++;
            _volume.SetMuted(value);
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

    /// <summary>Reads hardware without blocking the dispatcher or overlapping another refresh.</summary>
    public async Task RefreshAsync()
    {
        if (_disposed || _refreshing)
        {
            return;
        }

        _refreshing = true;
        int brightnessRevision = _brightnessRevision;
        int volumeRevision = _volumeRevision;
        int wifiRevision = _wifiRevision;
        int bluetoothRevision = _bluetoothRevision;
        try
        {
            Task<RadioStatus> wifiTask = _radios.GetStatusAsync(RadioDeviceKind.WiFi);
            Task<RadioStatus> bluetoothTask = _radios.GetStatusAsync(RadioDeviceKind.Bluetooth);
            Task<BrightnessStatus> brightnessTask = _brightness.GetStatusAsync();
            var systemTask = Task.Run(() => (_network.GetStatus(), _volume.GetStatus(), _battery.GetStatus()));
            await Task.WhenAll(wifiTask, bluetoothTask, brightnessTask, systemTask);
            if (_disposed)
            {
                return;
            }

            _applying = true;
            if (wifiRevision == _wifiRevision)
            {
                ApplyRadio(_wifiTile, await wifiTask);
            }

            if (bluetoothRevision == _bluetoothRevision)
            {
                ApplyRadio(_bluetoothTile, await bluetoothTask);
            }

            (NetworkStatus network, VolumeStatus volume, BatteryStatus battery) = await systemTask;
            _wifiTile.Detail = _wifiTile.IsOn && network.IsWireless ? network.Name : null;

            BrightnessStatus brightness = await brightnessTask;
            IsBrightnessAvailable = brightness.IsAvailable;
            if (brightnessRevision == _brightnessRevision && _brightnessWriteTask.IsCompleted)
            {
                BrightnessLevel = brightness.Level;
            }

            IsVolumeAvailable = volume.IsAvailable;
            if (volumeRevision == _volumeRevision)
            {
                VolumeLevel = volume.Level;
                IsMuted = volume.IsMuted;
            }

            ApplyBattery(battery);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            System.Diagnostics.Trace.TraceError("Quick settings refresh failed: {0}", exception);
        }
        finally
        {
            _applying = false;
            _refreshing = false;
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
