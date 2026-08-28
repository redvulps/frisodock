namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: reporting when the shell brings a taskbar window back on screen.
/// It does not know what to do about it — deciding belongs to whoever listens.
/// </summary>
public interface ITaskbarRevealWatcher : IDisposable
{
    /// <summary>Raised when Explorer reshows the primary taskbar or a secondary one.</summary>
    event EventHandler? TaskbarRevealed;

    /// <summary>Starts watching. Idempotent.</summary>
    void Start();

    /// <summary>Stops watching. Idempotent.</summary>
    void Stop();
}
