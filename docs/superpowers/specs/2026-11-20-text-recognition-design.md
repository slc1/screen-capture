# Screen Capture — Text Recognition (OCR) Design

Date: 2026-11-20
Extends: `2026-11-18-screen-capture-design.md` (v0.1.1)

## Why

The team often needs the text shown in a screenshot: an error message, a log line, a value in a UI that cannot be selected. Retyping it is slow and error-prone.

Success: select the text on screen and, within a second, have it both in the clipboard and open in a text editor.

## Goals

1. **Editor tool "Recognize text"** in the editor toolbar. The user drags a rectangle over the screenshot; on release the text in that rectangle is recognized. A click without a drag (rectangle under 3 px) recognizes the whole screenshot. The rectangle is shown while dragging (two-tone dashed) and is not an annotation: it is not added to the document or the undo history.
2. **Tray item "Recognize text in area"** with its own global hotkey (default **Ctrl+Alt+7**, reassignable in Settings like the other two). It uses the same frozen, dimmed selection as Capture area; on release it recognizes the selection directly, without opening the editor.
3. **Output**, for both entry points:
   - the text is put on the clipboard as Unicode text;
   - the text is written to a UTF-8 `.txt` file and opened with the default app for `.txt` (Notepad by default);
   - if no text is found, the user sees "No text found" and nothing is written or copied.
4. Recognition reads the **original pixels**, never the annotations, so arrows and marker strokes do not damage letters.
5. Languages: **Ukrainian and English together** in one pass (mixed text is common for the team).
6. Works offline; nothing leaves the machine.

## Non-goals

- Choosing other recognition languages in Settings.
- Keeping layout (tables, columns); the output is plain lines of text.
- Showing the recognized text inside the app, or editing it before it is opened.
- Recognizing handwriting.

## Decided implementation

### Engine

**Tesseract 5.5** via the `TesseractOCR` NuGet wrapper (5.5.2, Apache-2.0) with the `tessdata_fast` models `ukr` and `eng` (Apache-2.0), LSTM engine, languages `ukr+eng`.

Measured on rendered Segoe UI text (12–16 px, light and dark backgrounds, mixed Ukrainian/English): 98–99 % character accuracy, 80–170 ms per call. `tessdata_best` was not more accurate on this input and was 3× slower and 3× larger, so `fast` is used. Known weak spots: `ґ` is read as `г`; a `.` between letters is sometimes read as `,`.

Rejected: Windows.Media.Ocr (no Ukrainian recognizer; the Russian one mangles `і ї є ґ`); cloud OCR (screenshots would leave the machine, needs a key, costs money).

### Preprocessing (Core, unit-tested)

Before Tesseract: convert to 8-bit grayscale; invert when the mean luminance is below 50 % (light text on a dark theme); upscale ×2 with high-quality filtering (×1 when the doubled image would exceed 8000 px on a side); add a 10 px white margin. Tesseract reads small screen fonts and dark themes poorly without these steps.

### Packaging

The app stays a single exe. The native `tesseract55.dll` and `leptonica-1.85.0.dll` (x64) and the two `.traineddata` files are embedded resources of `ScreenCapture.Platform.Windows`. On the first recognition they are extracted to `%LOCALAPPDATA%\ScreenCapture\ocr\tesseract-5.5.2\` (`x64\` and `tessdata\`), and re-extracted if a file is missing or has the wrong size. The `.traineddata` files are committed to the repo (7.5 MB) so builds work offline.

The native DLLs need the **Microsoft Visual C++ 2015–2022 runtime (x64)**. It is present on almost every Windows 10/11 machine, but not guaranteed. Before loading Tesseract the service checks that `vcruntime140.dll`, `vcruntime140_1.dll` and `msvcp140.dll` load; if not, the user sees a message with the download link (https://aka.ms/vs/17/release/vc_redist.x64.exe) instead of a crash.

The exe grows by about 15 MB.

### Code layout

- `ScreenCapture.Core/Ocr/`: `OcrImagePrep` (preprocessing), `OcrText.Clean` (trim lines, collapse runs of blank lines, CRLF line ends), `OcrTextFiles` (file name `Text yyyy-MM-dd HH-mm-ss.txt`, write UTF-8 with BOM, delete files older than one day).
- `ScreenCapture.Platform`: `IOcrService.Recognize(SKBitmap) -> string` (throws `OcrUnavailableException` when the engine cannot load); `IClipboardTextService.TrySetText(string)`.
- `ScreenCapture.Platform.Windows`: `TesseractOcrService`, `Win32ClipboardTextService` (`CF_UNICODETEXT`).
- `ScreenCapture.App`: `TextRecognizer` runs recognition off the UI thread, then copies, writes and opens the file, and reports problems through `UserNotifier`. `EditorTool.Recognize` in `EditorSession`; the editor shows a wait cursor while recognizing. `CaptureCoordinator.StartRecognizeArea` reuses the area session. Third hotkey `HotkeyId.RecognizeArea` in `HotkeyManager`, Settings and both tray menus.
- Text files go to `%TEMP%\ScreenCapture\ocr\`; files older than a day are removed on each recognition.

### Settings compatibility

`settings.json` from v0.1.x has no `RecognizeHotkey`. A missing key gets the default (Ctrl+Alt+7) **without** the "settings file was damaged" warning, because it is an upgrade, not damage. An explicit `null` still means "no hotkey".

### Failure handling

- Engine cannot load (missing VC++ runtime, extraction failed): message, details in the log.
- Clipboard write fails: the file is still written and opened; the user is told the copy failed.
- Opening the file fails: the text is still on the clipboard; the message gives the file path.

### Licensing

`THIRD-PARTY-NOTICES.md` lists Tesseract (Apache-2.0), Leptonica (BSD-2-Clause), the TesseractOCR wrapper (Apache-2.0) and tessdata_fast (Apache-2.0); `publish.ps1` puts it in the zip.

## Calls I made (the author may change them)

- `tessdata_fast` over `tessdata_best` (measurements above).
- Default hotkey Ctrl+Alt+7, next to the existing 5 and 6.
- A click without a drag recognizes the whole screenshot.
- The recognized text file is temporary (deleted after a day); the clipboard is the durable copy.
- No new Settings options (language is fixed to `ukr+eng`).
