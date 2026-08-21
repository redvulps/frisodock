using FrisoDock.App.ViewModels;
using FrisoDock.App.Views;
using FrisoDock.Core.Abstractions;

namespace FrisoDock.App.Services;

/// <summary>
/// Creates the settings screen. That is all (SRP).
///
/// It exists so the dock can open the screen without knowing its dependencies: the window is
/// created and destroyed on every open, so it cannot come from the container as a singleton.
/// </summary>
public sealed class SettingsWindowFactory
{
    private readonly DockSettingsService _settings;
    private readonly IWindowBackdrop _backdrop;

    public SettingsWindowFactory(DockSettingsService settings, IWindowBackdrop backdrop)
    {
        _settings = settings;
        _backdrop = backdrop;
    }

    public SettingsWindow Create()
    {
        return new SettingsWindow(new SettingsViewModel(_settings), _backdrop);
    }
}
