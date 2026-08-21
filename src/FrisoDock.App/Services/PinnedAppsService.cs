using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

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

    private IReadOnlyList<PinnedApp> _apps;

    public PinnedAppsService(IPinnedAppStore store)
    {
        _store = store;
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

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static PinnedApp CreatePinnedApp(DockItem item)
    {
        string? executable = item.Windows.FirstOrDefault()?.ExecutablePath ?? item.IconSource;

        return new PinnedApp(
            item.DisplayName,
            executable ?? item.Key.Value,
            MatchExecutablePath: executable);
    }
}
