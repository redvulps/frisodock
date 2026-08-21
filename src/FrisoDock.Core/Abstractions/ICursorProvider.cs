using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Where the cursor is right now, in physical pixels.
///
/// It exists because WPF mouse events are not enough for a dock that hides: they only
/// account for what happens over the window, and the zone that brings the dock back includes the
/// band between the panel and the screen edge, where there is no window at all.
/// </summary>
public interface ICursorProvider
{
    PixelPoint GetPosition();
}
