using System.Management;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reads and changes the screen brightness, through WMI. That is all (SRP).
///
/// It is the path Windows exposes for a laptop's internal panel, and it does not ask for
/// administrator. External monitors are left out: they answer through DDC/CI, a different
/// protocol, and not all of them implement it — hence the tile disappearing when there is no panel
/// that accepts the command, instead of showing a control that does nothing.
/// </summary>
public sealed class BrightnessController : IBrightnessController
{
    private const string Scope = @"root\wmi";
    private const string BrightnessQuery = "SELECT CurrentBrightness FROM WmiMonitorBrightness";
    private const string MethodsQuery = "SELECT * FROM WmiMonitorBrightnessMethods";

    /// <summary>Without a timeout, a stuck WMI call would take the interface down with it.</summary>
    private static readonly TimeSpan MethodTimeout = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<BrightnessStatus> GetStatusAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(ReadStatus).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetLevelAsync(int level)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => WriteLevel(level)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static BrightnessStatus ReadStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, BrightnessQuery);
            searcher.Options.Timeout = MethodTimeout;
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject result in results)
            {
                using (result)
                {
                    return new BrightnessStatus(Convert.ToInt32(result["CurrentBrightness"]), IsAvailable: true);
                }
            }
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException)
        {
            return BrightnessStatus.Unavailable;
        }

        return BrightnessStatus.Unavailable;
    }

    private static void WriteLevel(int level)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(Scope, MethodsQuery);
            searcher.Options.Timeout = MethodTimeout;
            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementBaseObject result in results)
            {
                using (result)
                {
                    if (result is ManagementObject monitor)
                    {
                        using ManagementBaseObject parameters = monitor.GetMethodParameters("WmiSetBrightness");
                        parameters["Timeout"] = (uint)MethodTimeout.TotalSeconds;
                        parameters["Brightness"] = (byte)Math.Clamp(level, 0, 100);
                        using ManagementBaseObject response = monitor.InvokeMethod(
                            "WmiSetBrightness", parameters, new InvokeMethodOptions { Timeout = MethodTimeout });
                    }
                }
            }
        }
        catch (Exception exception) when (exception is ManagementException or UnauthorizedAccessException)
        {
            // A panel that does not accept the command: the brightness stays as it is.
        }
    }
}
