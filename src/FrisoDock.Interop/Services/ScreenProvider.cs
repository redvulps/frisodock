using System.Runtime.InteropServices;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Reports monitor geometry in physical pixels. That is all (SRP).
/// </summary>
public sealed class ScreenProvider : IScreenProvider
{
    public MonitorInfo GetPrimaryMonitor()
    {
        var origin = default(POINT);
        nint monitorHandle = NativeMethods.MonitorFromPoint(origin, NativeConstants.MONITOR_DEFAULTTOPRIMARY);

        if (TryReadMonitor(monitorHandle, out MonitorInfo monitor))
        {
            return monitor;
        }

        // With no monitor information there is no way to position anything: a silent fallback
        // would put the dock off screen, which is worse than failing loudly.
        throw new InvalidOperationException("Não foi possível ler as informações do monitor primário.");
    }

    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();

        NativeMethods.EnumDisplayMonitors(
            0,
            0,
            (nint monitorHandle, nint deviceContext, ref RECT clip, nint lParam) =>
            {
                if (TryReadMonitor(monitorHandle, out MonitorInfo monitor))
                {
                    monitors.Add(monitor);
                }

                return true;
            },
            0);

        if (monitors.Count == 0)
        {
            monitors.Add(GetPrimaryMonitor());
        }

        return monitors;
    }

    private static bool TryReadMonitor(nint monitorHandle, out MonitorInfo monitor)
    {
        monitor = default;

        if (monitorHandle == 0)
        {
            return false;
        }

        var info = new MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<MONITORINFO>(),
        };

        if (!NativeMethods.GetMonitorInfo(monitorHandle, ref info))
        {
            return false;
        }

        monitor = new MonitorInfo(
            info.rcMonitor.ToPixelRect(),
            info.rcWork.ToPixelRect(),
            (info.dwFlags & NativeConstants.MONITORINFOF_PRIMARY) != 0,
            GetDpiScale(monitorHandle));

        return true;
    }

    /// <summary>
    /// Per-monitor DPI. It fails on systems without per-monitor DPI support, where 1.0 is the right answer.
    /// </summary>
    private static double GetDpiScale(nint monitorHandle)
    {
        const int SuccessHResult = 0;

        int result = NativeMethods.GetDpiForMonitor(
            monitorHandle,
            NativeConstants.MDT_EFFECTIVE_DPI,
            out uint dpiX,
            out uint _);

        if (result != SuccessHResult || dpiX == 0)
        {
            return 1.0;
        }

        return dpiX / (double)NativeConstants.USER_DEFAULT_SCREEN_DPI;
    }
}
