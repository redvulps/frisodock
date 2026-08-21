using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Detects Explorer restarts through the "TaskbarCreated" broadcast. That is all (SRP).
///
/// The message identifier is resolved once per process — RegisterWindowMessage always returns
/// the same value for the same string within the Windows session.
/// </summary>
public sealed class ShellRestartWatcher : IShellRestartWatcher
{
    private readonly uint _taskbarCreatedMessage;

    public ShellRestartWatcher()
    {
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage(NativeConstants.TaskbarCreatedMessage);
    }

    public event EventHandler? ShellRestarted;

    public bool TryHandle(uint message)
    {
        // Zero means the message registration failed; in that case nothing should match,
        // otherwise any WM_NULL would be mistaken for a shell restart.
        if (_taskbarCreatedMessage == 0 || message != _taskbarCreatedMessage)
        {
            return false;
        }

        ShellRestarted?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
