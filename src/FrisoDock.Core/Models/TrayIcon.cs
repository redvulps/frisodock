namespace FrisoDock.Core.Models;

/// <summary>
/// Identity of a tray icon.
///
/// Modern apps identify the icon by GUID; older ones, by the window + id pair. Both
/// paths exist because the app chooses which to use when registering, and removal has to match
/// the same key used at registration.
/// </summary>
/// <param name="OwnerWindow">Window that registered the icon.</param>
/// <param name="Id">Identifier of the icon within the owning app.</param>
/// <param name="Item">GUID of the icon, when the app uses that mode.</param>
public readonly record struct TrayIconKey(nint OwnerWindow, uint Id, Guid Item)
{
    public bool UsesGuid => Item != Guid.Empty;

    public bool Equals(TrayIconKey other)
    {
        if (UsesGuid || other.UsesGuid)
        {
            return Item == other.Item;
        }

        return OwnerWindow == other.OwnerWindow && Id == other.Id;
    }

    public override int GetHashCode()
    {
        if (UsesGuid)
        {
            return Item.GetHashCode();
        }

        return HashCode.Combine(OwnerWindow, Id);
    }
}

/// <summary>A notification area icon registered by an app.</summary>
/// <param name="Key">Icon identity.</param>
/// <param name="Tooltip">Tooltip text.</param>
/// <param name="IconHandle">Current HICON, owned by the dock.</param>
/// <param name="CallbackMessage">Message the app expects to receive on interactions.</param>
/// <param name="Version">Protocol version declared by the app (0, 3 or 4).</param>
/// <param name="IsHidden">Whether the app asked for the icon to stay hidden.</param>
public sealed record TrayIcon(
    TrayIconKey Key,
    string Tooltip,
    nint IconHandle,
    uint CallbackMessage,
    uint Version,
    bool IsHidden)
{
    /// <summary>Label to display: the app's tooltip, or a generic text if it reported none.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Tooltip) ? "(sem nome)" : Tooltip.Trim();
}

/// <summary>Point in physical pixels.</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public readonly record struct PixelPoint(int X, int Y);
