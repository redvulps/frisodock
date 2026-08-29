using System.Security;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Services;
using Microsoft.Win32;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Registers and unregisters the dock in the Windows startup list. That is all (SRP).
///
/// The entry goes to <c>HKCU\...\CurrentVersion\Run</c>, which needs no administrator and is the
/// same place Steam, Discord and the Google Drive client use. The Startup folder would be the
/// alternative and means writing a <c>.lnk</c> through <c>IShellLink</c>: more code and one more
/// file to keep in sync for the same result.
///
/// Reading is not the same as writing here. Turning the option on writes two values, because the
/// Windows "Startup apps" screen keeps its own state next to the entry (see
/// <see cref="StartupApproval" />); turning it off deletes both, so nothing is left pointing at a
/// dock that no longer starts.
/// </summary>
public sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovalKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>Name of the entry, in both keys. Windows pairs them by this name.</summary>
    private const string ValueName = "FrisoDock";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKeyPath);
                if (run?.GetValue(ValueName) is not string command || string.IsNullOrWhiteSpace(command))
                {
                    return false;
                }

                using RegistryKey? approval = Registry.CurrentUser.OpenSubKey(ApprovalKeyPath);

                return StartupApproval.IsApproved(approval?.GetValue(ValueName) as byte[]);
            }
            catch (Exception exception) when (IsRegistryFailure(exception))
            {
                return false;
            }
        }
    }

    public void Enable()
    {
        // The apphost, which is what Windows has to launch. Under dotnet run this is the build
        // output, and registering it is what a portable app does anyway.
        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        try
        {
            using RegistryKey run = Registry.CurrentUser.CreateSubKey(RunKeyPath);

            // Quoted: the path can carry spaces, and the shell splits the command line on them.
            run.SetValue(ValueName, $"\"{executable}\"", RegistryValueKind.String);

            // An entry the user once disabled in the Windows screen stays disabled until this
            // value is rewritten, no matter what the Run key says.
            using RegistryKey approval = Registry.CurrentUser.CreateSubKey(ApprovalKeyPath);
            approval.SetValue(ValueName, StartupApproval.Approved(), RegistryValueKind.Binary);
        }
        catch (Exception exception) when (IsRegistryFailure(exception))
        {
            // Nothing to recover: the caller reads the state back and the switch returns to off.
        }
    }

    public void Disable()
    {
        try
        {
            using RegistryKey? run = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            run?.DeleteValue(ValueName, throwOnMissingValue: false);

            using RegistryKey? approval = Registry.CurrentUser.OpenSubKey(ApprovalKeyPath, writable: true);
            approval?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception exception) when (IsRegistryFailure(exception))
        {
            // Same as above: the state is read back from the registry, never assumed.
        }
    }

    private static bool IsRegistryFailure(Exception exception) =>
        exception is SecurityException or UnauthorizedAccessException or IOException or ObjectDisposedException;
}
