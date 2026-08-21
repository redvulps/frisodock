using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reads a <c>.customDestinations-ms</c> file. That is all (SRP): it does not locate the file,
/// does not cache and knows nothing about the dock.
///
/// Format, verified byte by byte against real files on this machine:
/// <code>
/// uint32 format_version      (= 2)
/// uint32 number_of_categories
/// uint32 unknown
///
/// per category:
///     uint32 type            0 = named by the app, 1 = Windows default category, 2 = tasks
///     if type == 0:
///         uint16 name_char_count
///         wchar  name[name_char_count]
///     uint32 entry_count     (for type == 1 this field is the default category id)
///     per entry:
///         byte   marker[16]  (01 14 02 ... 46)
///         variable-length binary shortcut
///     uint32 terminator      (= 0xBABFFBAB)
/// </code>
///
/// The size of each shortcut is written nowhere: it is only discovered by interpreting the
/// shortcut. That is why the reader is the Windows ShellLink object itself, through
/// <see cref="IPersistStream.Load"/>, which consumes exactly one shortcut and leaves the stream right
/// after it. This way we write no binary shortcut format parser at all.
/// </summary>
public sealed class CustomDestinationsParser
{
    private const uint ExpectedFormatVersion = 2;
    private const uint CategoryTerminator = 0xBABFFBAB;

    // An app with hundreds of categories or entries is a corrupt file, not a jump list.
    private const uint MaxCategories = 64;
    private const uint MaxEntriesPerCategory = 256;

    private const int MaxPathLength = 1024;
    private const int MaxArgumentsLength = 4096;

    public JumpList Parse(byte[] fileContent)
    {
        ArgumentNullException.ThrowIfNull(fileContent);

        nint streamPointer = NativeMethods.SHCreateMemStream(fileContent, (uint)fileContent.Length);
        if (streamPointer == 0)
        {
            return JumpList.Empty;
        }

        object streamObject = Marshal.GetObjectForIUnknown(streamPointer);
        try
        {
            return ParseStream((IStream)streamObject);
        }
        catch (Exception exception) when (exception is COMException or EndOfStreamException or InvalidDataException)
        {
            // A truncated file, from a future version, or written by an app that strayed from the format.
            // An unreadable jump list must not kill the dock: the menu appears without it.
            return JumpList.Empty;
        }
        finally
        {
            Marshal.ReleaseComObject(streamObject);
            Marshal.Release(streamPointer);
        }
    }

    private JumpList ParseStream(IStream stream)
    {
        uint formatVersion = ReadUInt32(stream);
        if (formatVersion != ExpectedFormatVersion)
        {
            throw new InvalidDataException($"Versão de jump list não suportada: {formatVersion}.");
        }

        uint categoryCount = ReadUInt32(stream);
        if (categoryCount > MaxCategories)
        {
            throw new InvalidDataException($"Contagem de categorias implausível: {categoryCount}.");
        }

        ReadUInt32(stream);

        var categories = new List<JumpListCategory>((int)categoryCount);
        for (uint index = 0; index < categoryCount; index++)
        {
            categories.Add(ReadCategory(stream));
        }

        return new JumpList(categories);
    }

    private JumpListCategory ReadCategory(IStream stream)
    {
        var kind = (JumpListCategoryKind)ReadUInt32(stream);
        string? name = null;

        if (kind == JumpListCategoryKind.Custom)
        {
            ushort nameLength = ReadUInt16(stream);
            name = Encoding.Unicode.GetString(ReadBytes(stream, nameLength * 2));
        }

        uint entryCount = ReadUInt32(stream);

        // Windows default categories carry no embedded entries: the field is the id of the
        // category (Frequent / Recent), whose content lives in the automaticDestinations file.
        if (kind == JumpListCategoryKind.Known)
        {
            ReadTerminator(stream);
            return new JumpListCategory(kind, name, []);
        }

        if (entryCount > MaxEntriesPerCategory)
        {
            throw new InvalidDataException($"Contagem de entradas implausível: {entryCount}.");
        }

        var entries = new List<JumpListEntry>((int)entryCount);
        for (uint index = 0; index < entryCount; index++)
        {
            JumpListEntry? entry = ReadEntry(stream);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        ReadTerminator(stream);
        return new JumpListCategory(kind, name, entries);
    }

    private static JumpListEntry? ReadEntry(IStream stream)
    {
        byte[] marker = ReadBytes(stream, ShellComGuids.ShellLinkEntryMarker.Length);
        if (!marker.AsSpan().SequenceEqual(ShellComGuids.ShellLinkEntryMarker))
        {
            throw new InvalidDataException("Marcador de entrada ausente: o arquivo saiu de sincronia.");
        }

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

    private static void ReadTerminator(IStream stream)
    {
        uint terminator = ReadUInt32(stream);
        if (terminator != CategoryTerminator)
        {
            throw new InvalidDataException($"Terminador de categoria inesperado: 0x{terminator:X8}.");
        }
    }

    private static byte[] ReadBytes(IStream stream, int count)
    {
        byte[] buffer = new byte[count];
        nint readCount = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            stream.Read(buffer, count, readCount);
            if (Marshal.ReadInt32(readCount) != count)
            {
                throw new EndOfStreamException("Arquivo de jump list terminou antes do esperado.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(readCount);
        }

        return buffer;
    }

    private static uint ReadUInt32(IStream stream) => BitConverter.ToUInt32(ReadBytes(stream, 4));

    private static ushort ReadUInt16(IStream stream) => BitConverter.ToUInt16(ReadBytes(stream, 2));
}
