using FrisoDock.Core.Services;

namespace FrisoDock.Core.Models;

/// <summary>
/// The dock settings. An immutable data object: no behaviour, so that any
/// service can depend on it without dragging logic along.
///
/// It lives in Core, and not in the presentation layer, because it is what goes to disk — the
/// persisted format is part of the domain, not of the UI.
/// </summary>
public sealed record DockSettings
{
    /// <summary>Screen edge the dock is anchored to.</summary>
    public DockEdge Edge { get; init; } = DockEdge.Bottom;

    /// <summary>Icon, spacing and margin metrics.</summary>
    public DockMetrics Metrics { get; init; } = DockMetrics.Default;

    /// <summary>Reserve screen space as an appbar, so maximized windows do not cover the dock.</summary>
    public bool ReserveScreenSpace { get; init; } = true;

    /// <summary>Show a dock on every monitor, instead of only on the primary one.</summary>
    public bool ShowOnAllMonitors { get; init; }

    /// <summary>
    /// In each dock, show only the apps with a window on that monitor. Pinned apps stay in
    /// all of them: without those, the dock of an empty monitor would not even serve to open an app.
    /// </summary>
    public bool IsolateMonitorApps { get; init; }

    /// <summary>When the dock leaves the screen on its own.</summary>
    public DockHideMode HideMode { get; init; } = DockHideMode.Never;

    /// <summary>Hide the native taskbar.</summary>
    public bool HideNativeTaskbar { get; init; } = true;

    /// <summary>Magnify the icon under the cursor, dash-to-dock style.</summary>
    public bool EnableMagnification { get; init; } = true;

    /// <summary>
    /// How much the icon under the cursor grows. 1.0 turns the effect off in practice; the headroom
    /// the window reserves above the panel is sized by this value.
    /// </summary>
    public double MagnificationScale { get; init; } = 1.5;

    /// <summary>Show the seconds in the dock clock.</summary>
    public bool ShowClockSeconds { get; init; }

    /// <summary>Show window thumbnails when the mouse rests on a running app.</summary>
    public bool EnableWindowPreviews { get; init; } = true;

    /// <summary>
    /// Replace the Windows Alt+Tab with a switcher that lists applications, not loose windows.
    ///
    /// It ships off: the option takes over the whole system's Alt+Tab, and that is the user's choice,
    /// not a default of ours.
    /// </summary>
    public bool UseGroupedWindowSwitcher { get; init; }

    /// <summary>Coalescing window for bursts of window events, in milliseconds.</summary>
    public int RefreshDebounceMilliseconds { get; init; } = 150;

    /// <summary>Resolution the icons are extracted at, before scaling in the UI.</summary>
    public int IconExtractionSize { get; init; } = 64;

    /// <summary>
    /// Whether the apps really should be separated per monitor. With a single dock, isolating would
    /// make no sense: the other monitors' apps would simply vanish from the dock.
    /// </summary>
    public bool IsolatesMonitorApps => IsolateMonitorApps && ShowOnAllMonitors;

    /// <summary>
    /// Metrics already adjusted to what the clock shows.
    ///
    /// With seconds showing the text gets wider, and the clock width enters the
    /// panel length. Adjusting here keeps a single source: the window placement and the
    /// XAML keep reading the same measure, and neither has to know why it changed.
    /// </summary>
    public DockMetrics EffectiveMetrics => ShowClockSeconds
        ? Metrics with { ClockContentWidth = Metrics.ClockContentWidthWithSeconds }
        : Metrics;

    /// <summary>
    /// Whether the appbar really should reserve screen space.
    ///
    /// A dock that hides and still keeps the band reserved gives nothing back to the
    /// user: the area sits empty while the dock is off screen. That is why the reservation only applies
    /// when the dock is always in view, and the settings screen disables the option in the
    /// other modes instead of letting it lie.
    /// </summary>
    public bool ReservesScreenSpace => ReserveScreenSpace && HideMode == DockHideMode.Never;

    /// <summary>
    /// Effective magnification factor, already normalized.
    ///
    /// A value below 1 would shrink the icon, and above 2 would make the dock take half the screen;
    /// clamping here keeps a hand-edited settings file from breaking the layout.
    /// </summary>
    public double EffectiveMagnification
    {
        get
        {
            if (!EnableMagnification)
            {
                return 1.0;
            }

            return Math.Clamp(MagnificationScale, 1.0, 2.0);
        }
    }
}
