using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: asking the Windows window manager for the native visual
/// finish — system material, rounded corners and dark mode.
///
/// Reproducing that material by hand does not work: Windows 11 transparency samples what
/// is behind the window or the wallpaper, something no WPF brush can see. Asking the
/// DWM is what makes the dock menu identical to the Windows one over any wallpaper.
/// </summary>
public interface IWindowBackdrop
{
    /// <summary>
    /// Applies the finish to the window. Silent on Windows versions that do not support one
    /// of the attributes: the window simply ends up opaque and square.
    /// </summary>
    void Apply(nint windowHandle, WindowBackdropMaterial material, bool darkMode);
}
