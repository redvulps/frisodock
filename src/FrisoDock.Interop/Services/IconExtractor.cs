using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Obtains HICONs. It converts to no UI types (SRP) — the presentation layer does that.
///
/// A cascade of attempts, from best quality to worst:
///   1. PrivateExtractIcons at the requested size — it returns the exe's highest resolution variant.
///   2. SHGetFileInfo — it covers shortcuts (.lnk) and associated types, but only delivers 32 px.
///   3. WM_GETICON / class icon — a last resort, from the window.
/// </summary>
public sealed class IconExtractor : IIconExtractor
{
    private const uint SendMessageTimeoutMilliseconds = 200;

    private static readonly Action<nint> DestroyIconAction = handle => NativeMethods.DestroyIcon(handle);

    public IconHandle? FromFile(string path, int preferredSize, int iconIndex = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        IconHandle? extracted = ExtractPrivate(path, preferredSize, iconIndex);
        if (extracted is not null)
        {
            return extracted;
        }

        return ExtractShellIcon(path);
    }

    public IconHandle? FromWindow(nint windowHandle)
    {
        if (windowHandle == 0 || !NativeMethods.IsWindow(windowHandle))
        {
            return null;
        }

        nint icon = QueryWindowIcon(windowHandle, NativeConstants.ICON_BIG);
        if (icon == 0)
        {
            icon = QueryWindowIcon(windowHandle, NativeConstants.ICON_SMALL2);
        }

        if (icon == 0)
        {
            icon = QueryWindowIcon(windowHandle, NativeConstants.ICON_SMALL);
        }

        if (icon == 0)
        {
            icon = NativeMethods.GetClassLongPtr(windowHandle, NativeConstants.GCLP_HICON);
        }

        if (icon == 0)
        {
            icon = NativeMethods.GetClassLongPtr(windowHandle, NativeConstants.GCLP_HICONSM);
        }

        if (icon == 0)
        {
            return null;
        }

        // The HICON belongs to the window: we copy it so we can destroy ours without affecting the owner.
        nint copy = NativeMethods.CopyIcon(icon);
        if (copy == 0)
        {
            return null;
        }

        return new IconHandle(copy, DestroyIconAction);
    }

    private static IconHandle? ExtractPrivate(string path, int preferredSize, int iconIndex)
    {
        nint[] icons = new nint[1];
        int[] iconIds = new int[1];

        int extractedCount = NativeMethods.PrivateExtractIcons(
            path,
            iconIndex,
            preferredSize,
            preferredSize,
            icons,
            iconIds,
            1,
            0);

        if (extractedCount <= 0 || icons[0] == 0)
        {
            return null;
        }

        return new IconHandle(icons[0], DestroyIconAction);
    }

    private static IconHandle? ExtractShellIcon(string path)
    {
        var fileInfo = default(SHFILEINFO);
        uint structSize = (uint)Marshal.SizeOf<SHFILEINFO>();

        // USEFILEATTRIBUTES avoids I/O when the path no longer exists (uninstalled app):
        // it returns the generic icon of the type instead of stalling on a disk query.
        uint flags = NativeConstants.SHGFI_ICON
            | NativeConstants.SHGFI_LARGEICON
            | NativeConstants.SHGFI_USEFILEATTRIBUTES;

        nint result = NativeMethods.SHGetFileInfo(
            path,
            NativeConstants.FILE_ATTRIBUTE_NORMAL,
            ref fileInfo,
            structSize,
            flags);

        if (result == 0 || fileInfo.hIcon == 0)
        {
            return null;
        }

        return new IconHandle(fileInfo.hIcon, DestroyIconAction);
    }

    /// <summary>
    /// SendMessageTimeout instead of SendMessage: a hung app must not freeze the dock.
    /// </summary>
    private static nint QueryWindowIcon(nint windowHandle, nint iconType)
    {
        nint sendResult = NativeMethods.SendMessageTimeout(
            windowHandle,
            NativeConstants.WM_GETICON,
            iconType,
            0,
            NativeConstants.SMTO_ABORTIFHUNG,
            SendMessageTimeoutMilliseconds,
            out nint icon);

        if (sendResult == 0)
        {
            return 0;
        }

        return icon;
    }
}
