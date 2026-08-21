using System.Windows.Threading;
using FrisoDock.Core.Abstractions;

namespace FrisoDock.App.Services;

/// <summary>
/// Keeps FrisoDock in the notification area host role while it runs. That is all (SRP).
///
/// It lives outside the dock windows because there is a single host: the tray belongs to one
/// Shell_TrayWnd window, which <see cref="ITrayHost"/> creates on its own. With a dock on
/// each monitor, all of them show the same icons, read from here.
/// </summary>
public sealed class TrayHostRunner : IDisposable
{
    private readonly ITrayHost _trayHost;

    private DispatcherTimer? _maintenanceTimer;
    private bool _disposed;

    public TrayHostRunner(ITrayHost trayHost)
    {
        _trayHost = trayHost;
    }

    public void Start()
    {
        if (_maintenanceTimer is not null)
        {
            return;
        }

        _trayHost.Start();

        // Windows does not report when Explorer takes the tray back, nor when an app dies without
        // removing its icon; the periodic maintenance covers both cases.
        _maintenanceTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _maintenanceTimer.Tick += OnMaintenanceTick;
        _maintenanceTimer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_maintenanceTimer is not null)
        {
            _maintenanceTimer.Stop();
            _maintenanceTimer.Tick -= OnMaintenanceTick;
            _maintenanceTimer = null;
        }

        _trayHost.Stop();
        _disposed = true;
    }

    private void OnMaintenanceTick(object? sender, EventArgs e)
    {
        _trayHost.Maintain();
    }
}
