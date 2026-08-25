namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: keeping a window from becoming foreground when clicked, the way the
/// Windows taskbar does.
///
/// The dock is a bar you launch and switch from, not a place you switch to. Stealing the
/// focus when clicked has two bad effects: it closes the Start menu the dock itself just
/// opened (the menu disappears on focus loss) and it takes the text caret away from where the user
/// was typing.
/// </summary>
public interface IWindowActivationPolicy
{
    /// <summary>Marks the window so it takes no activation on click. Idempotent.</summary>
    void PreventActivation(nint windowHandle);
}
