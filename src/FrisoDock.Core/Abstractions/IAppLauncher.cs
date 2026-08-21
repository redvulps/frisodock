using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: starting new processes from a pinned app.
/// </summary>
public interface IAppLauncher
{
    /// <summary>Starts the app. Returns false if the target could not be launched.</summary>
    bool Launch(PinnedApp app);

    /// <summary>
    /// Runs a jump list entry, with the target and arguments the app recorded.
    /// </summary>
    bool Launch(JumpListEntry entry);
}
