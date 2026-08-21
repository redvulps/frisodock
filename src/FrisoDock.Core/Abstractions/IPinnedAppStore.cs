using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: persisting and retrieving the pinned app list.
/// </summary>
public interface IPinnedAppStore
{
    /// <summary>Loads the pinned apps. It never throws: on error it returns the default list.</summary>
    IReadOnlyList<PinnedApp> Load();

    /// <summary>Writes the pinned apps in the given order.</summary>
    void Save(IReadOnlyList<PinnedApp> apps);
}
