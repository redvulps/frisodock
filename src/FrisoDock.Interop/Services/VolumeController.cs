using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reads and changes the default output device's volume, through Core Audio. That is all (SRP).
///
/// The device is fetched on every operation, and not stored: swapping headphones, plugging a monitor
/// with sound or connecting a Bluetooth device changes the default outside the dock, and a stored
/// pointer would end up changing the volume of a device the user is no longer listening to.
/// </summary>
public sealed class VolumeController : IVolumeController
{
    private const int SuccessHResult = 0;

    public VolumeStatus GetStatus()
    {
        IAudioEndpointVolume? volume = OpenDefaultEndpoint();

        if (volume is null)
        {
            return VolumeStatus.Unavailable;
        }

        try
        {
            if (volume.GetMasterVolumeLevelScalar(out float level) != SuccessHResult
                || volume.GetMute(out bool isMuted) != SuccessHResult)
            {
                return VolumeStatus.Unavailable;
            }

            return new VolumeStatus(ToPercent(level), isMuted, IsAvailable: true);
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    public void SetLevel(int level)
    {
        IAudioEndpointVolume? volume = OpenDefaultEndpoint();

        if (volume is null)
        {
            return;
        }

        try
        {
            volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0, 100) / 100f, 0);
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    public void SetMuted(bool isMuted)
    {
        IAudioEndpointVolume? volume = OpenDefaultEndpoint();

        if (volume is null)
        {
            return;
        }

        try
        {
            volume.SetMute(isMuted, 0);
        }
        finally
        {
            Marshal.ReleaseComObject(volume);
        }
    }

    /// <summary>Rounded percentage, as the Windows control shows it.</summary>
    private static int ToPercent(float level)
    {
        return Math.Clamp((int)Math.Round(level * 100), 0, 100);
    }

    private static IAudioEndpointVolume? OpenDefaultEndpoint()
    {
        object? enumeratorObject = null;
        IMMDevice? device = null;

        try
        {
            enumeratorObject = new MMDeviceEnumerator();

            if (enumeratorObject is not IMMDeviceEnumerator enumerator)
            {
                return null;
            }

            // No output device — a machine with no sound card, or all of them disabled.
            if (enumerator.GetDefaultAudioEndpoint(
                    AudioComGuids.DataFlowRender,
                    AudioComGuids.RoleConsole,
                    out device) != SuccessHResult)
            {
                return null;
            }

            Guid interfaceId = AudioComGuids.AudioEndpointVolume;

            if (device.Activate(ref interfaceId, AudioComGuids.ClsCtxAll, 0, out object instance) != SuccessHResult)
            {
                return null;
            }

            return instance as IAudioEndpointVolume;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            if (device is not null)
            {
                Marshal.ReleaseComObject(device);
            }

            if (enumeratorObject is not null)
            {
                Marshal.ReleaseComObject(enumeratorObject);
            }
        }
    }
}
