namespace FrisoDock.Core.Models;

/// <summary>
/// Immutable snapshot of a top-level window relevant to the dock.
/// </summary>
/// <param name="Handle">HWND of the window.</param>
/// <param name="Title">Current title.</param>
/// <param name="ProcessId">PID of the owning process.</param>
/// <param name="ExecutablePath">Full path of the executable, when obtained.</param>
/// <param name="IsMinimized">Whether the window is minimized.</param>
/// <param name="IsForeground">Whether the window is the foreground window.</param>
/// <param name="Bounds">
/// The window's rectangle in physical pixels. Only intellihide reads it; when nobody
/// filled it in, the empty rectangle overlaps nothing and the dock stays in view.
/// </param>
/// <param name="FriendlyName">
/// Product name declared by the executable ("Brave Browser"). It is what Windows shows;
/// the file name ("brave") only steps in when the executable declares nothing.
/// </param>
public sealed record WindowInfo(
    nint Handle,
    string Title,
    int ProcessId,
    string? ExecutablePath,
    bool IsMinimized,
    bool IsForeground,
    PixelRect Bounds = default,
    string? FriendlyName = null)
{
    /// <summary>Key of the app this window belongs to.</summary>
    public AppKey Key { get; } = AppKey.FromExecutable(ExecutablePath);

    /// <summary>App label, used when there is no pinned item with a name of its own.</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FriendlyName))
            {
                return FriendlyName;
            }

            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                return Title;
            }

            return Path.GetFileNameWithoutExtension(ExecutablePath);
        }
    }
}
