using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reads a shortcut from within a stream and turns it into the entry the dock shows. That is all (SRP).
///
/// What interprets the shortcut is the Windows ShellLink object itself, through
/// <see cref="IPersistStream.Load"/>: it consumes exactly one shortcut and leaves the stream right
/// after it, which is the only way to know where a shortcut ends — the size is written
/// nowhere. No shortcut format parser was written by hand.
///
/// Both jump list formats need this: the <c>.customDestinations-ms</c>, where the shortcuts
/// come in sequence in a single file, and the <c>.automaticDestinations-ms</c>, where each shortcut is
/// a separate stream inside a compound file.
/// </summary>
public sealed class ShellLinkReader
{
    private const int MaxPathLength = 1024;
    private const int MaxArgumentsLength = 4096;

    /// <summary>Loads a shortcut from the stream's current position.</summary>
    public JumpListEntry Read(IStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        Type shellLinkType = Type.GetTypeFromCLSID(new Guid(ShellComGuids.ShellLinkClsid))
            ?? throw new InvalidDataException("ShellLink indisponível neste sistema.");

        object shellLink = Activator.CreateInstance(shellLinkType)
            ?? throw new InvalidDataException("Não foi possível criar o objeto ShellLink.");

        try
        {
            ((IPersistStream)shellLink).Load(stream);
            return CreateEntry(shellLink);
        }
        finally
        {
            Marshal.ReleaseComObject(shellLink);
        }
    }

    private static JumpListEntry CreateEntry(object shellLink)
    {
        var link = (IShellLinkW)shellLink;

        var targetPath = new StringBuilder(MaxPathLength);
        link.GetPath(targetPath, targetPath.Capacity, 0, 0);

        var arguments = new StringBuilder(MaxArgumentsLength);
        link.GetArguments(arguments, arguments.Capacity);

        var iconPath = new StringBuilder(MaxPathLength);
        link.GetIconLocation(iconPath, iconPath.Capacity, out int iconIndex);

        string title = ReadTitle(shellLink);
        string resolvedIcon = iconPath.Length > 0 ? iconPath.ToString() : targetPath.ToString();

        return new JumpListEntry(
            title,
            targetPath.ToString(),
            arguments.ToString(),
            string.IsNullOrWhiteSpace(resolvedIcon) ? null : resolvedIcon,
            iconIndex);
    }

    /// <summary>
    /// The visible label lives in System.Title, not in the shortcut description — GetDescription
    /// would return the comment, which apps usually leave empty.
    /// </summary>
    private static string ReadTitle(object shellLink)
    {
        const int SuccessHResult = 0;

        if (shellLink is not IPropertyStore store)
        {
            return string.Empty;
        }

        using var value = new PropVariant();
        PROPERTYKEY key = PROPERTYKEY.Title;

        if (store.GetValue(ref key, value) != SuccessHResult)
        {
            return string.Empty;
        }

        return value.AsString() ?? string.Empty;
    }
}
