using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace FrisoDock.Interop.Native;

/// <summary>
/// Shell COM interfaces used to read jump lists. They live here with the rest of the interop
/// (DRY): no COM or P/Invoke declaration may exist outside <c>FrisoDock.Interop/Native</c>.
///
/// All of them are documented by Microsoft. No guessed vtables: the Windows ShellLink object
/// itself is what parses the embedded shortcuts binary.
/// </summary>
internal static class ShellComGuids
{
    internal const string ShellLinkClsid = "00021401-0000-0000-C000-000000000046";

    /// <summary>16-byte marker that precedes each shortcut inside the jump list file.</summary>
    internal static ReadOnlySpan<byte> ShellLinkEntryMarker =>
    [
        0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46,
    ];
}

[ComImport]
[Guid("000214F9-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, nint findData, uint flags);

    void GetIDList(out nint idList);

    void SetIDList(nint idList);

    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);

    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);

    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);

    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);

    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxArguments);

    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);

    void GetHotkey(out ushort hotkey);

    void SetHotkey(ushort hotkey);

    void GetShowCmd(out int showCommand);

    void SetShowCmd(int showCommand);

    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon, int maxPath, out int index);

    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon, int index);

    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);

    void Resolve(nint owner, uint flags);

    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

/// <summary>
/// Loads an object from an <see cref="IStream"/>. It is the centrepiece of the read:
/// <c>Load</c> consumes exactly one shortcut and leaves the stream positioned right after it,
/// which is how the embedded shortcuts are delimited in the jump list file.
/// </summary>
[ComImport]
[Guid("00000109-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPersistStream
{
    void GetClassID(out Guid classId);

    [PreserveSig]
    int IsDirty();

    void Load(IStream stream);

    void Save(IStream stream, [MarshalAs(UnmanagedType.Bool)] bool clearDirty);

    void GetSizeMax(out long size);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    void GetCount(out uint count);

    void GetAt(uint index, out PROPERTYKEY key);

    [PreserveSig]
    int GetValue(ref PROPERTYKEY key, [In, Out] PropVariant value);

    void SetValue(ref PROPERTYKEY key, PropVariant value);

    void Commit();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
    public Guid FormatId;
    public uint PropertyId;

    internal PROPERTYKEY(string formatId, uint propertyId)
    {
        FormatId = new Guid(formatId);
        PropertyId = propertyId;
    }

    /// <summary>System.Title — where the jump list entry's visible label is written.</summary>
    internal static PROPERTYKEY Title => new("F29F85E0-4FF9-1068-AB91-08002B27B3D9", 2);
}

/// <summary>
/// PROPVARIANT reduced to what we need: reading strings. Declared as a class so the
/// marshaller passes it by reference and so disposal frees the native memory.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal sealed class PropVariant : IDisposable
{
    private const ushort VT_LPWSTR = 31;
    private const ushort VT_BSTR = 8;

    public ushort VarType;
    private readonly ushort _reserved1;
    private readonly ushort _reserved2;
    private readonly ushort _reserved3;
    public nint Pointer;
    private readonly nint _padding;

    /// <summary>Value as text, or null if the property is not a string.</summary>
    internal string? AsString()
    {
        if (Pointer == 0)
        {
            return null;
        }

        return VarType switch
        {
            VT_LPWSTR => Marshal.PtrToStringUni(Pointer),
            VT_BSTR => Marshal.PtrToStringBSTR(Pointer),
            _ => null,
        };
    }

    public void Dispose()
    {
        NativeMethods.PropVariantClear(this);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Enumerator of a compound file's elements. .NET brings <c>STATSTG</c>, but not this
/// enumerator, so it is declared here.
/// </summary>
[ComImport]
[Guid("0000000D-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IEnumSTATSTG
{
    [PreserveSig]
    int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] STATSTG[] elements, out uint fetched);

    [PreserveSig]
    int Skip(uint count);

    void Reset();

    void Clone(out IEnumSTATSTG enumerator);
}

/// <summary>
/// Structured storage (OLE compound file). Only the used members are declared, but the
/// vtable order has to be respected up to the last of them — hence the intermediate methods
/// being here even though unused.
/// </summary>
[ComImport]
[Guid("0000000B-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IStorage
{
    void CreateStream(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        uint mode,
        uint reserved1,
        uint reserved2,
        out IStream stream);

    void OpenStream(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        nint reserved1,
        uint mode,
        uint reserved2,
        out IStream stream);

    void CreateStorage(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        uint mode,
        uint reserved1,
        uint reserved2,
        out IStorage storage);

    void OpenStorage(
        [MarshalAs(UnmanagedType.LPWStr)] string? name,
        IStorage? priority,
        uint mode,
        nint exclude,
        uint reserved,
        out IStorage storage);

    void CopyTo(uint excludeCount, nint excludeInterfaces, nint excludeNames, IStorage destination);

    void MoveElementTo(
        [MarshalAs(UnmanagedType.LPWStr)] string name,
        IStorage destination,
        [MarshalAs(UnmanagedType.LPWStr)] string newName,
        uint flags);

    void Commit(uint commitFlags);

    void Revert();

    void EnumElements(uint reserved1, nint reserved2, uint reserved3, out IEnumSTATSTG enumerator);
}

