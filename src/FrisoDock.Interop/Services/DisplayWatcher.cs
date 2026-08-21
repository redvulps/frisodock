using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Detects monitor changes through the WM_DISPLAYCHANGE message. That is all (SRP).
///
/// It follows the same path as <see cref="ShellRestartWatcher"/>: the message reaches the dock
/// window, which forwards it. This way the watcher needs no window of its own and no timer
/// asking about the monitors every so often.
/// </summary>
public sealed class DisplayWatcher : IDisplayWatcher
{
    public event EventHandler? DisplayChanged;

    public bool TryHandle(uint message)
    {
        if (message != NativeConstants.WM_DISPLAYCHANGE)
        {
            return false;
        }

        DisplayChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
