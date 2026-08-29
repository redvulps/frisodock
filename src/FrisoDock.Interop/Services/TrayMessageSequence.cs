using FrisoDock.Core.Abstractions;
using FrisoDock.Interop.Native;

namespace FrisoDock.Interop.Services;

/// <summary>
/// Which messages an app expects for each tray interaction. A pure function over the protocol
/// version the app declared, therefore testable without a tray.
///
/// The shape is the one the shell itself delivers, and the two halves are not alternatives: the
/// raw button pair always goes, and from version 3 on the modern notification goes after it. An
/// app that acts on <c>WM_LBUTTONUP</c> and one that acts on <c>NIN_SELECT</c> are both served,
/// which is the whole point, since the app chooses which one to listen to and never says so.
/// </summary>
public static class TrayMessageSequence
{
    /// <summary>
    /// Version from which the app also gets the modern notifications. It is not the same cut as
    /// the one that changes how the parameters are packed, which is version 4: an app can ask for
    /// <c>NIN_SELECT</c> and still expect the old packing.
    /// </summary>
    private const uint ModernNotificationVersion = 3;

    public static IReadOnlyList<uint> For(TrayMouseEvent mouseEvent, uint version)
    {
        bool modern = version >= ModernNotificationVersion;

        return mouseEvent switch
        {
            TrayMouseEvent.LeftClick => modern
                ? [NativeConstants.WM_LBUTTONDOWN, NativeConstants.WM_LBUTTONUP, NativeConstants.NIN_SELECT]
                : [NativeConstants.WM_LBUTTONDOWN, NativeConstants.WM_LBUTTONUP],

            // The double click replaces the press, not the release: that is the sequence a real
            // mouse produces, and plenty of apps only open their window on this one.
            TrayMouseEvent.LeftDoubleClick => modern
                ? [NativeConstants.WM_LBUTTONDBLCLK, NativeConstants.WM_LBUTTONUP, NativeConstants.NIN_SELECT]
                : [NativeConstants.WM_LBUTTONDBLCLK, NativeConstants.WM_LBUTTONUP],

            TrayMouseEvent.RightClick => modern
                ? [NativeConstants.WM_RBUTTONDOWN, NativeConstants.WM_RBUTTONUP, NativeConstants.WM_CONTEXTMENU]
                : [NativeConstants.WM_RBUTTONDOWN, NativeConstants.WM_RBUTTONUP],

            TrayMouseEvent.MiddleClick => [NativeConstants.WM_MBUTTONDOWN, NativeConstants.WM_MBUTTONUP],

            _ => [],
        };
    }
}
