using System.Runtime.InteropServices;

namespace FrisoDock.Interop.Native;

[ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItem
{
    [PreserveSig]
    int BindToHandler(nint context, ref Guid handler, ref Guid interfaceId, out nint result);
    [PreserveSig]
    int GetParent(out IShellItem parent);
    [PreserveSig]
    int GetDisplayName(uint kind, out nint name);
    [PreserveSig]
    int GetAttributes(uint mask, out uint attributes);
    [PreserveSig]
    int Compare(IShellItem other, uint hint, out int order);
}

[ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    [PreserveSig]
    int GetImage(SIZE size, uint flags, out nint bitmap);
}
