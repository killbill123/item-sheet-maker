# Item Sheet Maker

<img src="icon.png" width="96" align="right">

A small Windows app that turns a list of item codes into a printable **8.5 x 11 in** product sheet: each product photo with its item code underneath. Save as **PDF** or **JPEG**, or print.

**Version 1.2** — new navy + orange branded interface.

## Features
- Photos are found automatically in a folder by file name = item code (e.g. `WOP-187A.jpg`; subfolders and names like `WOP-187A front.jpg` also work)
- Type codes and press Enter, or paste a list (comma, space or new-line separated), with autocomplete
- Auto-fit all items on one page (up to 30), or a fixed grid (e.g. 4 x 2)
- 1–8 items keep the same photo size as a full 4 x 2 sheet and are centered on the page
- Optional title, adjustable code text size, automatic trimming of white space around photos
- 300 DPI output: multi-page PDF, one JPEG per page, or direct print
- Missing photos are flagged in orange and shown as an empty box

## Brand colors
| Navy | Orange | Cream | Off-white |
|---|---|---|---|
| `#102E50` | `#FE5D26` | `#FFE8CD` | `#FBFBFB` |

## Requirements
Windows 10 or 11. Uses the built-in .NET Framework 4.x, so nothing needs to be installed.

## Download
Get `ItemSheetMaker.zip` from the [Releases](../../releases) page, unzip, and run `ItemSheetMaker.exe`.
The app isn't code-signed, so SmartScreen may warn: click **More info > Run anyway**.

## Build from source
- **Windows:** double-click `build.bat` (uses the `csc.exe` included with Windows) → `bin\ItemSheetMaker.exe`
- **Linux/macOS:** install Mono and run `sh build.sh`

Command-line rendering (for testing):
```
ItemSheetMaker.exe --render <photoFolder> <out.pdf|out.jpg> CODE1 CODE2 ...
```

## Project layout
| Path | What it is |
|---|---|
| `src/Program.cs` | Entry point and command-line mode |
| `src/MainForm.cs` | The window (WinForms) |
| `src/Brand.cs` | Brand header, section titles and page preview controls |
| `src/Renderer.cs` | Photo lookup, page layout, JPEG and PDF output |
| `app.ico`, `app.manifest` | Icon and Windows manifest |
