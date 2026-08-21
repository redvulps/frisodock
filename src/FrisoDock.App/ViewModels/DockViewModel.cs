using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FrisoDock.App.Services;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.ViewModels;

/// <summary>
/// State of one dock's bar: keeps the item list up to date as the windows change.
///
/// There is one per monitor with a dock. It does not enumerate windows, group, extract icons or own
/// the pinned list (SRP) — that belongs to the collaborators. What is its own: debouncing the
/// event bursts, separating the windows per monitor when configured, and reconciling the
/// observable collection.
/// </summary>
public sealed partial class DockViewModel : ObservableObject, IDisposable
{
    private readonly IWindowEnumerator _windowEnumerator;
    private readonly PinnedAppsService _pinnedApps;
    private readonly IWindowActivator _activator;
    private readonly IAppLauncher _launcher;
    private readonly IStartMenuInvoker _startMenu;
    private readonly DockItemAggregator _aggregator;
    private readonly WindowMonitorMatcher _monitorMatcher;
    private readonly IconImageProvider _iconProvider;
    private readonly IApplicationLifetime _lifetime;
    private readonly DockSettingsService _settings;
    private readonly DockMonitor _monitor;
    private readonly DispatcherTimer _refreshTimer;

    private bool _disposed;

    /// <summary>
    /// Layout metrics the XAML consumes, derived from the same metrics as the placement.
    ///
    /// It is replaced, not edited, when the settings change the metrics: turning the clock seconds
    /// on widens its band, and the XAML has to follow or the text spills out.
    /// </summary>
    [ObservableProperty]
    private DockAppearance _appearance;

    public DockViewModel(
        IWindowEnumerator windowEnumerator,
        PinnedAppsService pinnedApps,
        IWindowActivator activator,
        IAppLauncher launcher,
        IStartMenuInvoker startMenu,
        DockItemAggregator aggregator,
        WindowMonitorMatcher monitorMatcher,
        IconImageProvider iconProvider,
        IApplicationLifetime lifetime,
        ClockViewModel clock,
        DockSettingsService settings,
        DockMonitorHolder monitor)
    {
        _windowEnumerator = windowEnumerator;
        _pinnedApps = pinnedApps;
        _activator = activator;
        _launcher = launcher;
        _startMenu = startMenu;
        _aggregator = aggregator;
        _monitorMatcher = monitorMatcher;
        _iconProvider = iconProvider;
        _lifetime = lifetime;
        _settings = settings;
        _monitor = monitor.Monitor;

        _appearance = new DockAppearance(settings.Current.EffectiveMetrics);
        Clock = clock;

        // The WinEvents arrive in bursts: opening a window fires several events in a row.
        // The timer coalesces the burst into a single recomputation.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(settings.Current.RefreshDebounceMilliseconds),
        };
        _refreshTimer.Tick += OnRefreshTick;

        _windowEnumerator.WindowsChanged += OnWindowsChanged;
        _settings.Changed += OnSettingsChanged;
        _pinnedApps.Changed += OnPinnedAppsChanged;
    }

    /// <summary>Raised when the item count changes and the dock has to be repositioned.</summary>
    public event EventHandler? LayoutChanged;

    public ObservableCollection<DockItemViewModel> Items { get; } = [];

    /// <summary>Clock fixed at the right end of the dock.</summary>
    public ClockViewModel Clock { get; }

    /// <summary>Reads the current state and starts observing changes.</summary>
    public void Start()
    {
        Refresh();
        _windowEnumerator.Start();
        Clock.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTick;
        _windowEnumerator.WindowsChanged -= OnWindowsChanged;
        _settings.Changed -= OnSettingsChanged;
        _pinnedApps.Changed -= OnPinnedAppsChanged;

        _disposed = true;
    }

    /// <summary>The dock's Start button.</summary>
    [RelayCommand]
    private void OpenStartMenu()
    {
        _startMenu.Toggle();
    }

    /// <summary>
    /// Shuts FrisoDock down from the context menu.
    ///
    /// It goes through the normal WPF lifetime on purpose: it is <c>App.OnExit</c> that restores the
    /// native taskbar and releases the space reserved by the appbar. Killing the process would skip
    /// that path and leave the user with no taskbar.
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        _lifetime.Shutdown();
    }

    private void OnSettingsChanged(object? sender, DockSettingsChangedEventArgs e)
    {
        if (e.MetricsChanged)
        {
            Appearance = new DockAppearance(e.Current.EffectiveMetrics);
        }

        if (e.MonitorIsolationChanged)
        {
            Refresh();
        }
    }

    private void OnPinnedAppsChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void OnWindowsChanged(object? sender, EventArgs e)
    {
        // Restarting the timer on every event extends the coalescing window: we only recompute
        // once the burst has really stopped.
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        _refreshTimer.Stop();
        Refresh();
    }

    private void Refresh()
    {
        IReadOnlyList<WindowInfo> windows = SelectWindows(_windowEnumerator.GetWindows());

        // The current order enters the calculation: without it, unpinned apps would follow the windows'
        // Z order and swap places on every app switch.
        AppKey[] currentOrder = Items.Select(item => item.Key).ToArray();
        IReadOnlyList<DockItem> items = _aggregator.Build(_pinnedApps.Current, windows, currentOrder);

        bool countChanged = Reconcile(items);

        if (countChanged)
        {
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Windows this dock shows. With the monitors isolated, only this monitor's; pinned apps
    /// do not go through here and keep showing up in every dock.
    /// </summary>
    private IReadOnlyList<WindowInfo> SelectWindows(IReadOnlyList<WindowInfo> windows)
    {
        if (!_settings.Current.IsolatesMonitorApps)
        {
            return windows;
        }

        return _monitorMatcher.ForMonitor(windows, _monitor.Index, _monitor.AllBounds);
    }

    /// <summary>
    /// Matches the observable collection to the computed list, reusing the existing view models.
    /// Recreating everything on each update would make the icons flicker and lose the hover state.
    /// </summary>
    private bool Reconcile(IReadOnlyList<DockItem> items)
    {
        int originalCount = Items.Count;

        for (int index = 0; index < items.Count; index++)
        {
            DockItem item = items[index];
            ImageSource? icon = _iconProvider.GetIcon(item);
            int existingIndex = FindExistingIndex(item.Key, index);

            if (existingIndex < 0)
            {
                Items.Insert(index, new DockItemViewModel(item, icon, _activator, _launcher));
                continue;
            }

            if (existingIndex != index)
            {
                Items.Move(existingIndex, index);
            }

            Items[index].Apply(item, icon);
        }

        while (Items.Count > items.Count)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        return Items.Count != originalCount;
    }

    private int FindExistingIndex(AppKey key, int startIndex)
    {
        for (int index = startIndex; index < Items.Count; index++)
        {
            if (Items[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }
}
