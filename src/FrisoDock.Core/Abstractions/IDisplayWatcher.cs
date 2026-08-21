namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Reports when the monitors change: another resolution, a monitor plugged in or unplugged.
///
/// It exists because the dock on every monitor has to gain and lose windows at
/// runtime; without it, plugging a screen in would do nothing until the dock restarted.
/// </summary>
public interface IDisplayWatcher
{
    event EventHandler? DisplayChanged;

    /// <summary>
    /// Handles a message received by the dock window. Returns true if the message belonged to the
    /// watcher, so the caller knows it does not have to forward it any further.
    /// </summary>
    bool TryHandle(uint message);
}
