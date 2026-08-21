using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: mirroring a window's live content inside another.
///
/// The Windows compositor itself does the drawing: we ask the DWM to replicate the source
/// window into a rectangle of the destination window. There is no screen capture or bitmap copy, and
/// the thumbnail follows the window in real time — including when it is minimized.
/// </summary>
public interface IWindowThumbnailService
{
    /// <summary>
    /// Registers a mirror of the source window inside the destination one. Returns null if the source
    /// cannot be mirrored (dead window, or one at a higher integrity level).
    /// </summary>
    IWindowThumbnail? Register(nint destinationWindow, nint sourceWindow);
}

/// <summary>An active mirror. Disposing removes the thumbnail from the destination window.</summary>
public interface IWindowThumbnail : IDisposable
{
    /// <summary>Real size of the source window, to compute the thumbnail's aspect ratio.</summary>
    PixelSize SourceSize { get; }

    /// <summary>Shows the thumbnail in the given rectangle, in destination window coordinates.</summary>
    void Show(PixelRect destination);
}
