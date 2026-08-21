using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// Single responsibility: merging pinned apps with running windows into an ordered list
/// of dock items.
///
/// A pure function, no Win32 and no UI — this is where the dock's business rule lives, and that is
/// why it is 100% covered by unit tests.
///
/// Output order:
///   1. Pinned apps, in the order they were pinned (running or not).
///   2. Running apps that are not pinned, in the order they already had in the dock; those appearing
///      for the first time join the end.
/// </summary>
public sealed class DockItemAggregator
{
    /// <param name="previousOrder">
    /// Keys of the items the dock already shows, in the current order. It is what gives unpinned apps a
    /// fixed place: the window list arrives in Z order, which changes on every app switch, and using it
    /// directly would make the icons swap places every time the user changed window.
    /// </param>
    public IReadOnlyList<DockItem> Build(
        IReadOnlyList<PinnedApp> pinnedApps,
        IReadOnlyList<WindowInfo> windows,
        IReadOnlyList<AppKey>? previousOrder = null)
    {
        ArgumentNullException.ThrowIfNull(pinnedApps);
        ArgumentNullException.ThrowIfNull(windows);

        Dictionary<AppKey, List<WindowInfo>> windowsByKey = GroupWindowsByApp(windows);
        var items = new List<DockItem>(pinnedApps.Count + windowsByKey.Count);
        var consumedKeys = new HashSet<AppKey>();

        foreach (PinnedApp pinned in pinnedApps)
        {
            AppKey key = pinned.Key;
            if (!consumedKeys.Add(key))
            {
                // Two pinned items pointing at the same executable: only the first is kept.
                continue;
            }

            IReadOnlyList<WindowInfo> appWindows = TakeWindows(windowsByKey, key);
            items.Add(new DockItem(key, pinned.DisplayName, pinned.IconSource, pinned, appWindows));
        }

        var running = new List<DockItem>();

        foreach (WindowInfo window in windows)
        {
            AppKey key = window.Key;
            if (key.IsEmpty || !consumedKeys.Add(key))
            {
                continue;
            }

            IReadOnlyList<WindowInfo> appWindows = TakeWindows(windowsByKey, key);
            if (appWindows.Count == 0)
            {
                continue;
            }

            running.Add(new DockItem(key, window.DisplayName, window.ExecutablePath, null, appWindows));
        }

        items.AddRange(SortByPreviousOrder(running, previousOrder));

        return items;
    }

    /// <summary>
    /// Returns the unpinned apps in the place they already had. The sort is stable, so those that
    /// were not in the dock end up last, among themselves in the order the windows were enumerated.
    /// </summary>
    private static IEnumerable<DockItem> SortByPreviousOrder(
        List<DockItem> running,
        IReadOnlyList<AppKey>? previousOrder)
    {
        if (previousOrder is null || previousOrder.Count == 0 || running.Count < 2)
        {
            return running;
        }

        var position = new Dictionary<AppKey, int>(previousOrder.Count);

        for (int index = 0; index < previousOrder.Count; index++)
        {
            position.TryAdd(previousOrder[index], index);
        }

        return running.OrderBy(item => position.TryGetValue(item.Key, out int index) ? index : int.MaxValue);
    }

    private static Dictionary<AppKey, List<WindowInfo>> GroupWindowsByApp(IReadOnlyList<WindowInfo> windows)
    {
        var grouped = new Dictionary<AppKey, List<WindowInfo>>();

        foreach (WindowInfo window in windows)
        {
            if (window.Key.IsEmpty)
            {
                // With no executable path there is no way to group or to match a pinned item.
                continue;
            }

            if (!grouped.TryGetValue(window.Key, out List<WindowInfo>? bucket))
            {
                bucket = [];
                grouped[window.Key] = bucket;
            }

            bucket.Add(window);
        }

        return grouped;
    }

    private static IReadOnlyList<WindowInfo> TakeWindows(
        Dictionary<AppKey, List<WindowInfo>> windowsByKey,
        AppKey key)
    {
        if (key.IsEmpty || !windowsByKey.TryGetValue(key, out List<WindowInfo>? bucket))
        {
            return [];
        }

        return bucket;
    }
}
