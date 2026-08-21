using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reads the <c>.automaticDestinations-ms</c>, which holds the items the user pinned and an app's
/// recent history. That is all (SRP): reading each shortcut belongs to
/// <see cref="ShellLinkReader"/>.
///
/// The file is an OLE compound one, and not a continuous blob like <c>.customDestinations-ms</c>:
/// each shortcut is a stream of its own, named with the entry id in hexadecimal, and an extra
/// stream called <c>DestList</c> holds the order and which entries are pinned.
///
/// The entry list always comes from enumerating the streams, never from DestList. That is on purpose:
/// DestList is an undocumented, variable-sized format, and a read error there
/// would misalign everything. It only comes in as garnish — saying what is pinned and in what order —
/// and, if it is not readable, the dock still shows the entries.
/// </summary>
public sealed class AutomaticDestinationsParser
{
    /// <summary>An app with hundreds of entries is a corrupt file, not a jump list.</summary>
    private const int MaxEntries = 512;

    /// <summary>
    /// How many recent entries to show. The file keeps the whole history — Brave on this machine
    /// has 61 —, but the native taskbar shows only the first ones, and reading the rest would cost one
    /// shortcut at a time with nothing in return.
    /// </summary>
    private const int MaxRecentEntries = 10;

    /// <summary>
    /// Possible names of the stream that holds the order. The classic one is <c>DestList</c>; Windows 11
    /// writes <c>DestListPropertyStore</c> in the files seen here.
    /// </summary>
    private static readonly string[] DestListStreamNames = ["DestList", "DestListPropertyStore"];

    private readonly ShellLinkReader _linkReader;

    public AutomaticDestinationsParser()
        : this(new ShellLinkReader())
    {
    }

    public AutomaticDestinationsParser(ShellLinkReader linkReader)
    {
        _linkReader = linkReader;
    }

    public JumpList Parse(string filePath)
    {
        const int SuccessHResult = 0;

        if (NativeMethods.StgOpenStorage(
                filePath,
                0,
                NativeConstants.STGM_READ_SHARE_DENY_WRITE,
                0,
                0,
                out IStorage storage) != SuccessHResult)
        {
            return JumpList.Empty;
        }

        try
        {
            return Read(storage);
        }
        catch (Exception exception) when (exception is COMException or InvalidDataException)
        {
            // File in use, truncated, or in a format we do not know. An unreadable jump list
            // must not kill the dock: the menu appears without it.
            return JumpList.Empty;
        }
        finally
        {
            Marshal.ReleaseComObject(storage);
        }
    }

    private JumpList Read(IStorage storage)
    {
        IReadOnlyList<string> streamNames = ListStreams(storage);
        DestListOrder order = ReadDestList(storage);

        var pinned = new List<JumpListEntry>();
        var recent = new List<JumpListEntry>();

        foreach (string name in order.Sort(streamNames))
        {
            bool isPinned = order.IsPinned(name);

            if (!isPinned && recent.Count >= MaxRecentEntries)
            {
                // There are enough recent entries. Stopping here avoids loading shortcuts nobody will see.
                continue;
            }

            JumpListEntry? entry = TryReadEntry(storage, name);

            if (entry is null || entry.IsEmpty)
            {
                // A shortcut to an item that is not a file — a library, a special folder — and that carries
                // no label: there is nothing to show and nothing to open.
                continue;
            }

            if (isPinned)
            {
                pinned.Add(entry);
            }
            else
            {
                recent.Add(entry);
            }
        }

        var categories = new List<JumpListCategory>();

        if (pinned.Count > 0)
        {
            categories.Add(new JumpListCategory(JumpListCategoryKind.Custom, "Fixados", pinned));
        }

        if (recent.Count > 0)
        {
            categories.Add(new JumpListCategory(JumpListCategoryKind.Custom, "Recentes", recent));
        }

        return new JumpList(categories);
    }

    private JumpListEntry? TryReadEntry(IStorage storage, string name)
    {
        IStream? stream = null;

        try
        {
            storage.OpenStream(name, 0, NativeConstants.STGM_READ_SHARE_EXCLUSIVE, 0, out stream);
            return _linkReader.Read(stream);
        }
        catch (Exception exception) when (exception is COMException or InvalidDataException)
        {
            return null;
        }
        finally
        {
            if (stream is not null)
            {
                Marshal.ReleaseComObject(stream);
            }
        }
    }

    /// <summary>Names of the shortcut streams, without DestList.</summary>
    private static IReadOnlyList<string> ListStreams(IStorage storage)
    {
        storage.EnumElements(0, 0, 0, out IEnumSTATSTG enumerator);

        var names = new List<string>();

        try
        {
            var buffer = new STATSTG[1];

            while (names.Count < MaxEntries && enumerator.Next(1, buffer, out uint fetched) == 0 && fetched == 1)
            {
                STATSTG element = buffer[0];

                if (element.type == NativeConstants.STGTY_STREAM && !IsDestList(element.pwcsName))
                {
                    names.Add(element.pwcsName);
                }
            }
        }
        finally
        {
            Marshal.ReleaseComObject(enumerator);
        }

        return names;
    }

    private static bool IsDestList(string name)
    {
        return DestListStreamNames.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static DestListOrder ReadDestList(IStorage storage)
    {
        foreach (string name in DestListStreamNames)
        {
            IStream? stream = null;

            try
            {
                storage.OpenStream(name, 0, NativeConstants.STGM_READ_SHARE_EXCLUSIVE, 0, out stream);
                DestListOrder order = DestListOrder.Parse(ReadAll(stream));

                if (order != DestListOrder.Unknown)
                {
                    return order;
                }
            }
            catch (Exception exception) when (exception is COMException or InvalidDataException)
            {
                // This name does not exist in this file; try the next one.
            }
            finally
            {
                if (stream is not null)
                {
                    Marshal.ReleaseComObject(stream);
                }
            }
        }

        return DestListOrder.Unknown;
    }

    private static byte[] ReadAll(IStream stream)
    {
        var chunk = new byte[4096];
        using var buffer = new MemoryStream();
        nint readCount = Marshal.AllocHGlobal(sizeof(int));

        try
        {
            while (true)
            {
                stream.Read(chunk, chunk.Length, readCount);
                int read = Marshal.ReadInt32(readCount);

                if (read <= 0)
                {
                    break;
                }

                buffer.Write(chunk, 0, read);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(readCount);
        }

        return buffer.ToArray();
    }
}
