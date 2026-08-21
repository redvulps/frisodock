namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: opening/closing the Windows Start menu.
/// The menu is hosted by StartMenuExperienceHost, a process independent of the taskbar,
/// so it keeps working even with the taskbar hidden — and it anchors to the bottom edge,
/// appearing above the dock.
/// </summary>
public interface IStartMenuInvoker
{
    /// <summary>Toggles the Start menu by simulating the Windows key.</summary>
    void Toggle();
}
