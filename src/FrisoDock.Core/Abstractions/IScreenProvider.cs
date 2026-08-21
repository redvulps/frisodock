using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: reporting monitor geometry in physical pixels.
/// </summary>
public interface IScreenProvider
{
    /// <summary>Primary monitor.</summary>
    MonitorInfo GetPrimaryMonitor();

    /// <summary>Every connected monitor.</summary>
    IReadOnlyList<MonitorInfo> GetMonitors();
}
