using FrisoDock.Core.Models;

namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Single responsibility: obtaining a HICON. It converts to no UI type —
/// that is the presentation layer's job.
/// The caller owns the returned <see cref="IconHandle"/> and must dispose it.
/// </summary>
public interface IIconExtractor
{
    /// <summary>
    /// Extracts the icon of a file (exe, .lnk, .ico) at the requested size.
    /// </summary>
    /// <param name="iconIndex">Index of the icon within the file; zero for the main one.</param>
    IconHandle? FromFile(string path, int preferredSize, int iconIndex = 0);

    /// <summary>Extracts the icon associated with a window, via WM_GETICON / the window class.</summary>
    IconHandle? FromWindow(nint windowHandle);
}
