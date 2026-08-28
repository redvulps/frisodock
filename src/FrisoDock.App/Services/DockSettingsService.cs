using FrisoDock.Core.Abstractions;
using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Holds the settings in force and notifies whoever depends on them when they change. That is
/// all (SRP): writing belongs to <see cref="IDockSettingsStore"/> and reacting to each subscriber.
///
/// It exists because the settings stopped being read once at startup: the settings screen
/// changes them with the dock running, and several services have to react right away.
/// </summary>
public sealed class DockSettingsService
{
    private readonly IDockSettingsStore _store;

    public DockSettingsService(IDockSettingsStore store, DockSettings initial)
    {
        _store = store;
        Current = initial;
    }

    /// <summary>Raised after the settings change, with the new value already in <see cref="Current"/>.</summary>
    public event EventHandler<DockSettingsChangedEventArgs>? Changed;

    /// <summary>Settings in force.</summary>
    public DockSettings Current { get; private set; }

    /// <summary>
    /// Swaps the settings, writes and notifies. Ignores calls that change nothing — the screen
    /// raises one update per toggle click, and each of those would cost a write
    /// to disk and a relayout.
    /// </summary>
    public void Update(DockSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings == Current)
        {
            return;
        }

        DockSettings previous = Current;
        Current = settings;

        _store.Save(settings);
        Changed?.Invoke(this, new DockSettingsChangedEventArgs(previous, settings));
    }
}

/// <summary>A settings change, with the before and the after.</summary>
public sealed class DockSettingsChangedEventArgs : EventArgs
{
    public DockSettingsChangedEventArgs(DockSettings previous, DockSettings current)
    {
        Previous = previous;
        Current = current;
    }

    public DockSettings Previous { get; }

    public DockSettings Current { get; }

    /// <summary>True if the native taskbar must be hidden or revealed now.</summary>
    public bool TaskbarVisibilityChanged => Previous.HideNativeTaskbar != Current.HideNativeTaskbar;

    /// <summary>
    /// True if the screen space reservation changed. It compares the effective value, not the
    /// option: turning autohide on also drops the reservation, without the user touching that switch.
    /// </summary>
    public bool ScreenReservationChanged => Previous.ReservesScreenSpace != Current.ReservesScreenSpace;

    /// <summary>
    /// True if the dock changed edge. Switching edge rebuilds the whole dock set,
    /// like switching monitor: it changes geometry, panel orientation and appbar edge, and
    /// patching the existing window would require reopening the appbar anyway.
    /// </summary>
    public bool EdgeChanged => Previous.Edge != Current.Edge;

    /// <summary>
    /// True if the dock changed language. Like the edge, it rebuilds the dock set: the strings
    /// bound in XAML follow on their own, but the ones a view model built once — the quick
    /// settings tooltip, the switcher captions — were already composed in the old language.
    /// </summary>
    public bool LanguageChanged => Previous.Language != Current.Language;

    /// <summary>True if the dock hide mode changed.</summary>
    public bool HideModeChanged => Previous.HideMode != Current.HideMode;

    /// <summary>True if the dock set has to be rebuilt.</summary>
    public bool MonitorLayoutChanged => Previous.ShowOnAllMonitors != Current.ShowOnAllMonitors;

    /// <summary>True if the per-monitor app separation changed.</summary>
    public bool MonitorIsolationChanged => Previous.IsolatesMonitorApps != Current.IsolatesMonitorApps;

    /// <summary>True if the window switcher changed owner — the dock or Windows.</summary>
    public bool WindowSwitcherChanged => Previous.UseGroupedWindowSwitcher != Current.UseGroupedWindowSwitcher;

    /// <summary>True if the same-app window switcher (Alt+') was turned on or off.</summary>
    public bool SameAppWindowSwitcherChanged => Previous.UseSameAppWindowSwitcher != Current.UseSameAppWindowSwitcher;

    /// <summary>True if the clock changed format.</summary>
    public bool ClockChanged => Previous.ShowClockSeconds != Current.ShowClockSeconds;

    /// <summary>True if the panel metrics changed — today, the clock width.</summary>
    public bool MetricsChanged => Previous.EffectiveMetrics != Current.EffectiveMetrics;

    /// <summary>
    /// True if the panel geometry has to be recomputed. It is not only magnification: seconds
    /// in the clock also widen the panel.
    /// </summary>
    public bool LayoutChanged =>
        Previous.EffectiveMagnification != Current.EffectiveMagnification || MetricsChanged;
}
