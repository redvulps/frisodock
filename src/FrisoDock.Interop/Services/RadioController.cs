using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using Windows.Devices.Radios;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Turns Wi-Fi and Bluetooth on and off through the Windows radios API. That is all (SRP).
///
/// It is WinRT, not P/Invoke, and it works in an unpackaged app: measured on this machine,
/// <c>Radio.RequestAccessAsync</c> returns <c>Allowed</c> with no administrator and no package
/// manifest, and both radios show up with their current state. There is no Win32 equivalent.
///
/// Permission is requested once and the result is kept: asking again on every click
/// would risk a dialog box in the middle of the user's gesture.
/// </summary>
public sealed class RadioController : IRadioController
{
    private readonly Dictionary<RadioDeviceKind, Radio> _radios = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    private bool _accessChecked;
    private bool _accessAllowed;

    public async Task<RadioStatus> GetStatusAsync(RadioDeviceKind kind)
    {
        await _gate.WaitAsync();
        try
        {
            return ReadStatus(kind, await FindRadioAsync(kind));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RadioStatus> SetStateAsync(RadioDeviceKind kind, bool isOn)
    {
        await _gate.WaitAsync();
        try
        {
            Radio? radio = await FindRadioAsync(kind);
            if (radio is null)
            {
                return RadioStatus.Unavailable(kind);
            }

            try
            {
                await radio.SetStateAsync(isOn ? RadioState.On : RadioState.Off);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException or COMException)
            {
                // Policy or a busy driver may reject the command. Report the actual state.
            }

            return ReadStatus(kind, radio);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Radio of the requested kind, reread on every query because the state changes outside the dock —
    /// through the Windows panel itself, a function key, another app.
    /// </summary>
    private async Task<Radio?> FindRadioAsync(RadioDeviceKind kind)
    {
        if (!await EnsureAccessAsync())
        {
            return null;
        }

        try
        {
            foreach (Radio radio in await Radio.GetRadiosAsync())
            {
                if (Matches(radio.Kind, kind))
                {
                    _radios[kind] = radio;
                    return radio;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or COMException)
        {
            return _radios.GetValueOrDefault(kind);
        }

        return null;
    }

    private async Task<bool> EnsureAccessAsync()
    {
        if (_accessChecked)
        {
            return _accessAllowed;
        }

        _accessChecked = true;

        try
        {
            // This request starts on the UI thread, as required by the WinRT permission API.
            _accessAllowed = await Radio.RequestAccessAsync() == RadioAccessStatus.Allowed;
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or COMException)
        {
            _accessAllowed = false;
        }

        return _accessAllowed;
    }

    private static bool Matches(RadioKind radioKind, RadioDeviceKind kind)
    {
        return kind switch
        {
            RadioDeviceKind.WiFi => radioKind == RadioKind.WiFi,
            RadioDeviceKind.Bluetooth => radioKind == RadioKind.Bluetooth,
            _ => false,
        };
    }

    private static RadioStatus ReadStatus(RadioDeviceKind kind, Radio? radio) => radio is null
        ? RadioStatus.Unavailable(kind)
        : new RadioStatus(kind, radio.State == RadioState.On, IsAvailable: true);
}
