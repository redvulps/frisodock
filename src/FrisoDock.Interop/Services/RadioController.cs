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

    private bool _accessChecked;
    private bool _accessAllowed;

    public RadioStatus GetStatus(RadioDeviceKind kind)
    {
        Radio? radio = FindRadio(kind);

        if (radio is null)
        {
            return RadioStatus.Unavailable(kind);
        }

        return new RadioStatus(kind, radio.State == RadioState.On, IsAvailable: true);
    }

    public async Task<RadioStatus> SetStateAsync(RadioDeviceKind kind, bool isOn)
    {
        Radio? radio = FindRadio(kind);

        if (radio is null)
        {
            return RadioStatus.Unavailable(kind);
        }

        try
        {
            await radio.SetStateAsync(isOn ? RadioState.On : RadioState.Off);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException)
        {
            // Windows may refuse the change — management policy, busy driver. The
            // panel goes back to showing the real state instead of pretending it obeyed.
        }

        return GetStatus(kind);
    }

    /// <summary>
    /// Radio of the requested kind, reread on every query because the state changes outside the dock —
    /// through the Windows panel itself, a function key, another app.
    /// </summary>
    private Radio? FindRadio(RadioDeviceKind kind)
    {
        if (!EnsureAccess())
        {
            return null;
        }

        try
        {
            foreach (Radio radio in Radio.GetRadiosAsync().AsTask().GetAwaiter().GetResult())
            {
                if (Matches(radio.Kind, kind))
                {
                    _radios[kind] = radio;
                    return radio;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return _radios.GetValueOrDefault(kind);
        }

        return null;
    }

    private bool EnsureAccess()
    {
        if (_accessChecked)
        {
            return _accessAllowed;
        }

        _accessChecked = true;

        try
        {
            _accessAllowed = Radio.RequestAccessAsync().AsTask().GetAwaiter().GetResult() == RadioAccessStatus.Allowed;
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException)
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
}
