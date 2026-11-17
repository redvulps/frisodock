using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// Owner of the pinned app list. That is all (SRP): writing belongs to <see cref="IPinnedAppStore"/>.
///
/// It moved out of the bar view model when the dock could exist on more than one monitor: the
/// list is a single one, and pinning an app from one screen's dock has to show up in the others.
/// </summary>
public sealed class PinnedAppsService : IPinnedAppsEditor
{
    private readonly IPinnedAppStore _store;
    private readonly PinnedAppsReorder _reorder;

    private IReadOnlyList<PinnedApp> _apps;
    private bool _pendingReorder;

    public PinnedAppsService(IPinnedAppStore store, PinnedAppsReorder reorder)
    {
        _store = store;
        _reorder = reorder;
        _apps = _store.Load();
    }

    /// <summary>Raised when the list changes, so each dock can update itself.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<PinnedApp> Current => _apps;

    public bool IsPinned(AppKey key)
    {
        return _apps.Any(app => app.Key == key);
    }

    /// <summary>
    /// Pins the app if it is not pinned, unpins it if it is. Unpinning an app that is not
    /// running makes the item disappear from the dock, the same as the native taskbar does.
    /// </summary>
    public void TogglePin(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var updated = _apps.ToList();
        int existingIndex = updated.FindIndex(app => app.Key == item.Key);

        if (existingIndex >= 0)
        {
            updated.RemoveAt(existingIndex);
        }
        else
        {
            updated.Add(CreatePinnedApp(item));
        }

        _apps = updated;
        _store.Save(updated);
        _pendingReorder = false;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves a pinned app, optionally deferring the save until the drag finishes.</summary>
    public void Reorder(AppKey key, int targetIndex, bool persist = true)
    {
        IReadOnlyList<PinnedApp> updated = _reorder.Move(_apps, key, targetIndex);

        if (ReferenceEquals(updated, _apps))
        {
            return;
        }

        _apps = updated;
        _pendingReorder = true;
        if (persist)
        {
            CommitReorder();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void CommitReorder()
    {
        if (_pendingReorder)
        {
            _store.Save(_apps);
            _pendingReorder = false;
        }
    }

    private static PinnedApp CreatePinnedApp(DockItem item)
    {
        WindowInfo? window = item.Windows.FirstOrDefault();
        string? executable = window?.ExecutablePath ?? item.IconSource;

        return new PinnedApp(
            item.DisplayName,
            window?.ShellLaunchTarget ?? executable ?? item.Key.Value,
            IconPath: window?.ShellLaunchTarget,
            MatchExecutablePath: executable);
    }
}
