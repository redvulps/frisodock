using System.Diagnostics;
using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Starts processes from pinned apps. That is all (SRP).
///
/// UseShellExecute is mandatory here: it is what makes shortcuts (.lnk) and shell URIs
/// ("shell:AppsFolder\...", used by packaged apps) work as a launch target.
/// </summary>
public sealed class AppLauncher : IAppLauncher
{
    public bool Launch(PinnedApp app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return Start(app.LaunchTarget, app.Arguments);
    }

    public bool Launch(JumpListEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return Start(entry.TargetPath, entry.Arguments);
    }

    private static bool Start(string target, string? arguments)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
        };

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            startInfo.Arguments = arguments;
        }

        try
        {
            using Process? process = Process.Start(startInfo);
            return process is not null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Missing target, no permission, or cancelled at UAC: the dock stays alive.
            return false;
        }
    }
}
