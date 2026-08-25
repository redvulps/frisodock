using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// Builds the switcher list grouped by application: one item per app, and not one per window.
/// A pure function, no Win32 and no UI.
///
/// It is the macOS and Linux desktop model — switching between <i>apps</i>, and on
/// reaching an app entering through the window that was in use. The Windows switcher lists
/// loose windows, and with a ten-tab browser that fills the list with entries the user
/// cannot tell apart.
/// </summary>
public sealed class WindowSwitchList
{
    /// <summary>
    /// Groups the windows by app, preserving the order in which they arrived.
    ///
    /// The input order has to be the Windows Z order — which is what
    /// <c>EnumWindows</c> returns —, and that is precisely most recent use: the activated window
    /// rises to the top. Hence both levels coming for free, with no history to maintain: the
    /// apps end up in usage order, and within each one the first window is the last used.
    ///
    /// It deliberately does not reuse the dock's stable order. There the Z order is a
    /// problem, because it would make the icons dance on every app switch; here it is the data.
    /// </summary>
    /// <param name="windows">Windows in Z order, from the most recent to the oldest.</param>
    public IReadOnlyList<DockItem> Build(IReadOnlyList<WindowInfo> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var order = new List<AppKey>();
        var grouped = new Dictionary<AppKey, List<WindowInfo>>();

        foreach (WindowInfo window in windows)
        {
            if (window.Key.IsEmpty)
            {
                // With no executable path there is no way to group; it is the same criterion as the dock's.
                continue;
            }

            if (!grouped.TryGetValue(window.Key, out List<WindowInfo>? bucket))
            {
                bucket = [];
                grouped[window.Key] = bucket;
                order.Add(window.Key);
            }

            bucket.Add(window);
        }

        var items = new List<DockItem>(order.Count);

        foreach (AppKey key in order)
        {
            List<WindowInfo> appWindows = grouped[key];
            WindowInfo first = appWindows[0];

            // No Pinned: the switcher only lists what is open, and a pinned app that is not
            // running is not a switching destination.
            items.Add(new DockItem(key, first.DisplayName, first.ExecutablePath, null, appWindows));
        }

        return items;
    }

    /// <summary>
    /// The focused app's windows, in usage order, for the same-app switcher (Alt+').
    ///
    /// One window per item, not grouped: here what is switched are the windows themselves. The order
    /// is the same input Z order, so the focused window comes first — and that is why the
    /// first step lands on the second, the last used before this one, which is the most common gesture.
    ///
    /// The app is the focused window's; with none focused (everything minimized, focus on the desktop),
    /// the app of the first window stands, which is the one at the top of the stack.
    /// </summary>
    /// <param name="windows">Windows in Z order, from the most recent to the oldest.</param>
    /// <param name="foreground">Handle of the foreground window, or zero if there is none.</param>
    public IReadOnlyList<WindowInfo> BuildSameApp(IReadOnlyList<WindowInfo> windows, nint foreground)
    {
        ArgumentNullException.ThrowIfNull(windows);

        AppKey key = ResolveActiveApp(windows, foreground);

        if (key.IsEmpty)
        {
            return [];
        }

        var appWindows = new List<WindowInfo>();

        foreach (WindowInfo window in windows)
        {
            if (window.Key == key)
            {
                appWindows.Add(window);
            }
        }

        return appWindows;
    }

    /// <summary>App the same-app gesture belongs to: the focused window's, or the first one's.</summary>
    private static AppKey ResolveActiveApp(IReadOnlyList<WindowInfo> windows, nint foreground)
    {
        foreach (WindowInfo window in windows)
        {
            if (window.Handle == foreground && !window.Key.IsEmpty)
            {
                return window.Key;
            }
        }

        foreach (WindowInfo window in windows)
        {
            if (!window.Key.IsEmpty)
            {
                return window.Key;
            }
        }

        return default;
    }

    /// <summary>
    /// Next index of the wheel, wrapping at both ends.
    /// </summary>
    public int Step(int count, int current, bool backwards)
    {
        if (count <= 0)
        {
            return 0;
        }

        int next = backwards ? current - 1 : current + 1;

        return ((next % count) + count) % count;
    }

    /// <summary>
    /// Item already selected when the gesture begins.
    ///
    /// Forwards it is the second app, and not the first: the first is the one in use, and an
    /// Alt+Tab tapped and released has to lead to the previous one — it is the gesture of going back and
    /// forth between two apps, the most frequent of all. Backwards it is the last in the list, same wheel.
    /// </summary>
    public int SelectFirst(int count, bool backwards)
    {
        if (count <= 0)
        {
            return 0;
        }

        return Step(count, 0, backwards);
    }
}
