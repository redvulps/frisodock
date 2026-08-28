namespace FrisoDock.Core.Models;

/// <summary>
/// Windows 11 material the DWM draws behind a window. The two sample different
/// things, and that is what decides which one suits each window.
/// </summary>
public enum WindowBackdropMaterial
{
    /// <summary>
    /// Transient window acrylic: it samples and blurs whatever is behind the window. It is the
    /// material of the Windows menus and flyouts — the quick actions panel, the jump lists.
    /// </summary>
    Flyout,

    /// <summary>
    /// Mica: it samples the wallpaper, and not what is behind. It is the material of the Windows 11
    /// app windows, Settings included — the dark background that pulls towards the desktop
    /// color instead of being a neutral gray.
    /// </summary>
    MainWindow,
}
