namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: detecting when the user changes the regional format in Windows.
///
/// The clock and the calendar follow the user's culture — time format, first day of the week,
/// month names. Changing the region with the dock running used to require a restart; the signal
/// that makes it live is the WM_SETTINGCHANGE broadcast for the "intl" section.
///
/// Broadcasts only reach top-level windows with a message pump, and creating/watching that window
/// is the UI layer's job. That is why the watcher does not "listen" on its own: it knows the
/// protocol and the UI merely forwards the messages it receives — the same shape as
/// <see cref="IShellRestartWatcher"/>.
/// </summary>
public interface IRegionalFormatWatcher
{
    /// <summary>Raised when the regional format changed.</summary>
    event EventHandler? RegionalFormatChanged;

    /// <summary>
    /// Forwards a window message. Returns true if it was the regional format signal,
    /// in which case <see cref="RegionalFormatChanged"/> has already been raised.
    /// </summary>
    bool TryHandle(uint message, nint lParam);
}
