namespace FrisoDock.Core.Models;

/// <summary>
/// App pinned by the user. Persisted to disk by <see cref="Abstractions.IPinnedAppStore"/>.
/// </summary>
/// <param name="DisplayName">Label shown in the tooltip.</param>
/// <param name="LaunchTarget">Executable, shortcut (.lnk) or shell URI (e.g. "shell:AppsFolder\...").</param>
/// <param name="Arguments">Optional command line arguments.</param>
/// <param name="IconPath">File to extract the icon from; if null, <paramref name="LaunchTarget"/> is used.</param>
/// <param name="MatchExecutablePath">
/// Executable used to match running windows. Needed when the launch
/// target is a shortcut or URI and therefore does not match the process exe.
/// </param>
public sealed record PinnedApp(
    string DisplayName,
    string LaunchTarget,
    string? Arguments = null,
    string? IconPath = null,
    string? MatchExecutablePath = null)
{
    /// <summary>Key used to match this pinned item with running windows.</summary>
    public AppKey Key => AppKey.FromExecutable(MatchExecutablePath ?? LaunchTarget);

    /// <summary>File the icon should be extracted from.</summary>
    public string IconSource => IconPath ?? MatchExecutablePath ?? LaunchTarget;
}
