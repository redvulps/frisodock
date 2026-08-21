namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: hiding and restoring the native Windows taskbar.
/// It knows nothing about the dock, icons or windows.
/// </summary>
public interface ITaskbarController
{
    /// <summary>True if the taskbar is currently hidden by us.</summary>
    bool IsHidden { get; }

    /// <summary>
    /// Hides the primary taskbar and the secondary ones, and puts Explorer's appbar into
    /// autohide to release the screen work area. Idempotent.
    /// </summary>
    void Hide();

    /// <summary>
    /// Restores the taskbar's visibility and original state. Idempotent and safe
    /// to call even if <see cref="Hide"/> never ran in this session.
    /// </summary>
    void Restore();
}
