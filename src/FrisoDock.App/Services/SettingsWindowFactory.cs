using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;

namespace FrisoDock.App.Services;

/// <summary>
/// Opens the settings screen, and keeps it a single window. That is all (SRP).
///
/// It exists so the dock can open the screen without knowing its dependencies: the window is
/// created and destroyed on every open, so it cannot come from the container as a singleton.
///
/// The single window is tracked here, and not in the dock that opened it, because several
/// options rebuild the whole dock set from inside this very screen — the edge, the monitors,
/// the language. The dock that opened the screen is closed by that rebuild, and the one that
/// takes its place would know nothing about the window still standing in front of the user.
/// </summary>
public sealed class SettingsWindowFactory
{
    private readonly DockSettingsService _settings;
    private readonly IWindowBackdrop _backdrop;
    private readonly IWindowActivator _activator;
    private readonly IStartupRegistration _startup;

    private SettingsWindow? _window;

    public SettingsWindowFactory(
        DockSettingsService settings,
        IWindowBackdrop backdrop,
        IWindowActivator activator,
        IStartupRegistration startup)
    {
        _settings = settings;
        _backdrop = backdrop;
        _activator = activator;
        _startup = startup;
    }

    /// <summary>
    /// Shows the screen, or brings the open one forward instead of stacking copies that edit the
    /// same settings.
    /// </summary>
    public void Show()
    {
        if (_window is not null)
        {
            _activator.Activate(_window.Handle);
            return;
        }

        _window = new SettingsWindow(new SettingsViewModel(_settings, _startup), _backdrop);
        _window.Closed += OnClosed;
        _window.Show();

        // The focus comes from the activator, and not from Window.Activate: the dock carries
        // WS_EX_NOACTIVATE and never becomes the foreground, and Windows refuses a focus change to
        // whoever is not in the foreground. Without this the screen opens behind the windows that
        // are already on the desktop.
        _activator.Activate(_window.Handle);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.Closed -= OnClosed;
            _window = null;
        }
    }
}
