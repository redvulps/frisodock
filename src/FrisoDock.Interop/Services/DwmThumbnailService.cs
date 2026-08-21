using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Mirrors windows using DWM thumbnails. That is all (SRP).
/// </summary>
public sealed class DwmThumbnailService : IWindowThumbnailService
{
    private const int SuccessHResult = 0;

    public IWindowThumbnail? Register(nint destinationWindow, nint sourceWindow)
    {
        if (destinationWindow == 0 || sourceWindow == 0 || !NativeMethods.IsWindow(sourceWindow))
        {
            return null;
        }

        int result = NativeMethods.DwmRegisterThumbnail(destinationWindow, sourceWindow, out nint handle);
        if (result != SuccessHResult || handle == 0)
        {
            // It happens with windows of protected processes and with windows that died between the
            // enumeration and this point. With no thumbnail, the caller shows only the title.
            return null;
        }

        return new DwmThumbnail(handle);
    }

    private sealed class DwmThumbnail : IWindowThumbnail
    {
        private nint _handle;

        internal DwmThumbnail(nint handle)
        {
            _handle = handle;
            SourceSize = QuerySourceSize(handle);
        }

        public PixelSize SourceSize { get; }

        public void Show(PixelRect destination)
        {
            if (_handle == 0)
            {
                return;
            }

            var properties = new DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = NativeConstants.DWM_TNP_RECTDESTINATION
                    | NativeConstants.DWM_TNP_VISIBLE
                    | NativeConstants.DWM_TNP_SOURCECLIENTAREAONLY,
                rcDestination = RECT.FromPixelRect(destination),
                fVisible = true,

                // Client area only: including the frame would bring the title bar and the window borders,
                // which in the thumbnail become a useless gray band.
                fSourceClientAreaOnly = true,
            };

            NativeMethods.DwmUpdateThumbnailProperties(_handle, ref properties);
        }

        public void Dispose()
        {
            nint handle = Interlocked.Exchange(ref _handle, 0);
            if (handle == 0)
            {
                return;
            }

            NativeMethods.DwmUnregisterThumbnail(handle);
        }

        private static PixelSize QuerySourceSize(nint handle)
        {
            if (NativeMethods.DwmQueryThumbnailSourceSize(handle, out SIZE size) != SuccessHResult)
            {
                return default;
            }

            return new PixelSize(size.cx, size.cy);
        }
    }
}
