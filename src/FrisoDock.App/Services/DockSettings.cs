using FrisoDock.Core.Models;
using FrisoDock.Core.Services;

namespace FrisoDock.App.Services;

/// <summary>
/// The dock settings. A pure data object: no behaviour, so that any
/// service can depend on it without dragging logic along.
/// </summary>
public sealed class DockSettings
{
    /// <summary>Screen edge the dock is anchored to.</summary>
    public DockEdge Edge { get; init; } = DockEdge.Bottom;

    /// <summary>Icon, spacing and margin metrics.</summary>
    public DockMetrics Metrics { get; init; } = DockMetrics.Default;

    /// <summary>Reserve screen space as an appbar, so maximized windows do not cover the dock.</summary>
    public bool ReserveScreenSpace { get; init; } = true;

    /// <summary>Hide the native taskbar at startup.</summary>
    public bool HideNativeTaskbar { get; init; } = true;

    /// <summary>Coalescing window for bursts of window events, in milliseconds.</summary>
    public int RefreshDebounceMilliseconds { get; init; } = 150;

    /// <summary>Resolution the icons are extracted at, before scaling in the UI.</summary>
    public int IconExtractionSize { get; init; } = 64;
}
