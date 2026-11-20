# ScreenCapture


A Windows tray screenshot tool: capture an area or the whole screen, mark it up, then copy or save it. Built with .NET 10, Avalonia and SkiaSharp. Requires Windows 10 1809+ or Windows 11 (x64).

## Features

- Tray icon; left or right click opens the menu (Capture area, Capture full screen, Recognize text in area, Settings, Exit).
- Global hotkeys: **Ctrl+Alt+5** captures an area, **Ctrl+Alt+6** captures the monitor under the cursor, **Ctrl+Alt+7** recognizes the text in a selected area.
- Area capture freezes and dims the screen; drag a rectangle (size shown in pixels), Esc cancels. Selection stays within one monitor.
- Editor tools: Arrow, Line, Rectangle, Marker, Text, Blur, Recognize text. 8 preset colors and five stroke widths.
- Annotations are objects: select, move or delete them while the editor is open.
- Undo/redo: Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z.
- Copy (Ctrl+C, primary button): puts the image with annotations on the clipboard and closes the editor.
- Save (Ctrl+S): PNG or JPG (JPG quality 1-100); the editor stays open.
- Settings: language (English / Ukrainian), hotkeys, default format and JPG quality, save folder, start with Windows.

## Text recognition

In the editor, pick **Recognize text** and drag over the text (a click reads the whole screenshot); from the tray or with **Ctrl+Alt+7**, select an area of the screen directly. The text is copied to the clipboard and opened in the default text editor (a UTF-8 file in `%TEMP%\ScreenCapture\ocr`, removed after a day). Ukrainian and English are read together; annotations are ignored.

Recognition runs offline with Tesseract 5.5 (`tessdata_fast` models). Its files are inside the exe and are unpacked to `%LOCALAPPDATA%\ScreenCapture\ocr` on first use. It needs the Microsoft Visual C++ 2015-2022 Redistributable (x64), which almost every Windows machine already has; without it the app shows the download link.

## Hotkeys

Defaults are Ctrl+Alt+5 (area), Ctrl+Alt+6 (full screen) and Ctrl+Alt+7 (recognize text). To change one, open Settings, click the hotkey field and press the new combination; it applies immediately. A combination needs a modifier unless it is a function key or PrintScreen. Each hotkey can be reset or cleared. A combination already used by another app or by another action is rejected and the old one is kept.

## Files

- Settings: `%APPDATA%\ScreenCapture\settings.json` (defaults are used if it is corrupt)
- Log: `%APPDATA%\ScreenCapture\logs\app.log`
- Default save folder: `<Pictures>\Screenshots`, file names `Screenshot yyyy-MM-dd HH-mm-ss.<ext>`

## Build, run, test

Requires the .NET 10 SDK.

```
dotnet build ScreenCapture.sln
dotnet run --project src/ScreenCapture.App
dotnet test ScreenCapture.sln
```

The `ScreenCapture.Platform.Windows.Tests` project touches real system state: it overwrites the clipboard and writes (then removes) a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. Its OCR tests run the real Tesseract engine.

## Publish

```
pwsh ./publish.ps1
```

Runs the Core and App tests (not the Platform.Windows tests, see above), publishes a self-contained single-file win-x64 exe, and creates `artifacts/ScreenCapture-win-x64.zip`. Unzip anywhere and run `ScreenCapture.App.exe`; no .NET install is needed.

## Adding a language

1. Copy `src/ScreenCapture.Core/Localization/Strings.uk.resx` to `Strings.<code>.resx` in the same folder (for example `Strings.de.resx`).
2. Translate the values; keep the keys.
3. Rebuild. The language appears in Settings automatically.

A test checks that every key exists in every language file.

## Known limits

- No HDR support (colors may look washed out on HDR monitors).
- Some protected or hardware-accelerated windows may be captured black.
- The exe is unsigned, so Windows SmartScreen warns on first run.
- Selection across several monitors is not supported.
- A hotkey already taken by another app cannot be assigned.
- Text recognition keeps lines but not layout (tables, columns), and does not read handwriting. `ґ` is often read as `г`.

## Third-party components

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
