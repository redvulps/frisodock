using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: registering the dock window as a shell appbar, so that
/// maximized windows do not end up underneath it.
/// </summary>
public interface IAppBarService
{
    /// <summary>Reserved space is active.</summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Registers the window as an appbar and reserves space on the given edge.
    /// </summary>
    /// <param name="windowHandle">HWND of the dock window.</param>
    /// <param name="callbackMessage">Private message the shell uses to notify the appbar.</param>
    void Register(nint windowHandle, uint callbackMessage);

    /// <summary>Updates the reserved position/thickness. Returns the rectangle the shell approved.</summary>
    PixelRect SetPosition(DockEdge edge, PixelRect desired);

    /// <summary>Releases the reserved space. Idempotent.</summary>
    void Unregister();

    /// <summary>
    /// Translates a window message into an appbar notification. Returns false if the message
    /// is not from the shell to this appbar.
    /// </summary>
    bool TryHandleNotification(uint message, nint wParam, out AppBarNotification notification);
}

/// <summary>Notifications the shell sends to a registered appbar.</summary>
public enum AppBarNotification
{
    /// <summary>Another appbar changed state (autohide / always on top).</summary>
    StateChange,

    /// <summary>The appbar layout changed: the position has to be sent again.</summary>
    PositionChanged,

    /// <summary>An app entered or left full screen: the dock must get out of the way.</summary>
    FullScreenApp,

    /// <summary>The user asked to cascade or tile the windows.</summary>
    WindowArrange,
}
