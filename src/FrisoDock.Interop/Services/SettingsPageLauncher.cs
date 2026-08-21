using System.Diagnostics;
using FrisoDock.Core.Abstractions;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Opens a page of the Settings app. That is all (SRP).
///
/// The <c>ms-settings:</c> address is opened by the shell, as the native panel does with its arrow
/// buttons. This is how the dock serves what has no API without administrator.
/// </summary>
public sealed class SettingsPageLauncher : ISettingsPageLauncher
{
    public void Open(string page)
    {
        if (string.IsNullOrWhiteSpace(page))
        {
            return;
        }

        try
        {
            // UseShellExecute is mandatory: what knows how to open an ms-settings: is the shell.
            Process.Start(new ProcessStartInfo(page) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // A page that does not exist in this Windows version: not opening beats killing the dock.
        }
    }
}
