using System.Runtime.InteropServices;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

internal static class ShellAppResources
{
    public static string? GetWindowApplicationId(nint window)
    {
        int initialized = NativeMethods.CoInitializeEx(0, NativeConstants.COINIT_MULTITHREADED);
        if (initialized < 0 && initialized != NativeConstants.RPC_E_CHANGED_MODE)
        {
            return null;
        }

        IPropertyStore? store = null;
        try
        {
            Guid interfaceId = typeof(IPropertyStore).GUID;
            if (NativeMethods.SHGetPropertyStoreForWindow(window, ref interfaceId, out store) < 0)
            {
                return null;
            }

            using var value = new PropVariant();
            PROPERTYKEY key = PROPERTYKEY.AppUserModelId;
            return store.GetValue(ref key, value) >= 0 ? value.AsString() : null;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (store is not null)
            {
                Marshal.ReleaseComObject(store);
            }

            if (initialized >= 0)
            {
                NativeMethods.CoUninitialize();
            }
        }
    }

    public static string? GetDisplayName(string path) => ReadItem(path, item =>
    {
        nint text = 0;
        try
        {
            return item.GetDisplayName(0, out text) >= 0 ? Marshal.PtrToStringUni(text)?.Trim() : null;
        }
        finally
        {
            Marshal.FreeCoTaskMem(text);
        }
    });

    public static IconHandle? GetIcon(string path, int size) => ReadItem(path, item =>
    {
        nint bitmap = 0;
        nint mask = 0;
        try
        {
            var factory = (IShellItemImageFactory)item;
            if (factory.GetImage(new SIZE { cx = size, cy = size },
                NativeConstants.SIIGBF_ICONONLY | NativeConstants.SIIGBF_BIGGERSIZEOK, out bitmap) < 0 || bitmap == 0)
            {
                return null;
            }

            if (NativeMethods.GetBitmapObject(bitmap, Marshal.SizeOf<BITMAP>(), out BITMAP information) == 0
                || information.Width <= 0 || information.Height <= 0)
            {
                return null;
            }

            int maskBytes = checked(((information.Width + 15) / 16) * 2 * information.Height);
            mask = NativeMethods.CreateBitmap(information.Width, information.Height, 1, 1, new byte[maskBytes]);
            if (mask == 0)
            {
                return null;
            }

            var iconInfo = new ICONINFO { IsIcon = true, Color = bitmap, Mask = mask };
            nint icon = NativeMethods.CreateIconIndirect(ref iconInfo);
            return icon == 0 ? null : new IconHandle(icon, value => NativeMethods.DestroyIcon(value));
        }
        finally
        {
            // CreateIconIndirect copies both bitmaps; the temporary GDI handles remain ours.
            if (mask != 0)
            {
                NativeMethods.DeleteObject(mask);
            }

            if (bitmap != 0)
            {
                NativeMethods.DeleteObject(bitmap);
            }
        }
    });

    private static T? ReadItem<T>(string path, Func<IShellItem, T?> read) where T : class
    {
        int initialized = NativeMethods.CoInitializeEx(0, NativeConstants.COINIT_MULTITHREADED);
        if (initialized < 0 && initialized != NativeConstants.RPC_E_CHANGED_MODE)
        {
            return null;
        }

        IShellItem? item = null;
        try
        {
            Guid interfaceId = typeof(IShellItem).GUID;
            return NativeMethods.SHCreateItemFromParsingName(path, 0, ref interfaceId, out item) >= 0
                ? read(item) : null;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException or ArgumentException)
        {
            return null;
        }
        finally
        {
            if (item is not null)
            {
                Marshal.ReleaseComObject(item);
            }

            if (initialized >= 0)
            {
                NativeMethods.CoUninitialize();
            }
        }
    }
}
