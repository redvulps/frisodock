namespace FrisoDock.Core.Abstractions;

/// <summary>
/// Whether the dock is launched when the user signs in to Windows.
///
/// There is no copy of this in <c>settings.json</c> on purpose: the user can turn the entry off
/// from the Windows "Startup apps" screen, and a persisted copy would drift and start lying.
/// The registration itself is the state.
/// </summary>
public interface IStartupRegistration
{
    /// <summary>True when the dock is registered to start and Windows has not disabled the entry.</summary>
    bool IsEnabled { get; }

    /// <summary>Registers the dock to start at logon. Rewrites the entry if it already exists.</summary>
    void Enable();

    /// <summary>Removes the registration.</summary>
    void Disable();
}
