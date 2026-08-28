using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Applies the Windows 11 visual finish through the DWM. That is all (SRP).
///
/// Each attribute is applied independently and the failure of one does not block the others:
/// DWMWA_SYSTEMBACKDROP_TYPE and DWMWA_WINDOW_CORNER_PREFERENCE only exist from
/// Windows 11 22H2 on, and on Windows 10 the calls merely return an error, which is ignored.
/// </summary>
public sealed class DwmWindowBackdrop : IWindowBackdrop
{
    public void Apply(nint windowHandle, WindowBackdropMaterial material, bool darkMode)
    {
        if (windowHandle == 0)
        {
            return;
        }

        SetAttribute(windowHandle, NativeConstants.DWMWA_USE_IMMERSIVE_DARK_MODE, darkMode ? 1 : 0);
        SetAttribute(windowHandle, NativeConstants.DWMWA_WINDOW_CORNER_PREFERENCE, NativeConstants.DWMWCP_ROUND);

        ExtendFrameOverWholeWindow(windowHandle);

        SetAttribute(windowHandle, NativeConstants.DWMWA_SYSTEMBACKDROP_TYPE, BackdropTypeFor(material));
    }

    private static int BackdropTypeFor(WindowBackdropMaterial material)
    {
        return material switch
        {
            WindowBackdropMaterial.MainWindow => NativeConstants.DWMSBT_MAINWINDOW,
            _ => NativeConstants.DWMSBT_TRANSIENTWINDOW,
        };
    }

    /// <summary>
    /// "Sheet of glass": the DWM frame advances over the whole window.
    ///
    /// Without this the material does not show, and the symptom is a pure black background. The DWM
    /// draws the material on the window <i>frame</i>, and the dock windows are all borderless
    /// (<c>WindowStyle=None</c>), that is, they have no frame at all for it to paint.
    ///
    /// Measured side by side on this machine, with the same XAML and the same material request:
    /// without extending the frame, mica and acrylic give <c>#000000</c>; extending it, mica gives
    /// <c>#202020</c> and acrylic <c>#545454</c>. Swapping WindowStyle=None for a window with a
    /// border does not help — the client area still has no frame, and the black remains.
    /// </summary>
    private static void ExtendFrameOverWholeWindow(nint windowHandle)
    {
        var margins = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        NativeMethods.DwmExtendFrameIntoClientArea(windowHandle, ref margins);
    }

    private static void SetAttribute(nint windowHandle, int attribute, int value)
    {
        int buffer = value;
        NativeMethods.DwmSetWindowAttribute(windowHandle, attribute, ref buffer, sizeof(int));
    }
}
