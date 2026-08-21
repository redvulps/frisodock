namespace FrisoDock.Core.Models;

/// <summary>When the dock leaves the screen on its own.</summary>
public enum DockHideMode
{
    /// <summary>Always visible. The dock occupies the edge all the time.</summary>
    Never = 0,

    /// <summary>
    /// Always hidden, appearing only when the cursor touches the screen edge (autohide).
    /// </summary>
    Always = 1,

    /// <summary>
    /// Hidden only when some window occupies the dock's space (intellihide). With the area
    /// free — on the desktop, or with small windows — the dock stays in view.
    /// </summary>
    WhenWindowOverlaps = 2,
}
