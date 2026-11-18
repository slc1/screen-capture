# Screen Capture — v1 Design

Date: 2026-11-18
Working name: **ScreenCapture** (final name can change later)

## Why

A tray screenshot utility in the spirit of Monosnap (https://monosnap.ai/). First users are the author and their team; later it may become a product. So v1 must be pleasant for daily use, and the architecture must not block a later move to macOS/Linux or public distribution.

Success: capture a part of the screen, mark something on it, and paste it into a chat, ticket or document within a few seconds.

## Goals (v1)

1. The app starts into the system tray. **Left click** on the icon opens a menu; right click opens the same menu.
2. Menu items:
   - **Capture area**: select a part of the screen with the mouse;
   - **Capture full screen**;
   - **Settings**;
   - **Exit**.
   The capture items show their hotkeys.
3. **Global hotkeys** for both capture modes, reassignable in Settings: click the hotkey field, press the new combination, and it is applied immediately (no restart). A combination needs at least one modifier (Ctrl/Alt/Shift/Win) unless it is a function key or PrintScreen. If the combination is taken by another app or by the other capture mode, the user sees a message and the previous hotkey stays. Each hotkey can be reset to its default or cleared (no hotkey).
4. **Area selection:** the screen is frozen and dimmed, and the user drags a rectangle. The selected area is shown undimmed, with its size in pixels next to it. Esc cancels. Releasing the mouse button opens the editor.
5. **Editor** with tools:
   - **Arrow**;
   - **Line** (for underlining);
   - **Rectangle** (outline, for highlighting);
   - **Marker**: semi-transparent highlighter (wide stroke, freehand or straight — builder's choice);
   - **Text**;
   - **Blur**: blur or pixelate a rectangular area (to hide passwords and personal data);
   - a small preset palette (6–8 colors) and stroke width (3 options).
6. Annotations are **objects**, not painted pixels. While the editor is open, an annotation can be selected, moved or deleted. Resizing is not required in v1.
7. **Undo/redo** for every action: add, move, delete or change an annotation. Ctrl+Z undoes; Ctrl+Y and Ctrl+Shift+Z redo. History is unlimited but lives only while the editor is open.
8. **Output:**
   - **Copy** (Ctrl+C, primary button): copies the image with annotations flattened into it to the clipboard, then **closes the editor** (author's decision). If the clipboard write fails, the editor stays open and the user sees an error.
   - **Save** (Ctrl+S): opens a save dialog. Format PNG or JPG; for JPG a quality slider 1–100. Default folder, format and quality come from Settings. The editor stays open after Save, so the user can still Copy (author's decision).
   - Annotations are part of the output, in both the copied and the saved image.
9. **Settings** (a separate window):
   - language: English or Ukrainian;
   - hotkeys for both modes;
   - default format (PNG or JPG) and JPG quality;
   - save folder;
   - start with Windows.
10. **Localization:** English and Ukrainian, selectable in Settings. Adding a language means adding one translation file and rebuilding — no code changes. On first run the language follows the Windows UI language, falling back to English if unsupported.

## Non-goals (v1)

- Video or GIF recording.
- Cloud upload, share links.
- Delayed capture, single-window capture.
- macOS and Linux (but the architecture accounts for them, see below).
- Installer, auto-update, code signing.
- Screenshot history; re-editing annotations after the editor is closed.

## Anti-goals: what makes v1 a failure even if everything "works"

- **Slow capture start.** From hotkey press to the dimmed screen must take under ~200 ms.
- **Blurry screenshots** at 125–200 % scaling or on multi-monitor setups with mixed scaling. Capture is always at physical pixels, 1:1.
- **Offset selection:** the image contains something other than what the user selected (the classic DPI bug).
- **Lost screenshot:** if something fails (e.g. clipboard write), the user gets a message, not silence.

## Constraints

- v1 platform: Windows 10 (1809+) and Windows 11, x64.
- Stack: **C# / .NET 10** and **Avalonia UI** (latest stable 11.x), IDE JetBrains Rider. .NET SDK 10.0.404 is installed.
- Single instance. A second launch opens nothing new (showing the menu is the builder's choice).
- No user-facing strings in code; everything goes through localization resources.

## Approaches considered

| Approach | Pros | Cons |
|---|---|---|
| **C# + Avalonia** (chosen) | Fast, native, easy Win32 access. Convenient .resx localization. Small size, Rider. Real path to macOS/Linux | Screen capture written per OS. Fewer existing screenshot-tool examples |
| Qt (C++ / PySide6) | Proven by Flameshot, Linux/Wayland already solved | C++ slower to develop; PySide heavy and harder to package. LGPL complicates a product |
| Tauri (Rust + web) | Smallest binary, editor easy on canvas | Transparent overlay across mixed-DPI monitors is fragile; requires Rust |
| Delphi (VCL / FMX) | Small native exe, excellent on Windows | VCL is Windows-only. FMX has no tray, Linux needs Enterprise. Paid license, few developers |

Dropped early: Electron (too heavy for an always-on tray app) and Kotlin Compose Desktop (weak AWT tray, DPI issues).

**Why Avalonia:** best result on Windows, where the app ships first; a real path to other OSes; no licensing constraints for a future product. The author agreed.

## Decided implementation

### Solution layout

```
ScreenCapture.sln
src/
  ScreenCapture.Core/        // OS-independent: annotation model, undo/redo,
                             // export (PNG/JPG), settings, localization
  ScreenCapture.Platform/    // platform interfaces (below)
  ScreenCapture.Platform.Windows/   // Win32 implementations
  ScreenCapture.App/         // Avalonia: tray, overlay, editor, settings
tests/
  ScreenCapture.Core.Tests/  // xUnit
```

### Platform abstractions (for future macOS/Linux)

- `IScreenCapturer`: capture all monitors (or one) at physical pixels, with monitor geometry and scale.
- `IGlobalHotkeyService`: register/unregister hotkeys, a fired event, conflict reporting (combination already taken).
- `IClipboardImageService`: write an image to the clipboard.
- `IAutostartService`: enable/disable start with the OS.

Windows implementations:
- capture via GDI (`BitBlt` from the screen DC) with per-monitor DPI awareness v2 in the manifest;
- hotkeys via `RegisterHotKey`;
- clipboard in two formats at once, `CF_DIB` and `PNG`, so pasting works in both old and new apps;
- autostart via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.

### Capture flow

1. Hotkey or menu item → immediately capture all monitors into memory (before showing any window).
2. On each monitor show a separate full-screen topmost overlay window with that monitor's frozen image and the dimming. One window per monitor is deliberate: a single window spanning monitors with different DPI is unreliable in Avalonia/Windows.
3. A selection stays within one monitor (cross-monitor selection is not supported in v1). Drag a rectangle; Esc cancels.
4. The cropped fragment (physical pixels) opens in an editor.
5. **Capture full screen** captures the monitor under the cursor and opens the editor directly.

### Editor

- A window with a canvas (image + annotation layer) and a toolbar. If the image is larger than the window, it is scaled to fit; annotations are stored in image coordinates.
- Model: a list of `Annotation` objects (Arrow, Line, Rectangle, Marker, Text, Blur), each with geometry, color and stroke width.
- Undo/redo via commands (`AddAnnotation`, `MoveAnnotation`, `RemoveAnnotation`, `ChangeText`, …) with two stacks. Model and commands live in Core and are unit-tested.
- Blur samples the **original** image under its area, so a moved blur blurs the new spot.
- Export renders the original image plus annotations into one raster at physical pixels.
- Several editors may be open at once; each capture opens its own window.

### Settings

- JSON at `%APPDATA%\ScreenCapture\settings.json`. If the file is corrupt, defaults are used and the user is notified.
- Defaults:
  - format PNG;
  - JPG quality 90;
  - folder `Pictures\Screenshots`;
  - autostart off;
  - hotkeys **Ctrl+Alt+5** (area) and **Ctrl+Alt+6** (full screen). PrintScreen is deliberately avoided: on Windows 11 it opens Snipping Tool by default.
- Default file name: `Screenshot yyyy-MM-dd HH-mm-ss.png`.

### Localization

- `.resx`: `Strings.resx` (English, neutral) and `Strings.uk.resx`. A new language = a new `Strings.<code>.resx` file and a rebuild; the list of available languages is discovered from the shipped satellite assemblies, not hand-maintained.
- Language switches immediately, without restart.
- A Core test verifies every key exists in every language file.

### Team build

- `dotnet publish` as a self-contained single-file exe for win-x64, zipped.
- Unsigned, so Windows SmartScreen warns on first run. Acceptable internally; a product needs signing.

## Calls I made (the author may change them)

- Features beyond the original request: hotkeys, full-screen capture, redo, text and blur, settings window (author agreed).
- Right click on the tray opens the same menu as left click.
- Annotations can be moved and deleted, not only drawn.
- "Clipboard by default" means **Copy** is the editor's primary action (Ctrl+C, highlighted button). The raw capture is not auto-copied.
- Hotkeys Ctrl+Alt+5 and Ctrl+Alt+6.
- Selection within one monitor.
- PNG has no quality setting (lossless); quality applies to JPG only.
- Multiple editors may be open at once. An editor open at capture time appears in the screenshot like any other window.
- A new language requires a rebuild (.resx). Acceptable for the author and team; if translators ever need to add languages without a rebuild, switch to runtime-loaded files.
- Capture via GDI `BitBlt`. The author confirmed nobody uses HDR monitors. Known limits: washed-out colors on HDR, and some protected or hardware-accelerated windows may come out black. Moving to Windows.Graphics.Capture or DXGI later stays inside `IScreenCapturer`.

## Left to the builder

- MVVM library (CommunityToolkit.Mvvm recommended).
- How to open the tray menu on left click (Avalonia's `TrayIcon` opens its menu on right click on Windows, so a custom popup window or a native call may be needed).
- Icons, toolbar look, exact color palette.
- Blur algorithm (gaussian or pixelation).
- Whether to confirm closing an editor with unsaved changes.
- Logging and unhandled-exception handling (no silent crashes).

## Addendum (2026-11-18, author request during implementation)

- The editor area around the screenshot is a dark checkerboard (not a flat colour), and the image has a two-tone (white/black) 1-px outline, so the boundary of the captured image is always obvious — including for white screenshots. Neither is part of the exported image.
- Annotations cannot be drawn outside the image: points are clamped to the image edges, and an annotation cannot be dragged entirely off the image.
- The overlay selection border is two-tone dashed (black/white) so it is visible on any background.
