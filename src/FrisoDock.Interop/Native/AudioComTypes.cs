using System.Runtime.InteropServices;

namespace FrisoDock.Interop.Native;

/// <summary>
/// Core Audio COM interfaces, used to read and change the output device volume.
///
/// Only the necessary members are declared, but the vtable order has to be respected up to
/// the last of them — hence the intermediate methods showing up here even though unused.
/// </summary>
internal static class AudioComGuids
{
    internal const string MMDeviceEnumeratorClsid = "BCDE0395-E52F-467C-8E3D-C4579291692E";

    /// <summary>IID of IAudioEndpointVolume, requested when activating the device.</summary>
    internal static readonly Guid AudioEndpointVolume = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    /// <summary>Audio output.</summary>
    internal const int DataFlowRender = 0;

    /// <summary>The "console" role: it is the device the system volume control touches.</summary>
    internal const int RoleConsole = 0;

    /// <summary>In-process activation context.</summary>
    internal const uint ClsCtxAll = 23;
}

[ComImport]
[Guid(AudioComGuids.MMDeviceEnumeratorClsid)]
internal class MMDeviceEnumerator
{
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, uint stateMask, out nint devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(
        ref Guid interfaceId,
        uint classContext,
        nint activationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object instance);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig]
    int RegisterControlChangeNotify(nint callback);

    [PreserveSig]
    int UnregisterControlChangeNotify(nint callback);

    [PreserveSig]
    int GetChannelCount(out uint count);

    [PreserveSig]
    int SetMasterVolumeLevel(float levelDb, nint eventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float level, nint eventContext);

    [PreserveSig]
    int GetMasterVolumeLevel(out float levelDb);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig]
    int SetChannelVolumeLevel(uint channel, float levelDb, nint eventContext);

    [PreserveSig]
    int SetChannelVolumeLevelScalar(uint channel, float level, nint eventContext);

    [PreserveSig]
    int GetChannelVolumeLevel(uint channel, out float levelDb);

    [PreserveSig]
    int GetChannelVolumeLevelScalar(uint channel, out float level);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool isMuted, nint eventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool isMuted);
}
