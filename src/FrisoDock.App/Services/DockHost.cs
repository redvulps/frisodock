using System.Windows.Threading;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FrisoDock.App.Services;

/// <summary>
/// Owner of the dock set: creates one per monitor, or only on the primary, as configured.
/// That is all (SRP) — what each dock does is its own business.
///
/// Each dock is born in its own container scope. The scope is what gives each one its
/// <see cref="DockPlacementService"/>, its appbar and its view model, without construction
/// having to pass dependency after dependency along.
/// </summary>
public sealed class DockHost : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IScreenProvider _screens;
    private readonly DockSettingsService _settings;
    private readonly IDisplayWatcher _displayWatcher;
    private readonly List<DockInstance> _docks = [];
    private readonly DispatcherTimer _rebuildTimer;

    private bool _disposed;

    public DockHost(
        IServiceProvider services,
        IScreenProvider screens,
        DockSettingsService settings,
        IDisplayWatcher displayWatcher)
    {
        _services = services;
        _screens = screens;
        _settings = settings;
        _displayWatcher = displayWatcher;

        // Changing monitors produces a burst of messages, and every open dock forwards its own.
        // Without coalescing the burst, the set would be rebuilt once per dock.
        _rebuildTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _rebuildTimer.Tick += OnRebuildTick;
    }

    /// <summary>Dock of the primary monitor, which the application uses as its main window.</summary>
    public DockWindow? PrimaryWindow => _docks.Count == 0 ? null : _docks[0].Window;

    public void Start()
    {
        _settings.Changed += OnSettingsChanged;
        _displayWatcher.DisplayChanged += OnDisplayChanged;

        Rebuild();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _settings.Changed -= OnSettingsChanged;
        _displayWatcher.DisplayChanged -= OnDisplayChanged;

        _rebuildTimer.Stop();
        _rebuildTimer.Tick -= OnRebuildTick;

        CloseAll();
        _disposed = true;
    }

    private void OnSettingsChanged(object? sender, DockSettingsChangedEventArgs e)
    {
        if (e.MonitorLayoutChanged)
        {
            Rebuild();
        }
    }

    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        _rebuildTimer.Stop();
        _rebuildTimer.Start();
    }

    private void OnRebuildTick(object? sender, EventArgs e)
    {
        _rebuildTimer.Stop();
        Rebuild();
    }

    /// <summary>
    /// Rebuilds the whole set.
    ///
    /// Closing and reopening, instead of patching the existing windows, is a conscious decision:
    /// changing resolution changes geometry, DPI and each dock's reserved band, and reusing windows
    /// would require reopening the appbar and redoing the hosting anyway. It only happens when the
    /// user touches the monitors.
    /// </summary>
    private void Rebuild()
    {
        CloseAll();

        foreach (DockMonitor monitor in ResolveMonitors())
        {
            Create(monitor);
        }
    }

    private IReadOnlyList<DockMonitor> ResolveMonitors()
    {
        IReadOnlyList<MonitorInfo> monitors = _screens.GetMonitors();

        if (monitors.Count == 0)
        {
            // No monitor enumerated: the dock still has to exist somewhere.
            return [DockMonitor.Single(_screens.GetPrimaryMonitor())];
        }

        PixelRect[] bounds = monitors.Select(monitor => monitor.Bounds).ToArray();

        if (_settings.Current.ShowOnAllMonitors)
        {
            // The primary comes first: it is the one the application uses as its main window.
            return Enumerable.Range(0, monitors.Count)
                .OrderByDescending(index => monitors[index].IsPrimary)
                .Select(index => new DockMonitor(monitors[index], index, bounds))
                .ToArray();
        }

        int primary = FindPrimaryIndex(monitors);

        return [new DockMonitor(monitors[primary], primary, bounds)];
    }

    private static int FindPrimaryIndex(IReadOnlyList<MonitorInfo> monitors)
    {
        for (int index = 0; index < monitors.Count; index++)
        {
            if (monitors[index].IsPrimary)
            {
                return index;
            }
        }

        return 0;
    }

    private void Create(DockMonitor monitor)
    {
        IServiceScope scope = _services.CreateScope();

        scope.ServiceProvider.GetRequiredService<DockMonitorHolder>().Monitor = monitor;
        DockWindow window = scope.ServiceProvider.GetRequiredService<DockWindow>();

        _docks.Add(new DockInstance(scope, window));
        window.Show();
    }

    private void CloseAll()
    {
        foreach (DockInstance dock in _docks)
        {
            dock.Window.Close();
            dock.Scope.Dispose();
        }

        _docks.Clear();
    }

    private sealed record DockInstance(IServiceScope Scope, DockWindow Window);
}
