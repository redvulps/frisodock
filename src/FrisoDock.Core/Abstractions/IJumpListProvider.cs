using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: delivering an app's jump list.
///
/// The source is the same file Explorer uses
/// (<c>%APPDATA%\Microsoft\Windows\Recent\CustomDestinations</c>), so the dock shows
/// exactly the same entries the native taskbar would show. Reading in HKCU/APPDATA:
/// no administrator required.
/// </summary>
public interface IJumpListProvider
{
    /// <summary>
    /// Jump list of the given executable. Returns <see cref="JumpList.Empty"/> when the app publishes
    /// none — which is the case for most programs.
    /// </summary>
    JumpList GetFor(string executablePath);

    /// <summary>Drops whatever is cached, forcing a reread on the next query.</summary>
    void Invalidate();
}
