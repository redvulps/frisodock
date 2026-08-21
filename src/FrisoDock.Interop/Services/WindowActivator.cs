using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Activates, minimizes and toggles windows. That is all (SRP).
///
/// Windows blocks SetForegroundWindow for processes that do not hold the focus. The classic
/// trick — and a legitimate one, used by any alternative shell — is to temporarily attach our
/// thread's input queue to that of the foreground window, which puts us in the same
/// input context and unlocks the focus change.
/// </summary>
public sealed class WindowActivator : IWindowActivator
{
    public void Activate(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        nint handle = window.Handle;
        if (!NativeMethods.IsWindow(handle))
        {
            return;
        }

        if (NativeMethods.IsIconic(handle))
        {
            NativeMethods.ShowWindow(handle, NativeConstants.SW_RESTORE);
        }

        ForceForeground(handle);
    }

    public void Minimize(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!NativeMethods.IsWindow(window.Handle))
        {
            return;
        }

        NativeMethods.ShowWindowAsync(window.Handle, NativeConstants.SW_MINIMIZE);
    }

    public void ToggleActivation(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        bool isCurrentlyForeground = NativeMethods.GetForegroundWindow() == window.Handle;

        if (isCurrentlyForeground && !NativeMethods.IsIconic(window.Handle))
        {
            Minimize(window);
            return;
        }

        Activate(window);
    }

    public void Close(WindowInfo window)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (!NativeMethods.IsWindow(window.Handle))
        {
            return;
        }

        // PostMessage and not SendMessage: an app that opens an exit confirmation would block the dock
        // while the dialog was open.
        NativeMethods.PostMessage(window.Handle, NativeConstants.WM_CLOSE, 0, 0);
    }

    private static void ForceForeground(nint handle)
    {
        if (NativeMethods.SetForegroundWindow(handle))
        {
            return;
        }

        nint foreground = NativeMethods.GetForegroundWindow();
        if (foreground == 0)
        {
            return;
        }

        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();

        if (foregroundThread == currentThread)
        {
            return;
        }

        if (!NativeMethods.AttachThreadInput(currentThread, foregroundThread, true))
        {
            return;
        }

        try
        {
            NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
        }
    }
}
