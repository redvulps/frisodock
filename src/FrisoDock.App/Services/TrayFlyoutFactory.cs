using System.Windows.Media;
using System.Windows.Media.Imaging;
using FrisoDock.App.ViewModels;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Builds the tray flyout content. That is all (SRP): receiving the icons belongs to
/// <see cref="ITrayHost"/>, and so does forwarding the clicks.
/// </summary>
public sealed class TrayFlyoutFactory
{
    private readonly ITrayHost _trayHost;

    public TrayFlyoutFactory(ITrayHost trayHost)
    {
        _trayHost = trayHost;
    }

    /// <summary>
    /// Creates the view model.
    /// </summary>
    /// <param name="resolveScreenPoint">
    /// Where the app should open its menu. It is resolved at click time, and not now, because
    /// the flyout closes before forwarding — the point has to be the anchor's in the dock, which
    /// stays put.
    /// </param>
    public TrayFlyoutViewModel Create(Func<PixelPoint> resolveScreenPoint)
    {
        ArgumentNullException.ThrowIfNull(resolveScreenPoint);

        var icons = new List<TrayIconViewModel>();

        foreach (TrayIcon icon in _trayHost.Icons)
        {
            if (icon.IsHidden)
            {
                continue;
            }

            icons.Add(new TrayIconViewModel(icon, ToImageSource(icon.IconHandle), _trayHost, resolveScreenPoint));
        }

        return new TrayFlyoutViewModel(icons);
    }

    /// <summary>
    /// The HICON belongs to the host and stays alive after this conversion, so the bitmap is merely
    /// frozen — there is no handle of ours to dispose here.
    /// </summary>
    private static ImageSource? ToImageSource(nint iconHandle)
    {
        if (iconHandle == 0)
        {
            return null;
        }

        try
        {
            BitmapSource bitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                iconHandle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();
            return bitmap;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Corrupt icon, or already destroyed by the app: the item shows up with no image.
            return null;
        }
    }
}
