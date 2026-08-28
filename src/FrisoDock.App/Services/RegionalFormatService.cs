using System.Globalization;
using System.Windows.Threading;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Applies a regional format change without restarting the dock. That is all (SRP): detecting
/// the change belongs to <see cref="IRegionalFormatWatcher"/>, and reacting to the new metrics
/// to whoever already listens to the settings.
///
/// Two things have to happen here, in order. First, .NET caches the culture it read at startup:
/// without <see cref="CultureInfo.ClearCachedData"/> the new region would never be seen — the
/// formatter and the calendar builder read the current culture on every call, but "current"
/// would still be the stale cache. Second, the clock widths were measured in the old culture
/// and enter the panel geometry: re-measuring and pushing the result through the settings takes
/// the existing MetricsChanged route, and the panel resizes exactly as it does when the
/// seconds are turned on.
/// </summary>
public sealed class RegionalFormatService : IDisposable
{
    private readonly IRegionalFormatWatcher _watcher;
    private readonly DockSettingsService _settings;
    private readonly DispatcherTimer _timer;

    private bool _disposed;

    public RegionalFormatService(IRegionalFormatWatcher watcher, DockSettingsService settings)
    {
        _watcher = watcher;
        _settings = settings;

        // Every open dock forwards the same broadcast, and Windows can send it more than once
        // per change; without coalescing the burst, the clock would be re-measured once per dock.
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _timer.Tick += OnTick;
    }

    public void Start()
    {
        _watcher.RegionalFormatChanged += OnRegionalFormatChanged;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watcher.RegionalFormatChanged -= OnRegionalFormatChanged;
        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }

    private void OnRegionalFormatChanged(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();

        CultureInfo.CurrentCulture.ClearCachedData();

        // The formatter is created after the cache is cleared, so it measures in the new
        // culture. Update ignores a no-change on purpose: if the new region has the same
        // widths, nothing moves — and the clock text itself catches up on its next tick.
        Core.Models.DockSettings current = _settings.Current;
        _settings.Update(current with
        {
            Metrics = ClockWidthMeasurer.Measure(current.Metrics, new ClockFormatter()),
        });
    }
}
