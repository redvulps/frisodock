using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: changing the display state of an existing window.
/// </summary>
public interface IWindowActivator
{
    /// <summary>Brings the window forward, restoring it if it is minimized.</summary>
    void Activate(WindowInfo window);

    /// <summary>Minimizes the window.</summary>
    void Minimize(WindowInfo window);

    /// <summary>
    /// The dock's click behaviour: activates the window, or minimizes it if it is already focused.
    /// </summary>
    void ToggleActivation(WindowInfo window);

    /// <summary>
    /// Asks the window to close, as the close button does. It is a request, not an imposition:
    /// the app may show a confirmation or refuse, and that is the correct behaviour.
    /// </summary>
    void Close(WindowInfo window);
}
