using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>Accent color the user chose in Windows.</summary>
public interface IAccentColorProvider
{
    /// <summary>Color in force right now.</summary>
    AccentPalette Current { get; }

    /// <summary>
    /// Raised when the user changes the accent color. It may arrive off the UI thread: whoever
    /// touches brushes has to dispatch.
    /// </summary>
    event EventHandler? Changed;
}
