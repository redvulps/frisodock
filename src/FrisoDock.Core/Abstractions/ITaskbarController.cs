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
    /// It hides the window and nothing else: the band it reserves on screen stays reserved, and it is the
    /// dock that discounts that band from its own — see <see cref="GetReservedBand"/>.
    /// </summary>
    void Hide();

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
    /// Explorer recomputes over <c>SPI_SETWORKAREA</c>. Only autohide releases it, and autohide
    /// is precisely what makes the bar reappear when the cursor reaches the edge.
    /// </summary>
    PixelRect? GetReservedBand();
}
