namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: keeping a window out of the Windows window switcher (Alt+Tab).
///
/// The dock is not an app you switch to — it is the bar you switch from. Showing up there is noise,
/// and switching to it leads nowhere.
/// </summary>
public interface IWindowSwitcherExclusion
{
    /// <summary>It must be called with the window not yet shown: the exclusion is read at show time.</summary>
    void Exclude(nint windowHandle);
}
