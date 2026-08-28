using System.Globalization;
using System.Resources;

namespace FrisoDock.Core.Resources;

/// <summary>
/// The dock's translated text. That is all (SRP): choosing the language belongs to whoever
/// applies <see cref="CultureInfo.CurrentUICulture" />, and this class only reads it.
///
/// It reads the UI culture on every call, and not once in a static field, because the language
/// can change with the dock running. That is also why the properties are computed and not
/// constants.
///
/// The UI culture is deliberately not the same thing as the regional format: someone running
/// Windows in English with the Brazilian format has to get an English dock still showing
/// 28/08/2026. The clock and the calendar read <see cref="CultureInfo.CurrentCulture" />; only
/// this class reads the UI culture.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("FrisoDock.Core.Resources.Strings", typeof(Strings).Assembly);

    /// <summary>FrisoDock is already running.</summary>
    public static string AppAlreadyRunning => Get(nameof(AppAlreadyRunning));

    /// <summary>Dock position</summary>
    public static string MenuDockPosition => Get(nameof(MenuDockPosition));

    /// <summary>Settings…</summary>
    public static string MenuSettings => Get(nameof(MenuSettings));

    /// <summary>Exit FrisoDock</summary>
    public static string MenuExit => Get(nameof(MenuExit));

    /// <summary>Bottom</summary>
    public static string EdgeBottom => Get(nameof(EdgeBottom));

    /// <summary>Top</summary>
    public static string EdgeTop => Get(nameof(EdgeTop));

    /// <summary>Left</summary>
    public static string EdgeLeft => Get(nameof(EdgeLeft));

    /// <summary>Right</summary>
    public static string EdgeRight => Get(nameof(EdgeRight));

    /// <summary>Start</summary>
    public static string DockStart => Get(nameof(DockStart));

    /// <summary>Tray icons</summary>
    public static string DockTrayIcons => Get(nameof(DockTrayIcons));

    /// <summary>No icons in the tray</summary>
    public static string TrayEmpty => Get(nameof(TrayEmpty));

    /// <summary>Previous month</summary>
    public static string CalendarPreviousMonth => Get(nameof(CalendarPreviousMonth));

    /// <summary>Next month</summary>
    public static string CalendarNextMonth => Get(nameof(CalendarNextMonth));

    /// <summary>Quick settings</summary>
    public static string QuickTitle => Get(nameof(QuickTitle));

    /// <summary>No connection</summary>
    public static string QuickNoConnection => Get(nameof(QuickNoConnection));

    /// <summary>Muted</summary>
    public static string QuickMuted => Get(nameof(QuickMuted));

    /// <summary>Volume {0}%</summary>
    public static string QuickVolumeFormat => Get(nameof(QuickVolumeFormat));

    /// <summary>Battery {0}%</summary>
    public static string QuickBatteryFormat => Get(nameof(QuickBatteryFormat));

    /// <summary>Mute</summary>
    public static string QuickMute => Get(nameof(QuickMute));

    /// <summary>Sound devices</summary>
    public static string QuickSoundDevices => Get(nameof(QuickSoundDevices));

    /// <summary>Power and battery</summary>
    public static string QuickPowerAndBattery => Get(nameof(QuickPowerAndBattery));

    /// <summary>Windows settings</summary>
    public static string QuickWindowsSettings => Get(nameof(QuickWindowsSettings));

    /// <summary>Wi-Fi</summary>
    public static string QuickWifi => Get(nameof(QuickWifi));

    /// <summary>Bluetooth</summary>
    public static string QuickBluetooth => Get(nameof(QuickBluetooth));

    /// <summary>Airplane mode</summary>
    public static string QuickAirplaneMode => Get(nameof(QuickAirplaneMode));

    /// <summary>Accessibility</summary>
    public static string QuickAccessibility => Get(nameof(QuickAccessibility));

    /// <summary>VPN</summary>
    public static string QuickVpn => Get(nameof(QuickVpn));

    /// <summary>Battery saver</summary>
    public static string QuickBatterySaver => Get(nameof(QuickBatterySaver));

    /// <summary>Tasks</summary>
    public static string JumpListTasks => Get(nameof(JumpListTasks));

    /// <summary>Pinned</summary>
    public static string JumpListPinned => Get(nameof(JumpListPinned));

    /// <summary>Recent</summary>
    public static string JumpListRecent => Get(nameof(JumpListRecent));

    /// <summary>Pin to taskbar</summary>
    public static string JumpListPin => Get(nameof(JumpListPin));

    /// <summary>Unpin from taskbar</summary>
    public static string JumpListUnpin => Get(nameof(JumpListUnpin));

    /// <summary>Close window</summary>
    public static string JumpListCloseWindow => Get(nameof(JumpListCloseWindow));

    /// <summary>Close all windows</summary>
    public static string JumpListCloseAllWindows => Get(nameof(JumpListCloseAllWindows));

    /// <summary>{0}  ·  {1} windows</summary>
    public static string SwitcherWindowCountFormat => Get(nameof(SwitcherWindowCountFormat));

    /// <summary>FrisoDock settings</summary>
    public static string SettingsWindowTitle => Get(nameof(SettingsWindowTitle));

    /// <summary>Settings</summary>
    public static string SettingsTitle => Get(nameof(SettingsTitle));

    /// <summary>Changes apply right away and are saved.</summary>
    public static string SettingsSubtitle => Get(nameof(SettingsSubtitle));

    /// <summary>Close</summary>
    public static string SettingsClose => Get(nameof(SettingsClose));

    /// <summary>Appearance</summary>
    public static string SettingsTabAppearance => Get(nameof(SettingsTabAppearance));

    /// <summary>Position</summary>
    public static string SettingsTabPosition => Get(nameof(SettingsTabPosition));

    /// <summary>Hide the dock</summary>
    public static string SettingsTabHiding => Get(nameof(SettingsTabHiding));

    /// <summary>Switch windows</summary>
    public static string SettingsTabSwitcher => Get(nameof(SettingsTabSwitcher));

    /// <summary>Monitors</summary>
    public static string SettingsTabMonitors => Get(nameof(SettingsTabMonitors));

    /// <summary>Desktop</summary>
    public static string SettingsTabDesktop => Get(nameof(SettingsTabDesktop));

    /// <summary>Language</summary>
    public static string SettingsTabLanguage => Get(nameof(SettingsTabLanguage));

    /// <summary>Magnify the icon under the cursor</summary>
    public static string SettingsMagnificationTitle => Get(nameof(SettingsMagnificationTitle));

    /// <summary>The icon grows over the bar, and its neighbours follow.</summary>
    public static string SettingsMagnificationDetail => Get(nameof(SettingsMagnificationDetail));

    /// <summary>Magnification strength</summary>
    public static string SettingsMagnificationScaleTitle => Get(nameof(SettingsMagnificationScaleTitle));

    /// <summary>Window thumbnails</summary>
    public static string SettingsPreviewsTitle => Get(nameof(SettingsPreviewsTitle));

    /// <summary>Resting the mouse on an open app shows what is in each window.</summary>
    public static string SettingsPreviewsDetail => Get(nameof(SettingsPreviewsDetail));

    /// <summary>Show the seconds</summary>
    public static string SettingsClockSecondsTitle => Get(nameof(SettingsClockSecondsTitle));

    /// <summary>The clock counts second by second, and the bar widens to fit.</summary>
    public static string SettingsClockSecondsDetail => Get(nameof(SettingsClockSecondsDetail));

    /// <summary>The default: the dock sits on the bottom edge, where the taskbar used to be.</summary>
    public static string SettingsEdgeBottomDetail => Get(nameof(SettingsEdgeBottomDetail));

    /// <summary>The dock sits on the top edge, and the menus open downwards.</summary>
    public static string SettingsEdgeTopDetail => Get(nameof(SettingsEdgeTopDetail));

    /// <summary>Vertical dock on the left edge, as in GNOME.</summary>
    public static string SettingsEdgeLeftDetail => Get(nameof(SettingsEdgeLeftDetail));

    /// <summary>Vertical dock on the right edge.</summary>
    public static string SettingsEdgeRightDetail => Get(nameof(SettingsEdgeRightDetail));

    /// <summary>Never</summary>
    public static string SettingsHideNeverTitle => Get(nameof(SettingsHideNeverTitle));

    /// <summary>The dock stays on the edge all the time.</summary>
    public static string SettingsHideNeverDetail => Get(nameof(SettingsHideNeverDetail));

    /// <summary>When a window takes its place</summary>
    public static string SettingsHideOverlapTitle => Get(nameof(SettingsHideOverlapTitle));

    /// <summary>With the area free the dock stays in view; it goes away when a window reaches it.</summary>
    public static string SettingsHideOverlapDetail => Get(nameof(SettingsHideOverlapDetail));

    /// <summary>Always</summary>
    public static string SettingsHideAlwaysTitle => Get(nameof(SettingsHideAlwaysTitle));

    /// <summary>The dock only appears when the cursor touches the screen edge.</summary>
    public static string SettingsHideAlwaysDetail => Get(nameof(SettingsHideAlwaysDetail));

    /// <summary>Use Alt+Tab grouped by application</summary>
    public static string SettingsGroupedSwitcherTitle => Get(nameof(SettingsGroupedSwitcherTitle));

    /// <summary>One item per app instead of one per window; reaching an app enters through the window that was in use. Off, the Windows Alt+Tab applies.</summary>
    public static string SettingsGroupedSwitcherDetail => Get(nameof(SettingsGroupedSwitcherDetail));

    /// <summary>Switch between windows of the same app (ALT+')</summary>
    public static string SettingsSameAppSwitcherTitle => Get(nameof(SettingsSameAppSwitcherTitle));

    /// <summary>With Alt held, the ' key walks the windows of the focused app, macOS style.</summary>
    public static string SettingsSameAppSwitcherDetail => Get(nameof(SettingsSameAppSwitcherDetail));

    /// <summary>Show on every monitor</summary>
    public static string SettingsAllMonitorsTitle => Get(nameof(SettingsAllMonitorsTitle));

    /// <summary>One dock on each screen. Off, the dock stays only on the primary monitor.</summary>
    public static string SettingsAllMonitorsDetail => Get(nameof(SettingsAllMonitorsDetail));

    /// <summary>Show only the apps present on the monitor</summary>
    public static string SettingsIsolateAppsTitle => Get(nameof(SettingsIsolateAppsTitle));

    /// <summary>Each dock shows only the windows of its own screen. Pinned apps stay in all of them.</summary>
    public static string SettingsIsolateAppsDetail => Get(nameof(SettingsIsolateAppsDetail));

    /// <summary>Hide the Windows taskbar</summary>
    public static string SettingsHideTaskbarTitle => Get(nameof(SettingsHideTaskbarTitle));

    /// <summary>Turning it off brings the native bar back without closing the dock.</summary>
    public static string SettingsHideTaskbarDetail => Get(nameof(SettingsHideTaskbarDetail));

    /// <summary>Reserve screen space</summary>
    public static string SettingsReserveSpaceTitle => Get(nameof(SettingsReserveSpaceTitle));

    /// <summary>Keeps maximized windows from sitting under the dock. Only applies with the dock always in view.</summary>
    public static string SettingsReserveSpaceDetail => Get(nameof(SettingsReserveSpaceDetail));

    /// <summary>Use the system language</summary>
    public static string SettingsLanguageSystemTitle => Get(nameof(SettingsLanguageSystemTitle));

    /// <summary>The dock follows the Windows display language, and falls back to English when it is none of the ones below.</summary>
    public static string SettingsLanguageSystemDetail => Get(nameof(SettingsLanguageSystemDetail));

    /// <summary>Português (Brasil)</summary>
    public static string SettingsLanguagePortugueseBrazil => Get(nameof(SettingsLanguagePortugueseBrazil));

    /// <summary>English</summary>
    public static string SettingsLanguageEnglish => Get(nameof(SettingsLanguageEnglish));

    /// <summary>Español</summary>
    public static string SettingsLanguageSpanish => Get(nameof(SettingsLanguageSpanish));

    /// <summary>
    /// Every key in the catalogue. It exists for the tests that check the three languages carry
    /// the same set, so a missing translation shows up in the build and not on screen.
    /// </summary>
    public static IReadOnlyList<string> Keys { get; } =
    [
        nameof(AppAlreadyRunning),
        nameof(MenuDockPosition),
        nameof(MenuSettings),
        nameof(MenuExit),
        nameof(EdgeBottom),
        nameof(EdgeTop),
        nameof(EdgeLeft),
        nameof(EdgeRight),
        nameof(DockStart),
        nameof(DockTrayIcons),
        nameof(TrayEmpty),
        nameof(CalendarPreviousMonth),
        nameof(CalendarNextMonth),
        nameof(QuickTitle),
        nameof(QuickNoConnection),
        nameof(QuickMuted),
        nameof(QuickVolumeFormat),
        nameof(QuickBatteryFormat),
        nameof(QuickMute),
        nameof(QuickSoundDevices),
        nameof(QuickPowerAndBattery),
        nameof(QuickWindowsSettings),
        nameof(QuickWifi),
        nameof(QuickBluetooth),
        nameof(QuickAirplaneMode),
        nameof(QuickAccessibility),
        nameof(QuickVpn),
        nameof(QuickBatterySaver),
        nameof(JumpListTasks),
        nameof(JumpListPinned),
        nameof(JumpListRecent),
        nameof(JumpListPin),
        nameof(JumpListUnpin),
        nameof(JumpListCloseWindow),
        nameof(JumpListCloseAllWindows),
        nameof(SwitcherWindowCountFormat),
        nameof(SettingsWindowTitle),
        nameof(SettingsTitle),
        nameof(SettingsSubtitle),
        nameof(SettingsClose),
        nameof(SettingsTabAppearance),
        nameof(SettingsTabPosition),
        nameof(SettingsTabHiding),
        nameof(SettingsTabSwitcher),
        nameof(SettingsTabMonitors),
        nameof(SettingsTabDesktop),
        nameof(SettingsTabLanguage),
        nameof(SettingsMagnificationTitle),
        nameof(SettingsMagnificationDetail),
        nameof(SettingsMagnificationScaleTitle),
        nameof(SettingsPreviewsTitle),
        nameof(SettingsPreviewsDetail),
        nameof(SettingsClockSecondsTitle),
        nameof(SettingsClockSecondsDetail),
        nameof(SettingsEdgeBottomDetail),
        nameof(SettingsEdgeTopDetail),
        nameof(SettingsEdgeLeftDetail),
        nameof(SettingsEdgeRightDetail),
        nameof(SettingsHideNeverTitle),
        nameof(SettingsHideNeverDetail),
        nameof(SettingsHideOverlapTitle),
        nameof(SettingsHideOverlapDetail),
        nameof(SettingsHideAlwaysTitle),
        nameof(SettingsHideAlwaysDetail),
        nameof(SettingsGroupedSwitcherTitle),
        nameof(SettingsGroupedSwitcherDetail),
        nameof(SettingsSameAppSwitcherTitle),
        nameof(SettingsSameAppSwitcherDetail),
        nameof(SettingsAllMonitorsTitle),
        nameof(SettingsAllMonitorsDetail),
        nameof(SettingsIsolateAppsTitle),
        nameof(SettingsIsolateAppsDetail),
        nameof(SettingsHideTaskbarTitle),
        nameof(SettingsHideTaskbarDetail),
        nameof(SettingsReserveSpaceTitle),
        nameof(SettingsReserveSpaceDetail),
        nameof(SettingsLanguageSystemTitle),
        nameof(SettingsLanguageSystemDetail),
        nameof(SettingsLanguagePortugueseBrazil),
        nameof(SettingsLanguageEnglish),
        nameof(SettingsLanguageSpanish),
    ];

    /// <summary>
    /// The text for a key, in the current UI culture.
    ///
    /// A missing key comes back marked instead of empty: an unbound label vanishing from the
    /// screen hides the mistake, while "!Key!" points straight at it.
    /// </summary>
    public static string Get(string key)
    {
        return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";
    }
}
