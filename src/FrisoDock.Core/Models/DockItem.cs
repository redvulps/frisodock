namespace FrisoDock.Core.Models;

/// <summary>
/// A resolved dock item: an app (pinned, running, or both) with its windows.
/// Produced by <see cref="Services.DockItemAggregator"/>.
/// </summary>
/// <param name="Key">App identity.</param>
/// <param name="DisplayName">Item label.</param>
/// <param name="IconSource">File to extract the icon from; null when there is only a window.</param>
/// <param name="Pinned">Pinned definition, when there is one.</param>
/// <param name="Windows">Open windows of this app, in discovery order.</param>
public sealed record DockItem(
    AppKey Key,
    string DisplayName,
    string? IconSource,
    PinnedApp? Pinned,
    IReadOnlyList<WindowInfo> Windows)
{
    public bool IsPinned => Pinned is not null;

    public bool IsRunning => Windows.Count > 0;

    public bool HasMultipleWindows => Windows.Count > 1;

    public bool IsActive => Windows.Any(window => window.IsForeground);

    /// <summary>Window used as icon source when no icon file is known.</summary>
    public nint IconWindowHandle => Windows.Count > 0 ? Windows[0].Handle : 0;

    /// <summary>
    /// Executable this item's jump list comes from.
    ///
    /// It lives in the model because two places need the same answer: whoever builds the flyout and
    /// whoever warms it in the background. An item pinned by shortcut keeps the executable in
    /// <see cref="PinnedApp.MatchExecutablePath"/> — <see cref="IconSource"/> would point at the
    /// .lnk itself, which has no jump list at all.
    /// </summary>
    public string? JumpListExecutable
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Pinned?.MatchExecutablePath))
            {
                return Pinned.MatchExecutablePath;
            }

            string? fromWindow = Windows.Count > 0 ? Windows[0].ExecutablePath : null;
            if (!string.IsNullOrWhiteSpace(fromWindow))
            {
                return fromWindow;
            }

            return string.IsNullOrWhiteSpace(IconSource) ? null : IconSource;
        }
    }
}
