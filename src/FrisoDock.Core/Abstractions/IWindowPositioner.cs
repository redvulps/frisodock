using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: positioning a window in physical pixels.
///
/// WPF works in device independent units, which would force converting DPI
/// back and forth — and the conversion goes wrong with monitors at different scales. Positioning
/// directly in physical pixels removes that problem.
/// </summary>
public interface IWindowPositioner
{
    /// <summary>Moves and resizes the window without activating it or stealing the focus.</summary>
    void SetBounds(nint windowHandle, PixelRect bounds, bool topMost);

    /// <summary>
    /// Reasserts the topmost state. Necessary because other apps entering full screen
    /// can push the dock behind.
    /// </summary>
    void BringToTop(nint windowHandle);
}
