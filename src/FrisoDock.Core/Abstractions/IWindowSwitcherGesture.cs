namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: recognizing the window switching gesture — Alt held, Tab tapped —
/// and reporting when it advances, is confirmed or is abandoned. It does not know what a window is,
/// draws nothing and activates nobody.
///
/// It exists because Alt+Tab cannot be taken any other way. <c>RegisterHotKey</c> with
/// <c>MOD_ALT + VK_TAB</c> fails with <c>ERROR_HOTKEY_ALREADY_REGISTERED</c> (1409) — measured: the
/// combination already belongs to the system. And raw input (<c>RegisterRawInputDevices</c>) does see
/// the keys, but cannot swallow them, so the native switcher would show up alongside.
/// </summary>
public interface IWindowSwitcherGesture
{
    /// <summary>The user tapped Tab with Alt held: advance the selection.</summary>
    event EventHandler<WindowSwitcherStepEventArgs>? Stepped;

    /// <summary>The user released Alt: activate whatever is selected.</summary>
    event EventHandler? Committed;

    /// <summary>The user pressed Esc: give up without switching window.</summary>
    event EventHandler? Cancelled;

    /// <summary>Starts recognizing the gesture. Idempotent.</summary>
    void Start();

    /// <summary>Stops recognizing, giving Alt+Tab back to Windows. Idempotent.</summary>
    void Stop();
}

/// <summary>One step of the gesture.</summary>
/// <param name="Backwards">True when Shift is held too, and the wheel turns the other way.</param>
/// <param name="StartsSession">
/// True on the first Tab of a held Alt. It is what separates opening the switcher from moving
/// within it — the consumer needs to know when to build the list, and when to only move the
/// selection over the list already on screen.
/// </param>
public sealed record WindowSwitcherStepEventArgs(bool Backwards, bool StartsSession);
