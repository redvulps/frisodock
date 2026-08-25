using FrisoDock.Core.Models;

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
    /// Hides the primary taskbar and the secondary ones. Idempotent.
    ///
    /// The dock edge comes in because it decides what to do with the band the taskbar reserves:
    /// on its own edge the dock covers it and discounting it is enough (<see cref="GetReservedBand"/>);
    /// on any other edge nobody covers it, and it is released.
    /// </summary>
    /// <param name="dockEdge">Edge the dock is anchored to.</param>
    void Hide(DockEdge dockEdge);

    /// <summary>
    /// Restores the taskbar's visibility. Idempotent and safe to call even if
    /// <see cref="Hide"/> never ran in this session.
    /// </summary>
    void Restore();

    /// <summary>
    /// Band the native taskbar reserves on screen, or <c>null</c> when it reserves nothing —
    /// which is the case for whoever chose autohide in the Windows settings.
    ///
    /// Hiding the window does not give that band back, and there is no way to give it back from
    /// outside: on Windows 11 the taskbar answers neither <c>ABM_SETPOS</c> nor <c>ABM_REMOVE</c> from
    /// another process, and Explorer recomputes over <c>SPI_SETWORKAREA</c>. Only autohide releases it —
    /// which is what <see cref="Hide"/> does when the dock is not on the taskbar's edge.
    /// </summary>
    PixelRect? GetReservedBand();
}
