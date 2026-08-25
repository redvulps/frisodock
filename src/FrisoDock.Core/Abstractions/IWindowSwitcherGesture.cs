namespace FrisoDock.Core.Abstractions;

/// <summary>
/// What the gesture switches between: applications (Alt+Tab) or windows of the focused app (Alt+').
/// </summary>
public enum WindowSwitchScope
{
    /// <summary>One item per app; the grouped Alt+Tab.</summary>
    ByApp,

    /// <summary>One window per item, from the focused app only; the macOS-style Alt+'.</summary>
    SameApp,
}

/// <summary>
/// Single responsibility: recognizing the window switching gestures — Alt held, Tab or '
/// tapped — and reporting when they advance, are confirmed or abandoned. It does not know what a
/// window is, draws nothing and activates nobody.
///
/// It exists because Alt+Tab cannot be taken any other way. <c>RegisterHotKey</c> with
/// <c>MOD_ALT + VK_TAB</c> fails with <c>ERROR_HOTKEY_ALREADY_REGISTERED</c> (1409) — measured: the
/// combination already belongs to the system. And raw input (<c>RegisterRawInputDevices</c>) does see
/// the keys, but cannot swallow them, so the native switcher would show up alongside.
/// </summary>
public interface IWindowSwitcherGesture
{
    /// <summary>The user tapped the gesture key with Alt held: advance the selection.</summary>
    event EventHandler<WindowSwitcherStepEventArgs>? Stepped;

    /// <summary>The user released Alt: activate whatever is selected.</summary>
    event EventHandler? Committed;

    /// <summary>The user pressed Esc: give up without switching window.</summary>
    event EventHandler? Cancelled;

    /// <summary>
    /// Turns each gesture on independently, installing the keyboard hook when any is
    /// on and removing it when none is. Idempotent.
    ///
    /// A single method, and not one <c>Start</c> per gesture, because there is a single hook: both
    /// gestures share the same thread and the same message pump, and what differs between them is only
    /// which key the hook swallows.
    /// </summary>
    void SetModes(bool groupedByApp, bool sameApp);

    /// <summary>Stops recognizing everything, giving the keys back to Windows. Idempotent.</summary>
    void Stop();
}

/// <summary>One step of the gesture.</summary>
/// <param name="Scope">Whether the step is between apps or between windows of the same app.</param>
/// <param name="Backwards">True when Shift is held too, and the wheel turns the other way.</param>
/// <param name="StartsSession">
/// True on the first tap of a held Alt. It is what separates opening the switcher from moving
/// within it — the consumer needs to know when to build the list, and when to only move the
/// selection over the list already on screen.
/// </param>
public sealed record WindowSwitcherStepEventArgs(WindowSwitchScope Scope, bool Backwards, bool StartsSession);
