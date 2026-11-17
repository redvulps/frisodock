using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: hosting the notification area and keeping the list of live icons.
///
/// There is no API to *read* the tray: Windows hands the icons to whoever hosts it. So the
/// dock takes that role — it creates a <c>Shell_TrayWnd</c> class window and fires the
/// <c>TaskbarCreated</c> broadcast, which makes the apps re-register their icons with it. Each
/// registration arrives as <c>WM_COPYDATA</c> with the full NOTIFYICONDATA. No administrator required.
///
/// While the dock hosts, the icons stop appearing in Explorer's tray; <see cref="Stop"/>
/// gives the role back by firing the same broadcast again.
/// </summary>
public interface ITrayHost
{
    /// <summary>Raised when an icon is added, changed or removed.</summary>
    event EventHandler? IconsChanged;

    /// <summary>Icons currently registered, in the order they appeared.</summary>
    IReadOnlyList<TrayIcon> Icons { get; }

    /// <summary>True while the dock is hosting the tray.</summary>
    bool IsHosting { get; }

    /// <summary>
    /// Takes the host role and asks for the icons to be re-registered. It must be called from a
    /// thread with a message pump.
    /// </summary>
    void Start();

    /// <summary>Gives the role back to Explorer. Idempotent.</summary>
    void Stop();

    /// <summary>
    /// Periodic maintenance: reasserts ownership of the tray and drops icons whose app died.
    ///
    /// Both need a push from outside because Windows reports neither of
    /// them — Explorer raises its own window back in silence, and an app that closes without
    /// removing its icon would leave a ghost in the list forever.
    /// </summary>
    void Maintain();

    /// <summary>
    /// Forwards a mouse interaction to the app that owns the icon, in the format its
    /// protocol version expects.
    /// </summary>
    /// <param name="icon">Target icon.</param>
    /// <param name="mouseEvent">Interaction to forward.</param>
    /// <param name="screenPoint">Point on screen where the app menu should appear.</param>
    void ForwardMouseEvent(TrayIcon icon, TrayMouseEvent mouseEvent, PixelPoint screenPoint);

    /// <summary>
    /// True when the window in the foreground belongs to the app that owns the icon.
    ///
    /// The flyout asks this to tell apart the two reasons it loses the focus: the app coming
    /// forward to show the menu the user just asked for, which is no reason to go away, and the
    /// user moving on to something else, which is.
    /// </summary>
    bool IsAppInForeground(TrayIcon icon);
}

/// <summary>Interactions that can be forwarded to a tray icon.</summary>
public enum TrayMouseEvent
{
    LeftClick,

    /// <summary>
    /// Second click of a double. It exists because plenty of apps do nothing on a single click
    /// and only open their window on this one, and the flyout was never sending it.
    /// </summary>
    LeftDoubleClick,

    RightClick,
    MiddleClick,
}
