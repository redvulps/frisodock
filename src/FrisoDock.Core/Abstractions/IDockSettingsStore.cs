using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: persisting and retrieving the dock settings.
/// </summary>
public interface IDockSettingsStore
{
    /// <summary>
    /// Loads the settings. It never throws: a corrupt file returns the defaults, because
    /// opening without settings is better than not opening.
    /// </summary>
    DockSettings Load();

    /// <summary>Writes the settings.</summary>
    void Save(DockSettings settings);
}
