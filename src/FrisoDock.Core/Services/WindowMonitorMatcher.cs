using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>
/// Single responsibility: saying which monitor each window is on. A pure function, no Win32.
///
/// The criterion is the same as Windows' <c>MonitorFromWindow</c>: the monitor containing the
/// larger part of the window wins. Testing only the top-left corner would put on the wrong monitor every
/// window that starts on one screen and ends on another.
/// </summary>
public sealed class WindowMonitorMatcher
{
    /// <summary>Index of the monitor the window belongs to, or -1 if it is on none.</summary>
    public int FindMonitorIndex(PixelRect window, IReadOnlyList<PixelRect> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        int best = -1;
        long bestArea = 0;

        for (int index = 0; index < monitors.Count; index++)
        {
            long area = window.IntersectionArea(monitors[index]);

            if (area > bestArea)
            {
                best = index;
                bestArea = area;
            }
        }

        return best;
    }

    /// <summary>
    /// Windows that belong to a monitor.
    ///
    /// A minimized window goes into all of them. Win32 does not say where it will come back — its
    /// rectangle sits off screen — and dropping the app from every dock would be worse than showing it
    /// once too often: the user would lose the way back to a window they minimized themselves.
    /// </summary>
    public IReadOnlyList<WindowInfo> ForMonitor(
        IReadOnlyList<WindowInfo> windows,
        int monitorIndex,
        IReadOnlyList<PixelRect> monitors)
    {
        ArgumentNullException.ThrowIfNull(windows);

        var result = new List<WindowInfo>(windows.Count);

        foreach (WindowInfo window in windows)
        {
            if (window.IsMinimized || FindMonitorIndex(window.Bounds, monitors) == monitorIndex)
            {
                result.Add(window);
            }
        }

        return result;
    }
}
