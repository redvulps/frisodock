using System.Buffers.Binary;

namespace FrisoDock.Core.Services;

/// <summary>
/// What the <c>DestList</c> stream of an <c>.automaticDestinations-ms</c> says about the entries:
/// in what order they appear and which ones the user pinned. A pure function, no COM, therefore testable.
///
/// The format is undocumented and the entries are variable-sized, so this reader is
/// deliberately suspicious: any unexpected field aborts the whole read and the
/// result becomes <see cref="Unknown"/>. The caller still shows the entries — only without
/// separating pinned from recent. Getting the alignment wrong here must not invent data.
/// </summary>
public sealed class DestListOrder
{
    /// <summary>Fixed header before the first entry.</summary>
    private const int HeaderLength = 32;

    /// <summary>Offset of the pin state within an entry, the same in every version.</summary>
    private const int PinStatusOffset = 0x64;

    /// <summary>From version 3 on, three new fields come before the path.</summary>
    private const int ModernExtraLength = 12;

    /// <summary>From version 3 on, each entry also ends with four bytes.</summary>
    private const int ModernTrailerLength = 4;

    private const int MaxEntries = 512;
    private const int MaxPathCharacters = 512;

    /// <summary>The "not pinned" value. Pinned ones carry their position in the pinned list.</summary>
    private const int NotPinned = -1;

    private readonly Dictionary<string, int> _positions;
    private readonly HashSet<string> _pinned;

    private DestListOrder(Dictionary<string, int> positions, HashSet<string> pinned)
    {
        _positions = positions;
        _pinned = pinned;
    }

    /// <summary>Nothing was read: the entries all land in recent, in id order.</summary>
    public static DestListOrder Unknown { get; } = new([], []);

    public bool IsPinned(string streamName)
    {
        return _pinned.Contains(streamName);
    }

    /// <summary>
    /// Orders the streams as DestList dictates. Whatever does not appear there comes after, from the
    /// highest id to the lowest: the ids grow with every new entry, so the highest is the most recent.
    /// </summary>
    public IEnumerable<string> Sort(IReadOnlyList<string> streamNames)
    {
        ArgumentNullException.ThrowIfNull(streamNames);

        return streamNames
            .OrderBy(name => _positions.TryGetValue(name, out int position) ? position : int.MaxValue)
            .ThenByDescending(ParseStreamId);
    }

    public static DestListOrder Parse(byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length < HeaderLength)
        {
            return Unknown;
        }

        var span = content.AsSpan();
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(span);
        bool isModern = version >= 3;

        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int offset = HeaderLength;
        int position = 0;

        while (offset < content.Length && position < MaxEntries)
        {
            int consumed = ReadEntry(span[offset..], isModern, out string streamName, out bool isPinned);

            if (consumed <= 0)
            {
                // Out of sync: better to return nothing than an invented order.
                return Unknown;
            }

            positions.TryAdd(streamName, position);

            if (isPinned)
            {
                pinned.Add(streamName);
            }

            offset += consumed;
            position++;
        }

        if (positions.Count == 0)
        {
            return Unknown;
        }

        return new DestListOrder(positions, pinned);
    }

    /// <summary>
    /// Reads an entry and returns how many bytes it took, or zero if something does not add up.
    /// </summary>
    private static int ReadEntry(ReadOnlySpan<byte> entry, bool isModern, out string streamName, out bool isPinned)
    {
        streamName = string.Empty;
        isPinned = false;

        int pathLengthOffset = PinStatusOffset + 4 + (isModern ? ModernExtraLength : 0);
        int pathOffset = pathLengthOffset + 2;

        if (entry.Length < pathOffset)
        {
            return 0;
        }

        uint id = BinaryPrimitives.ReadUInt32LittleEndian(entry[0x50..]);
        int pinStatus = BinaryPrimitives.ReadInt32LittleEndian(entry[PinStatusOffset..]);
        int pathCharacters = BinaryPrimitives.ReadUInt16LittleEndian(entry[pathLengthOffset..]);

        if (pathCharacters > MaxPathCharacters)
        {
            return 0;
        }

        int consumed = pathOffset + (pathCharacters * 2) + (isModern ? ModernTrailerLength : 0);

        if (consumed > entry.Length)
        {
            return 0;
        }

        // The stream name is the id in hexadecimal, with no leading zeros — that is how the shell writes it.
        streamName = id.ToString("x");
        isPinned = pinStatus != NotPinned;

        return consumed;
    }

    private static long ParseStreamId(string streamName)
    {
        return long.TryParse(streamName, System.Globalization.NumberStyles.HexNumber, null, out long id) ? id : -1;
    }
}
