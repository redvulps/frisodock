using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Converts HICONs into WPF <see cref="ImageSource"/> and caches the result. That is all (SRP):
/// the extraction itself belongs to <see cref="IIconExtractor"/>.
///
/// The cache exists because the dock list is rebuilt on every window change; without it,
/// every burst of events would trigger dozens of icon extractions.
/// </summary>
public sealed class IconImageProvider
{
    private readonly IIconExtractor _extractor;
    private readonly DockSettings _settings;
    private readonly Dictionary<string, ImageSource?> _cache = [];

    public IconImageProvider(IIconExtractor extractor, DockSettings settings)
    {
        _extractor = extractor;
        _settings = settings;
    }

    /// <summary>
    /// Resolves a dock item's icon: first from the source file, and if that fails,
    /// from the open window.
    /// </summary>
    public ImageSource? GetIcon(DockItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        string cacheKey = item.IconSource ?? $"hwnd:{item.IconWindowHandle}";

        if (_cache.TryGetValue(cacheKey, out ImageSource? cached))
        {
            return cached;
        }

        ImageSource? image = Resolve(item);
        _cache[cacheKey] = image;

        return image;
    }

    /// <summary>Drops the cache. Used when the system theme changes and the icons have to be reread.</summary>
    public void Clear()
    {
        _cache.Clear();
    }

    private ImageSource? Resolve(DockItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.IconSource))
        {
            using IconHandle? fromFile = _extractor.FromFile(item.IconSource, _settings.IconExtractionSize);
            ImageSource? image = ToImageSource(fromFile);

            if (image is not null)
            {
                return image;
            }
        }

        using IconHandle? fromWindow = _extractor.FromWindow(item.IconWindowHandle);
        return ToImageSource(fromWindow);
    }

    /// <summary>
    /// The bitmap is frozen so it can be shared across threads and so WPF stops tracking
    /// changes to it — the source HICON is destroyed right afterwards.
    /// </summary>
    internal static ImageSource? ToImageSource(IconHandle? handle)
    {
        if (handle is null || !handle.IsValid)
        {
            return null;
        }

        try
        {
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
                handle.Value,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            bitmap.Freeze();
            return bitmap;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Corrupt icon or already invalid handle: the item goes without an image, the dock carries on.
            return null;
        }
    }
}
