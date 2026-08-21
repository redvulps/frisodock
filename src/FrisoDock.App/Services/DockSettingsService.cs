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

    /// <summary>True if the screen space reservation changed.</summary>
    public bool ScreenReservationChanged => Previous.ReserveScreenSpace != Current.ReserveScreenSpace;

    /// <summary>True if the panel geometry has to be recomputed.</summary>
    public bool LayoutChanged => Previous.EffectiveMagnification != Current.EffectiveMagnification;
}
