using System.Windows;

namespace FrisoDock.App.Services;

/// <summary>
/// Single responsibility: shutting the application down.
///
/// It exists as an abstraction so the ViewModels do not call <c>Application.Current</c> directly.
/// Shutdown has to go through the normal WPF lifetime — that is what raises
/// <c>App.OnExit</c>, and that is where the native taskbar is restored.
/// </summary>
public interface IApplicationLifetime
{
    /// <summary>Starts the orderly shutdown of the application.</summary>
    void Shutdown();
}

/// <summary>Shutdown over the WPF <see cref="Application"/>.</summary>
public sealed class WpfApplicationLifetime : IApplicationLifetime
{
    public void Shutdown()
    {
        Application.Current?.Shutdown();
    }
}
