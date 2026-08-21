using System.Buffers.Binary;
using System.Text;
using FrisoDock.Core.Models;
using FrisoDock.Core.Services;
using Xunit;

namespace FrisoDock.Tests;

/// <summary>
/// Reading DestList, the stream that says the order and what is pinned in an
/// automaticDestinations-ms. An undocumented format, so what matters most here is the
/// behaviour in the face of bytes that do not add up: return nothing, never invent.
/// </summary>
public sealed class DestListOrderTests
{
    [Fact]
    public void ReadsTheOrderAndThePinned()
    {
        byte[] content = BuildDestList(
            version: 1,
            (id: 0x1A, pinStatus: 0, path: @"C:\um.txt"),
            (id: 0x02, pinStatus: -1, path: @"C:\dois.txt"));

        DestListOrder order = DestListOrder.Parse(content);

        Assert.True(order.IsPinned("1a"));
        Assert.False(order.IsPinned("2"));
        Assert.Equal(["1a", "2"], order.Sort(["2", "1a"]));
    }

    [Fact]
    public void ModernVersion_HasExtraFields()
    {
        byte[] content = BuildDestList(
            version: 4,
            (id: 0x0B, pinStatus: -1, path: @"C:\arquivo.txt"),
            (id: 0x0C, pinStatus: 2, path: @"C:\outro.txt"));

        DestListOrder order = DestListOrder.Parse(content);

        Assert.False(order.IsPinned("b"));
        Assert.True(order.IsPinned("c"));
        Assert.Equal(["b", "c"], order.Sort(["c", "b"]));
    }

    [Fact]
    public void WhatIsNotInTheDestList_GoesToTheEnd_FromTheHighestIdToTheLowest()
    {
        // The ids grow with every new entry, so the highest is the most recent.
        byte[] content = BuildDestList(version: 1, (id: 0x05, pinStatus: -1, path: @"C:\um.txt"));

        DestListOrder order = DestListOrder.Parse(content);

        Assert.Equal(["5", "1f", "a"], order.Sort(["a", "5", "1f"]));
    }

    [Fact]
    public void TruncatedFile_DoesNotInventAnOrder()
    {
        Assert.Same(DestListOrder.Unknown, DestListOrder.Parse([1, 0, 0, 0]));
    }

    [Fact]
    public void EntryWithAnAbsurdSize_AbortsEverything()
    {
        // A 60 thousand character path means the alignment was lost. Carrying on from there
        // would produce an invented order, which is worse than having no order at all.
        byte[] content = BuildDestList(version: 1, (id: 0x01, pinStatus: -1, path: @"C:\um.txt"));
        BinaryPrimitives.WriteUInt16LittleEndian(content.AsSpan(32 + 0x68), 60000);

        Assert.Same(DestListOrder.Unknown, DestListOrder.Parse(content));
    }

    [Fact]
    public void WithNoEntries_OrdersNothing()
    {
        byte[] header = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 1);

        DestListOrder order = DestListOrder.Parse(header);

        Assert.Same(DestListOrder.Unknown, order);
        Assert.Equal(["b", "a"], order.Sort(["a", "b"]));
    }

    [Fact]
    public void UnknownOrder_PinsNothing()
    {
        Assert.False(DestListOrder.Unknown.IsPinned("1"));
    }

    private static byte[] BuildDestList(uint version, params (int id, int pinStatus, string path)[] entries)
    {
        bool modern = version >= 3;
        int pathLengthOffset = 0x64 + 4 + (modern ? 12 : 0);
        int pathOffset = pathLengthOffset + 2;

        var buffer = new List<byte>(new byte[32]);
        BinaryPrimitives.WriteUInt32LittleEndian(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer), version);
        BinaryPrimitives.WriteUInt32LittleEndian(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer)[4..], (uint)entries.Length);

        foreach ((int id, int pinStatus, string path) in entries)
        {
            byte[] entry = new byte[pathOffset + (path.Length * 2) + (modern ? 4 : 0)];

            BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(0x50), (uint)id);
            BinaryPrimitives.WriteInt32LittleEndian(entry.AsSpan(0x64), pinStatus);
            BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(pathLengthOffset), (ushort)path.Length);
            Encoding.Unicode.GetBytes(path).CopyTo(entry, pathOffset);

            buffer.AddRange(entry);
        }

        return [.. buffer];
    }
}

/// <summary>The label each jump list entry shows.</summary>
public sealed class JumpListEntryTests
{
    [Fact]
    public void TitleWrittenByTheApp_Wins()
    {
        var entry = new JumpListEntry("Nova janela", @"C:\app.exe", "--new", null, 0);

        Assert.Equal("Nova janela", entry.DisplayTitle);
        Assert.False(entry.IsEmpty);
    }

    [Fact]
    public void WithNoTitle_UsesTheFileName()
    {
        // It is the case of recent documents: the shortcut carries no System.Title, and Windows shows
        // the file name.
        var entry = new JumpListEntry(string.Empty, @"C:\Users\eu\Downloads\relatorio.pdf", string.Empty, null, 0);

        Assert.Equal("relatorio.pdf", entry.DisplayTitle);
    }

    [Fact]
    public void WithNoTitleAndNoTarget_HasNothingToShow()
    {
        var entry = new JumpListEntry(string.Empty, string.Empty, string.Empty, null, 0);

        Assert.True(entry.IsEmpty);
    }
}
