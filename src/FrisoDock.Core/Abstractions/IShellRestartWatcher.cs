namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: detecting when Explorer restarts.
///
/// A new Explorer recreates the taskbar already visible and undoes what we hid, so we have to
/// react. The signal is the "TaskbarCreated" broadcast — the same message apps listen to in order
/// to re-register their tray icons, and which will be reused in Phase 2.
///
/// Broadcasts only reach top-level windows with a message pump, and creating/watching that window
/// is the UI layer's job. That is why the watcher does not "listen" on its own: it knows the
/// protocol and the UI merely forwards the messages it receives.
/// </summary>
public interface IShellRestartWatcher
{
    /// <summary>Raised when Explorer recreates the taskbar.</summary>
    event EventHandler? ShellRestarted;

    /// <summary>
    /// Forwards a window message. Returns true if it was the restart signal,
    /// in which case <see cref="ShellRestarted"/> has already been raised.
    /// </summary>
    bool TryHandle(uint message);
}
