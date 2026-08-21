using FrisoDock.Core.Models;

namespace FrisoDock.App.Services;

/// <summary>
/// Single responsibility: pinning and unpinning apps in the dock.
///
/// It exists so a dock item can ask for the change without knowing the whole list or the
/// storage — the implementer is the bar view model, which owns the collection.
/// </summary>
public interface IPinnedAppsEditor
{
    /// <summary>True if the app is already pinned.</summary>
    bool IsPinned(AppKey key);

    /// <summary>Pins the app if it is not pinned, unpins it if it is. Persists the change.</summary>
    void TogglePin(DockItem item);
}
