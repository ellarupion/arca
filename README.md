# arca

**English** · [Русский](README.ru.md)

*arca* is Latin for "chest": a box of tiny Windows tools.

One exe, ~50 KB, no installer, no dependencies, ~15 MB RAM. Every tool can be switched on or off,
arca updates itself from GitHub Releases, and it speaks English and Russian.

| Tool | What it does |
| --- | --- |
| **Light/dark theme** | Click the ◐ icon to switch the Windows theme. Optional daily schedule. |
| **Recycle Bin** | The bin next to the clock: open, empty, auto-empty over a size limit. |
| **Window tools** | Keep any window on top, hide it to the tray, make it transparent, move it with Alt + drag. |

## Download

Grab `arca.exe` from [Releases](https://github.com/ellarupion/arca/releases/latest) and run it. Needs .NET Framework 4, which is built into Windows 10/11.

## Usage

Click the arca icon (a box) next to the clock for the main menu:

- **Tools**: switch each tool on or off
- **Settings…**: opens `arca.ini`
- **Language**: same as Windows, English or Russian
- **Start with Windows**
- **Check for updates automatically** / **Check for updates now**

### Light/dark theme

- **Left click** the ◐ icon: switch light ↔ dark
- **Right click**: auto schedule (default 07:00 light, 19:00 dark), edit schedule

A manual switch stays until the next scheduled one.

### Recycle Bin

- **Double click** the bin icon: open the Recycle Bin (or single click, if you turn off *Open on double-click*)
- **Right click**: **Empty Recycle Bin** first, then open, ask before emptying, auto-empty over N GB

The icon gets a dot when there's something in the bin. Hover to see how many items and how much space.

### Window tools

| Shortcut | Action on the active window |
| --- | --- |
| `Ctrl+Win+T` | Keep on top / release |
| `Ctrl+Win+H` | Hide to the tray. Click its tray icon to bring it back |
| `Ctrl+Win+PageDown` | More transparent (down to 20%) |
| `Ctrl+Win+PageUp` | Less transparent |
| `Alt` + drag | Move a window by grabbing it anywhere (off by default) |

The *Window tools* submenu lists hidden and pinned windows, so you can bring them back from there too.
When arca exits, every window goes back to normal. If arca is killed, hidden windows reappear on its next start.

Apps running as administrator can only be changed when arca runs as administrator too.

## Config

`arca.ini` is created next to the exe and picked up as soon as you save it:

```ini
[arca]
language=auto
updates=1
theme=1
bin=1
windows=1

[theme]
light=07:00
dark=19:00
auto=1

[bin]
doubleclick=1
confirm=1
auto=0
limit_gb=10

[windows]
on_top=Ctrl+Win+T
hide=Ctrl+Win+H
fade=Ctrl+Win+PageDown
unfade=Ctrl+Win+PageUp
alt_drag=0
```

`language` is `auto` (follow Windows), `en` or `ru`.
Shortcuts take `Ctrl`, `Alt`, `Shift`, `Win` and any key name (`T`, `F9`, `PageUp`, `Space`…). Leave one empty to turn it off.

## Build from source

The C# compiler ships with Windows, so you don't need to install anything:

```
build.cmd
```

This produces `arca.exe`. Put it anywhere and run it.

## Updates

arca checks `https://api.github.com/repos/ellarupion/arca/releases/latest` at startup and every 12 hours.
When a newer version is out, it shows a notification and an **Update** item at the top of the menu.
Updating downloads the new `arca.exe`, swaps it in place and restarts. Nothing is sent besides that request.

## License

MIT
