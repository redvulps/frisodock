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

    /// <summary>
    /// Whether the primary mouse button is physically down right now.
    ///
    /// It exists because the window input state is not enough: a press that dismisses a flyout
    /// through the declined activation never enters the message queue, so the thread-synchronized
    /// state keeps saying "released" while the finger is still on the button.
    /// </summary>
    bool IsPrimaryButtonPressed();
}
