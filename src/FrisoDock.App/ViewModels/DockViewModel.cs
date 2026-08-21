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
/// State of the dock bar: it keeps the item list up to date as the windows change.
///
/// It does not enumerate windows, group or extract icons (SRP) — that belongs to the collaborators.
/// What is its own: debouncing the event bursts and reconciling the observable collection.
/// </summary>
public sealed partial class DockViewModel : ObservableObject, IPinnedAppsEditor, IDisposable
{
    private readonly IWindowEnumerator _windowEnumerator;
    private readonly IPinnedAppStore _pinnedAppStore;
    private readonly IWindowActivator _activator;
    private readonly IAppLauncher _launcher;
    private readonly IStartMenuInvoker _startMenu;
    private readonly DockItemAggregator _aggregator;
    private readonly IconImageProvider _iconProvider;
    private readonly IApplicationLifetime _lifetime;
    private readonly DispatcherTimer _refreshTimer;

    private IReadOnlyList<PinnedApp> _pinnedApps;
    private bool _disposed;

    public DockViewModel(
        IWindowEnumerator windowEnumerator,
        IPinnedAppStore pinnedAppStore,
        IWindowActivator activator,
        IAppLauncher launcher,
        IStartMenuInvoker startMenu,
        DockItemAggregator aggregator,
        IconImageProvider iconProvider,
        IApplicationLifetime lifetime,
        ClockViewModel clock,
        DockSettings settings)
    {
        _windowEnumerator = windowEnumerator;
        _pinnedAppStore = pinnedAppStore;
        _activator = activator;
        _launcher = launcher;
        _startMenu = startMenu;
        _aggregator = aggregator;
        _iconProvider = iconProvider;
        _lifetime = lifetime;

        _pinnedApps = _pinnedAppStore.Load();
        Appearance = new DockAppearance(settings.Metrics);
        Clock = clock;

        // The WinEvents arrive in bursts: opening a window fires several events in a row.
        // The timer coalesces the burst into a single recomputation.
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(settings.RefreshDebounceMilliseconds),
        };
        _refreshTimer.Tick += OnRefreshTick;

        _windowEnumerator.WindowsChanged += OnWindowsChanged;
    }

    /// <summary>Raised when the item count changes and the dock has to be repositioned.</summary>
    public event EventHandler? LayoutChanged;

    public ObservableCollection<DockItemViewModel> Items { get; } = [];

    /// <summary>Layout metrics the XAML consumes, derived from the same metrics as the placement.</summary>
    public DockAppearance Appearance { get; }

    /// <summary>Clock fixed at the right end of the dock.</summary>
    public ClockViewModel Clock { get; }

    /// <summary>Reads the current state and starts observing changes.</summary>
    public void Start()
    {
        Refresh();
        _windowEnumerator.Start();
        Clock.Start();
    }

    public bool IsPinned(AppKey key)
    {
        return _pinnedApps.Any(app => app.Key == key);
    }

    /// <summary>
    /// Pins or unpins the app and writes the list. Unpinning an app that is not running makes the
    /// item disappear from the dock, the same as the native taskbar does.
    /// </summary>
    public void TogglePin(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var updated = _pinnedApps.ToList();
        int existingIndex = updated.FindIndex(app => app.Key == item.Key);

        if (existingIndex >= 0)
        {
            updated.RemoveAt(existingIndex);
        }
        else
        {
            updated.Add(CreatePinnedApp(item));
        }

        _pinnedApps = updated;
        _pinnedAppStore.Save(updated);

        Refresh();
    }

    private static PinnedApp CreatePinnedApp(DockItem item)
    {
        string? executable = item.Windows.FirstOrDefault()?.ExecutablePath ?? item.IconSource;

        return new PinnedApp(
            item.DisplayName,
            executable ?? item.Key.Value,
            MatchExecutablePath: executable);
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
        _windowEnumerator.Stop();
        Clock.Dispose();

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
        IReadOnlyList<WindowInfo> windows = _windowEnumerator.GetWindows();
        IReadOnlyList<DockItem> items = _aggregator.Build(_pinnedApps, windows);

        bool countChanged = Reconcile(items);

        if (countChanged)
        {
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }
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
