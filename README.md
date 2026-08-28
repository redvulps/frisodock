# FrisoDock

A dock for Windows 11, in the spirit of GNOME's Dash to Dock. It hides the native taskbar and
takes over as launcher and window switcher.

The goal is deliberately narrow: install it and use it. There is no setup ritual, no theme file
to write, no config language to learn. FrisoDock reads the accent color you already picked in
Windows, uses the materials the system itself uses (acrylic on flyouts, mica on the settings
window), and follows the shapes and spacing of Windows 11 instead of inventing its own.

It runs as a normal user. No administrator, no writes to HKLM, no service.

## Launcher and switcher

Pinned apps and running apps sit in the same strip, grouped by executable. An indicator marks
what is open, and a brighter one marks what is focused. Click to activate or minimize, click
again to cycle through the app's windows, middle click for a new instance. Drag an icon to
change its place, and the order sticks.

The Start menu keeps working as usual. FrisoDock's Start button sends the Windows key, and the
menu opens above the dock.

## Jump lists

Right click an icon and you get the app's real jump list, the same entries the native taskbar
would show: the app's own tasks, plus what you pinned and what you opened recently. It is read
from your own `%APPDATA%`, so nothing has to be registered or scraped.

The list is warmed up when the cursor lands on the icon and cached afterwards, so the menu opens
without the pause a cold read would cost. A file watcher drops the cache when the underlying
files change.

## Notification area

The dock hosts the tray itself, so the icons are the real ones and clicks are forwarded to the
app that owns them, context menus included. On exit the tray goes back to Explorer.

Volume, network and battery are drawn by Explorer and never reach a tray host, so they get their
own group in the dock, which opens the quick settings panel.

## Quick settings

A panel in the Windows 11 shape: tiles on top, brightness and volume in the middle, battery and
the settings shortcut at the bottom.

Wi-Fi and Bluetooth really toggle. Brightness and volume really move. Battery and network are
read live. What Windows has no API for without administrator rights, such as airplane mode, opens
the matching settings page instead of pretending to be a switch.

## Window thumbnails

Rest the mouse on an open app and a panel shows the live contents of each of its windows. The DWM
draws them, so they are the real window, not a screenshot, and they keep updating even while the
window is minimized. Apps that are only pinned get a name label instead.

## Magnification

The icon under the cursor grows and overflows above the bar, macOS style, with the neighbours
following in a gradient and being pushed aside as the bar widens. It can be turned off, and its
strength is adjustable.

## Placement

The dock sits on any of the four screen edges, and goes vertical on the sides. With more than one
monitor it can show a dock on every screen, and each dock can be limited to the apps that have a
window on it.

It reserves its strip through the shell, so maximized windows stop at the dock instead of running
under it. Or it can stay out of the way: hide when a window takes its place, or hide always, with
a sliver at the edge that brings it back.

## Keyboard

Two optional gestures, both off by default, because each one takes over a system key and that is
your call to make:

* **Alt+Tab grouped by application.** Lists apps instead of loose windows, and entering an app
  lands on the window you last used.
* **Alt+'** walks through the windows of the app in front, the way ``Cmd+` `` works on macOS.

## Requirements

* Windows 11 22H2 or newer. Windows 10 is secondary and less tested.
* .NET 8 SDK to build (8.0.424 is pinned in `global.json`).

## Build and run

```
dotnet build FrisoDock.sln
dotnet run --project src/FrisoDock.App
```

That hides the native taskbar and reserves screen space for the dock. Both are undone when the
dock exits, including on a crash.

Everything above is toggled from the settings screen in the dock's right click menu, and applies
immediately. It is kept in `%APPDATA%\FrisoDock`.

### Diagnostic flags

Useful when you want to try the dock without touching your working environment.

| Flag | Effect |
|---|---|
| `--keep-taskbar` | Leaves the native taskbar visible. Dock and taskbar coexist. |
| `--no-reserve` | Does not register the appbar, so no screen space is reserved. |
| `--restore-taskbar` | Rescue mode: restores the taskbar and exits. |

If the taskbar is gone and the dock is not running:

```
dotnet run --project src/FrisoDock.App -- --restore-taskbar
```

## Project layout

```
src/FrisoDock.Core       domain: models, abstractions and pure functions. No UI, no Win32
src/FrisoDock.Interop    the only place with P/Invoke. Win32 implementations of the abstractions
src/FrisoDock.App        WPF, MVVM and dependency injection. Produces FrisoDock.exe
tests/FrisoDock.Tests    xUnit over Core
```

Two rules hold the thing together. Every `DllImport`, constant and Win32 struct lives in
`FrisoDock.Interop/Native`, nowhere else. Every measurement comes from `DockMetrics`, so window
placement and XAML read the same number instead of each keeping its own copy.

```
dotnet test FrisoDock.sln
```

## License

MIT. See [LICENSE](LICENSE).
