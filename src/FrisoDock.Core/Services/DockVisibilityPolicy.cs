using FrisoDock.Core.Models;

namespace FrisoDock.Core.Services;

/// <summary>Everything that decides whether the dock should be in view at this instant.</summary>
/// <param name="Mode">Configured hide mode.</param>
/// <param name="PointerOverDock">Whether the cursor is over the dock — including over the sliver left when it is hidden.</param>
/// <param name="HasOpenFlyout">Whether a panel anchored to the dock is open (jump list, tray, thumbnails).</param>
/// <param name="WindowOverlapsDock">Whether some window occupies the dock's space.</param>
public readonly record struct DockVisibilityInput(
    DockHideMode Mode,
    bool PointerOverDock,
    bool HasOpenFlyout,
    bool WindowOverlapsDock);

/// <summary>
/// Single responsibility: deciding whether the dock shows or hides. A pure function, no timers and no
/// Win32 — measuring time and watching the windows belongs to the controller in the presentation layer.
///
/// The order of the rules matters. Cursor and open panel beat the mode: a dock that disappears with the
/// mouse on it, or that takes the jump list along when it goes, is worse than a dock that never
/// hides.
/// </summary>
public sealed class DockVisibilityPolicy
{
    public bool ShouldReveal(DockVisibilityInput input)
    {
        if (input.Mode == DockHideMode.Never)
        {
            return true;
        }

        if (input.PointerOverDock || input.HasOpenFlyout)
        {
            return true;
        }

        if (input.Mode == DockHideMode.Always)
        {
            return false;
        }

        return !input.WindowOverlapsDock;
    }

    /// <summary>
    /// Whether some window occupies the dock's space.
    ///
    /// Minimized windows do not count: Win32 still returns a rectangle for them, off
    /// screen, and treating them as present would hide the dock because of windows nobody sees.
    /// </summary>
    public bool AnyWindowOverlaps(IReadOnlyList<WindowInfo> windows, PixelRect area)
    {
        ArgumentNullException.ThrowIfNull(windows);

        foreach (WindowInfo window in windows)
        {
            if (window.IsMinimized)
            {
                continue;
            }

            if (window.Bounds.IntersectsWith(area))
            {
                return true;
            }
        }

        return false;
    }
}
