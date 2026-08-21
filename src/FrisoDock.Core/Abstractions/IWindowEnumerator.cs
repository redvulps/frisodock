using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: discovering which top-level windows deserve to show up in the dock
/// and reporting when that set changes. It decides neither layout nor grouping.
/// </summary>
public interface IWindowEnumerator
{
    /// <summary>
    /// Raised when windows are created, destroyed, renamed or change focus.
    /// It can fire in bursts: the consumer should debounce.
    /// </summary>
    event EventHandler? WindowsChanged;

    /// <summary>Reads the current window state. A synchronous call, cheap enough for a ~100 ms debounce.</summary>
    IReadOnlyList<WindowInfo> GetWindows();

    /// <summary>Starts watching for changes. It must be called from a thread with a message pump.</summary>
    void Start();

    /// <summary>Stops watching. Idempotent.</summary>
    void Stop();
}
