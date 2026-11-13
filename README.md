# Item Sheet Maker

<img src="icon.png" width="96" align="right">

A small Windows app that turns a list of item codes into a printable **8.5 x 11 in** product sheet: each product photo with its item code underneath. Export as **PDF** or **JPEG**, or print.

**Version 1.6** — dark, design-tool style workspace with a sheet heading, drag & drop photos, quantities under each item code, and automatic updates.

## Features
- Photos are found automatically in a folder by file name = item code (e.g. `WOP-187A.jpg`; subfolders and names like `WOP-187A front.jpg` also work)
- Type codes and press Enter, or paste a list (comma, space or new-line separated), with autocomplete
- Item list shows a thumbnail of each photo; missing photos are flagged
- Auto-fit all items on one page (up to 30), or a fixed grid (e.g. 4 x 2)
- 1–8 items keep the same photo size as a full 4 x 2 sheet and are centered on the page
- **Quantity (new in 1.6):** type it after the code (`WOP-187A 24`), paste two columns from Excel, or select items and use the Quantity box. Printed in blue under the item code; can be turned off
- Drag & drop photos onto the artboard or an item to use a different photo
- Automatic updates: the app checks GitHub Releases at startup and offers to install new versions
- Optional sheet heading, adjustable code text size, automatic trimming of white space around photos
- 300 DPI output: multi-page PDF, one JPEG per page, or direct print

## Keyboard shortcuts
| Shortcut | Action |
|---|---|
| Ctrl+O | Choose photos folder |
| Ctrl+N | Jump to the item code box |
| Ctrl+G | Toggle auto-fit / grid |
| Ctrl+E | Export PDF |
| Ctrl+J | Export JPEG |
| Ctrl+P | Print |

## Requirements
Windows 10 or 11. Uses the built-in .NET Framework 4.x, so nothing needs to be installed.

## Download
Get `ItemSheetMaker-Setup-x.y.z.exe` from the [Releases](../../releases) page and run it (installs per-user, no admin needed).
The app isn't code-signed, so SmartScreen may warn: click **More info > Run anyway**.

## Build from source
- **Windows:** double-click `build.bat` (uses the `csc.exe` included with Windows) → `bin\ItemSheetMaker.exe`
- **Linux/macOS:** install Mono and run `sh build.sh`

Command-line rendering (for testing):
```
ItemSheetMaker.exe --render <photoFolder> <out.pdf|out.jpg> CODE1 CODE2=24 ...
```

## Project layout
| Path | What it is |
|---|---|
| `src/Program.cs` | Entry point and command-line mode |
| `src/MainForm.cs` | The window and Properties panel (WinForms) |
| `src/Brand.cs` | App bar, document tab, tool rail, panel headers and artboard preview |
| `src/Renderer.cs` | Photo lookup, page layout, JPEG and PDF output |
| `app.ico`, `app.manifest` | Icon and Windows manifest |
