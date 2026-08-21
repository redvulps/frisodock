using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// The monitor a dock belongs to, with the set of monitors it was created in.
///
/// It is immutable on purpose: changing resolution or plugging a screen rebuilds the docks
/// entirely, instead of trying to patch the geometry of windows that already exist.
/// </summary>
/// <param name="Info">Bounds, work area and DPI of this dock's monitor.</param>
/// <param name="Index">Position of this monitor in the list, used to separate windows by screen.</param>
/// <param name="AllBounds">Bounds of every monitor, in the same order as the index.</param>
public sealed record DockMonitor(MonitorInfo Info, int Index, IReadOnlyList<PixelRect> AllBounds)
{
    public static DockMonitor Single(MonitorInfo info)
    {
        return new DockMonitor(info, 0, [info.Bounds]);
    }
}

/// <summary>
/// Hands the <see cref="DockMonitor"/> to a dock's service graph.
///
/// It exists because the container creates each dock's services in its own scope, and a scope
/// takes no parameter: whoever opens the scope fills this in before asking for the window.
/// </summary>
public sealed class DockMonitorHolder
{
    private DockMonitor? _monitor;

    public DockMonitor Monitor
    {
        get => _monitor ?? throw new InvalidOperationException("O monitor do dock não foi definido neste escopo.");
        set => _monitor = value;
    }
}
