namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: remembering, across runs, the taskbar appbar's state before the
/// dock touched it.
///
/// Why it has to outlive the process: to release the screen work area, the dock puts the
/// taskbar into autohide. If the instance dies without restoring (kill, power loss), the taskbar
/// stays in autohide. The next instance would read that autohide and store it as the "user's
/// original state", restoring to autohide forever after — the true state
/// would be lost on the first abnormal shutdown.
///
/// With the value on disk, a pending read means exactly this: "the previous run
/// did not restore, and the stored state is the true one".
/// </summary>
public interface ITaskbarStateStore
{
    /// <summary>State written by a run that never got to restore, or null.</summary>
    int? Load();

    /// <summary>Stores the original state before the first change.</summary>
    void Save(int state);

    /// <summary>Erases the record after a successful restore.</summary>
    void Clear();
}
