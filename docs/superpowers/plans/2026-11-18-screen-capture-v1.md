# ScreenCapture v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Windows tray screenshot tool: area / full-screen capture, an annotation editor with undo/redo, copy to clipboard or save as PNG/JPG, EN/UK UI, reassignable global hotkeys.

**Architecture:** OS-independent logic (annotation model, undo/redo, rendering, export, settings, localization, geometry) lives in `ScreenCapture.Core` and is unit-tested. OS services sit behind interfaces in `ScreenCapture.Platform`, implemented with Win32 P/Invoke in `ScreenCapture.Platform.Windows`. `ScreenCapture.App` (Avalonia) is a thin UI over Core: tray, overlays, editor, settings. One renderer in Core (SkiaSharp) draws both the editor preview and the exported image, so what you see is what you copy.

**Tech Stack:** .NET 10 (SDK 10.0.404), Avalonia 11.x latest stable, SkiaSharp (the version Avalonia.Skia pulls in), CommunityToolkit.Mvvm 8.x, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-18-screen-capture-design.md`

## Global Constraints

- Windows 10 1809+ / Windows 11, x64. Platform-specific projects target `net10.0-windows10.0.17763.0`; Core and Platform target `net10.0`.
- Capture is always at physical pixels 1:1; process is Per-Monitor DPI aware v2.
- Hotkey press → dimmed overlay visible in under ~200 ms.
- No user-facing string literals in code or XAML; everything goes through `Strings.resx` / `Strings.uk.resx`.
- Default hotkeys: **Ctrl+Alt+5** (area), **Ctrl+Alt+6** (full screen). Never PrintScreen by default.
- Defaults: format PNG, JPG quality 90, folder `<Pictures>\Screenshots`, autostart off.
- File name: `Screenshot yyyy-MM-dd HH-mm-ss.<ext>`.
- Settings file: `%APPDATA%\ScreenCapture\settings.json`; log: `%APPDATA%\ScreenCapture\logs\app.log`.
- Copy closes the editor on success; on failure the editor stays open with an error. Save leaves the editor open.
- Failures are never silent: show a message and log.
- All docs and code comments in English.
- `dotnet` may not be on the bash PATH; if `dotnet` is not found, use `"C:\Program Files\dotnet\dotnet.exe"`.

## Review Focus

1. **Click without dragging on the overlay** (or a 1–2 px drag) → ignored; overlay stays so the user can try again. Test: `SelectionMathTests.TooSmallSelection_IsRejected` (Task 5).
2. **Hotkey pressed again while the overlay is open** (or area + full-screen hotkeys back to back) → the second request is ignored, no second overlay set. Test: `CaptureGateTests.SecondEnter_IsRejectedUntilExit` (Task 5).
3. **Selection dragged right-to-left / bottom-to-top, or the pointer leaves the monitor edge** → normalized and clamped to the monitor image. Test: `SelectionMathTests.ReversedDrag_IsNormalized`, `SelectionMathTests.OutOfBounds_IsClamped` (Task 5).
4. **Save folder from settings no longer exists** → it is created; if the path is unwritable, the user sees an error and the editor stays open. Test: `ImageExporterTests.Save_CreatesMissingDirectory` (Task 4).
5. **Corrupt `settings.json`** → app starts with defaults and tells the user. Test: `SettingsStoreTests.Load_CorruptFile_ReturnsDefaultsAndFlag` (Task 6).

---

## File Structure

```
ScreenCapture.sln
Directory.Build.props                 // Nullable, ImplicitUsings, LangVersion
.gitignore
README.md
src/ScreenCapture.Core/
  Annotations/Annotation.cs           // records + hit-test + move
  Annotations/AnnotationStyle.cs      // StrokeWidth, AnnotationPalette
  Annotations/AnnotationDocument.cs   // list + undo/redo
  Annotations/DocumentCommands.cs     // Add/Remove/Replace commands
  Editing/EditorSession.cs            // tool interaction in image coords
  Rendering/AnnotationRenderer.cs     // flatten image + annotations
  Export/ImageExporter.cs             // encode/save, default file name
  Geometry/SelectionMath.cs
  Geometry/ViewportMath.cs
  Capture/CaptureGate.cs
  Hotkeys/Hotkey.cs                   // Hotkey, HotkeyModifiers, HotkeyRules
  Settings/AppSettings.cs
  Settings/SettingsStore.cs
  Localization/Strings.resx, Strings.uk.resx
  Localization/Loc.cs                 // runtime-switchable localizer
  Diagnostics/FileLog.cs
src/ScreenCapture.Platform/
  IScreenCapturer.cs, IGlobalHotkeyService.cs, IClipboardImageService.cs, IAutostartService.cs
src/ScreenCapture.Platform.Windows/
  NativeMethods.cs, GdiScreenCapturer.cs, Win32HotkeyService.cs,
  Win32ClipboardImageService.cs, RegistryAutostartService.cs
src/ScreenCapture.App/
  Program.cs, App.axaml(.cs), app.manifest, Assets/tray.ico
  Services/AppServices.cs             // composition root
  Services/HotkeyManager.cs, Services/UserNotifier.cs
  Tray/TrayController.cs, Tray/TrayMenuWindow.axaml(.cs)
  Capture/CaptureCoordinator.cs, Capture/OverlayWindow.axaml(.cs)
  Editor/EditorWindow.axaml(.cs), Editor/EditorViewModel.cs,
  Editor/AnnotationCanvas.cs, Editor/JpegQualityDialog.axaml(.cs)
  Settings/SettingsWindow.axaml(.cs), Settings/SettingsViewModel.cs,
  Settings/HotkeyBox.cs
  Common/MessageWindow.axaml(.cs)
tests/ScreenCapture.Core.Tests/
tests/ScreenCapture.Platform.Windows.Tests/
publish.ps1
```

---

### Task 1: Repository and solution scaffold

**Files:**
- Create: `.gitignore`, `Directory.Build.props`, `ScreenCapture.sln`, the six project files listed above, `tests/ScreenCapture.Core.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: buildable solution; project references App → Core, Platform, Platform.Windows; Platform.Windows → Platform → Core; tests → their targets.

- [ ] **Step 1:** `git init` in `C:\my_projects\screen_capture`. `.gitignore`: standard Visual Studio/.NET ignores (`bin/`, `obj/`, `*.user`, `publish/`, `artifacts/`) plus Rider: ignore `.idea/**/workspace.xml`, `.idea/**/tasks.xml`, `.idea/**/usage.statistics.xml`, `.idea/**/shelf/`, `.idea/**/contentModel.xml`, `.idea/**/indexLayout.xml`, `.idea/**/projectSettingsUpdater.xml`, `.idea/**/.idea.*/.idea/` caches. Do not delete the existing `.idea` folder.
- [ ] **Step 2:** `dotnet new install Avalonia.Templates`; create projects: `classlib` for Core and Platform (`net10.0`), Platform.Windows (`net10.0-windows10.0.17763.0`, `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`), `avalonia.mvvm` app for App (retarget to `net10.0-windows10.0.17763.0`, `<OutputType>WinExe</OutputType>`), `xunit` for both test projects (Windows tests target `net10.0-windows10.0.17763.0`). Add all to `ScreenCapture.sln`.
- [ ] **Step 3:** `Directory.Build.props`: `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=latest`. Packages: App → Avalonia, Avalonia.Desktop, Avalonia.Themes.Fluent, Avalonia.Fonts.Inter, CommunityToolkit.Mvvm; Core → SkiaSharp at the same version Avalonia.Skia depends on (check with `dotnet list src/ScreenCapture.App package --include-transitive`) so there is one SkiaSharp in the app.
- [ ] **Step 4:** Add `SmokeTests.Solution_Builds` (`Assert.True(true)`), run `dotnet build ScreenCapture.sln` and `dotnet test`. Expected: build succeeds, 1 test passes.
- [ ] **Step 5:** Commit `chore: scaffold solution`.

---

### Task 2: Annotation model

**Files:**
- Create: `src/ScreenCapture.Core/Annotations/Annotation.cs`, `Annotations/AnnotationStyle.cs`
- Test: `tests/ScreenCapture.Core.Tests/AnnotationTests.cs`

**Interfaces:**
- Produces:
  - `enum StrokeWidth { Thin = 2, Medium = 4, Thick = 8 }` (pixels in image space)
  - `static class AnnotationPalette { IReadOnlyList<SKColor> Colors; SKColor Default; }` — Colors in order: `#E53935, #FB8C00, #FDD835, #43A047, #1E88E5, #8E24AA, #000000, #FFFFFF`; Default `#E53935`.
  - `abstract record Annotation(Guid Id, SKColor Color, StrokeWidth Width)` with `abstract SKRect Bounds { get; }`, `abstract Annotation MoveBy(float dx, float dy)`, `abstract bool HitTest(SKPoint p, float tolerance)`.
  - `sealed record ArrowAnnotation(..., SKPoint Start, SKPoint End)`, `LineAnnotation(..., SKPoint Start, SKPoint End)`, `RectangleAnnotation(..., SKRect Rect)`, `MarkerAnnotation(..., SKPoint Start, SKPoint End)` (straight highlighter stroke), `TextAnnotation(..., SKPoint Position, string Text, float FontSize)`, `BlurAnnotation(..., SKRect Rect)`.
  - Rects are always normalized (`Left<=Right`, `Top<=Bottom`) — constructors/factories normalize.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Line_HitTest_NearSegment() {
    var a = new LineAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new(0,0), new(100,0));
    Assert.True(a.HitTest(new(50, 3), 4));
    Assert.False(a.HitTest(new(50, 10), 4));
    Assert.False(a.HitTest(new(110, 0), 4)); }
[Fact] public void Rectangle_HitTest_OnlyOnBorder() {
    var r = new RectangleAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new SKRect(10,10,110,60));
    Assert.True(r.HitTest(new(10, 30), 4));
    Assert.False(r.HitTest(new(60, 35), 4)); }
[Fact] public void Blur_HitTest_Inside() {
    var b = new BlurAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new SKRect(10,10,110,60));
    Assert.True(b.HitTest(new(60, 35), 4)); }
[Fact] public void MoveBy_KeepsIdAndShiftsGeometry() {
    var a = new ArrowAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new(0,0), new(10,10));
    var m = (ArrowAnnotation)a.MoveBy(5, -2);
    Assert.Equal(a.Id, m.Id); Assert.Equal(new SKPoint(5,-2), m.Start); Assert.Equal(new SKPoint(15,8), m.End); }
[Fact] public void Rectangle_FromReversedCorners_IsNormalized() {
    var r = RectangleAnnotation.FromCorners(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new(100,80), new(10,20));
    Assert.Equal(new SKRect(10,20,100,80), r.Rect); }
[Fact] public void Text_HitTest_InsideEstimatedBounds() {
    var t = new TextAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new(10,10), "Hello", 20);
    Assert.True(t.HitTest(new(15, 20), 4)); Assert.False(t.HitTest(new(500, 500), 4)); }
```

- [ ] **Step 2:** `dotnet test --filter AnnotationTests` → FAIL (types missing).
- [ ] **Step 3:** Implement. Hit tolerance for strokes = `tolerance + (int)Width / 2f`. Line/Arrow/Marker use point-to-segment distance; Rectangle tests distance to its four edges; Blur and Text test bounds containment (inflated by tolerance). Text bounds: measure with `SKFont(SKTypeface.Default, FontSize).MeasureText(Text)` width and `FontSize * 1.25f` height, top-left at `Position`. Add `static FromCorners(...)` on Rectangle and Blur.
- [ ] **Step 4:** `dotnet test --filter AnnotationTests` → PASS.
- [ ] **Step 5:** Commit `feat: annotation model with hit-testing`.

---

### Task 3: Annotation document with undo/redo

**Files:**
- Create: `src/ScreenCapture.Core/Annotations/AnnotationDocument.cs`, `Annotations/DocumentCommands.cs`
- Test: `tests/ScreenCapture.Core.Tests/AnnotationDocumentTests.cs`

**Interfaces:**
- Consumes: `Annotation` (Task 2).
- Produces:
  - `interface IDocumentCommand { void Apply(List<Annotation> items); void Revert(List<Annotation> items); }`
  - `sealed class AddAnnotationCommand(Annotation annotation)`, `RemoveAnnotationCommand(Guid id)` (remembers index on Apply, reinserts there on Revert), `ReplaceAnnotationCommand(Annotation before, Annotation after)` (same Id; used for move and text edits).
  - `sealed class AnnotationDocument { IReadOnlyList<Annotation> Annotations; bool CanUndo; bool CanRedo; void Execute(IDocumentCommand c); void Undo(); void Redo(); Annotation? HitTest(SKPoint p, float tolerance); event EventHandler? Changed; }` — `HitTest` returns the topmost (last) match. `Execute` clears the redo stack. `Undo`/`Redo` are no-ops when not possible.

- [ ] **Step 1: Write failing tests:** `Execute_Add_ThenUndo_RemovesIt`, `Undo_ThenRedo_RestoresIt`, `Execute_AfterUndo_ClearsRedo` (`Assert.False(doc.CanRedo)`), `Remove_Undo_RestoresAtOriginalIndex` (three items, remove middle, undo → `Annotations[1].Id == middle.Id`), `Replace_Undo_RestoresBefore`, `HitTest_ReturnsTopmost` (two overlapping rectangles → second), `Changed_FiresOnExecuteUndoRedo` (counter == 3), `Undo_WhenEmpty_DoesNothing`.
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement with two `Stack<IDocumentCommand>`.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: annotation document with undo/redo`.

---

### Task 4: Rendering and export

**Files:**
- Create: `src/ScreenCapture.Core/Rendering/AnnotationRenderer.cs`, `Export/ImageExporter.cs`
- Test: `tests/ScreenCapture.Core.Tests/AnnotationRendererTests.cs`, `ImageExporterTests.cs`

**Interfaces:**
- Consumes: annotations (Task 2).
- Produces:
  - `static class AnnotationRenderer { SKBitmap Flatten(SKBitmap original, IEnumerable<Annotation> annotations); void Draw(SKCanvas canvas, SKBitmap original, IEnumerable<Annotation> annotations); }` — `Flatten` returns a new bitmap of the original's size; `Draw` draws original + annotations in list order (used by the editor preview too).
  - `enum ImageFormatKind { Png, Jpg }`
  - `static class ImageExporter { byte[] Encode(SKBitmap image, ImageFormatKind format, int jpgQuality); void Save(SKBitmap image, string path, ImageFormatKind format, int jpgQuality); string DefaultFileName(DateTime now, ImageFormatKind format); ImageFormatKind FormatFromPath(string path); }`

Rendering rules:
- Line: round caps, width `(int)Width`. Arrow: line plus a filled triangular head at `End`, head length `max(10, 4 * width)`, head half-angle 25°. Rectangle: stroke only. Marker: stroke width `(int)Width * 4`, color alpha 102 (40 %), `SKBlendMode.Multiply`, square caps. Text: `SKFont` size `FontSize`, fill with color, baseline at `Position.Y + FontSize`. Blur: pixelate — sample the **original** bitmap inside `Rect` (clamped to image), average blocks of 12×12 px, fill each block with its average.
- Encode: PNG via `SKEncodedImageFormat.Png` (quality ignored). JPG: quality clamped to 1..100; image is first drawn onto an opaque white background.
- `Save` creates the parent directory if missing; IO exceptions propagate to the caller.
- `DefaultFileName(new DateTime(2026,11,18,21,4,5), Png)` → `"Screenshot 2026-11-18 21-04-05.png"`; Jpg → `.jpg`. `FormatFromPath` maps `.jpg`/`.jpeg` (any case) → Jpg, everything else → Png.

- [ ] **Step 1: Write failing tests:**
  - `Flatten_NoAnnotations_EqualsOriginalPixels` (compare a few pixels of a 50×50 solid blue bitmap).
  - `Flatten_Rectangle_PaintsBorderNotInside` (red rect 10..40, check pixel (10,25) is red-ish and (25,25) is still blue).
  - `Flatten_Blur_ReplacesBlockWithAverage` (left half black, right half white within one 12-px block → pixel in block is gray, R between 60 and 200).
  - `Flatten_Blur_ReadsOriginalNotOtherAnnotations` (a red rectangle drawn under a later blur: blurred block contains no red — R≈G≈B).
  - `Flatten_Marker_IsTranslucent` (white bg, yellow marker → pixel is not fully saturated yellow nor white).
  - `Encode_Png_RoundTripsSize`, `Encode_Jpg_LowerQualityIsSmaller` (noise image, q=20 bytes < q=95 bytes), `Encode_Jpg_ClampsQuality` (q=0 and q=500 don't throw).
  - `DefaultFileName_MatchesPattern`, `FormatFromPath_JpegVariants`.
  - `Save_CreatesMissingDirectory` (temp dir + `sub/dir/x.png` → file exists).
- [ ] **Step 2:** Run → FAIL.
- [ ] **Step 3:** Implement per rules above.
- [ ] **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: annotation renderer and image export`.

---

### Task 5: Geometry helpers and capture gate

**Files:**
- Create: `src/ScreenCapture.Core/Geometry/SelectionMath.cs`, `Geometry/ViewportMath.cs`, `Capture/CaptureGate.cs`
- Test: `SelectionMathTests.cs`, `ViewportMathTests.cs`, `CaptureGateTests.cs`

**Interfaces:**
- Produces:
  - `static class SelectionMath { const int MinSizePx = 3; SKRectI ToPixelRect(SKPoint startDip, SKPoint endDip, double scale, SKSizeI imageSize); bool IsTooSmall(SKRectI rect); }` — multiply DIP by scale, normalize, round outward (floor left/top, ceiling right/bottom), clamp to `[0, imageSize]`. `IsTooSmall` when width or height `< MinSizePx`.
  - `readonly record struct Viewport(float Scale, SKPoint Offset)`; `static class ViewportMath { Viewport Fit(SKSizeI image, SKSize available); SKPoint ToImage(Viewport v, SKPoint viewPoint); SKPoint ToView(Viewport v, SKPoint imagePoint); }` — `Fit` never upscales (scale ≤ 1) and centers.
  - `sealed class CaptureGate { bool TryEnter(); void Exit(); bool IsBusy; }` — thread-safe (`Interlocked`).

- [ ] **Step 1: Write failing tests:**
  - `ToPixelRect_AppliesScale` (start (10,10), end (20,30), scale 1.5 → `SKRectI(15,15,30,45)`).
  - `ReversedDrag_IsNormalized` (start (20,30), end (10,10), scale 1 → `(10,10,20,30)`).
  - `OutOfBounds_IsClamped` (end (5000,-50), image 1920×1080 → right 1920, top 0).
  - `TooSmallSelection_IsRejected` (`IsTooSmall(new SKRectI(5,5,7,50))` true; 3×3 false).
  - `Fit_SmallImage_NotUpscaled` (100×100 in 800×600 → scale 1, offset (350,250)).
  - `Fit_LargeImage_ScaledDown` (3840×2160 in 1920×1080 → scale 0.5).
  - `ToImage_RoundTripsToView`.
  - `SecondEnter_IsRejectedUntilExit` (TryEnter true, TryEnter false, Exit, TryEnter true).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: selection/viewport math and capture gate`.

---

### Task 6: Hotkey model, settings and store, file log

**Files:**
- Create: `src/ScreenCapture.Core/Hotkeys/Hotkey.cs`, `Settings/AppSettings.cs`, `Settings/SettingsStore.cs`, `Diagnostics/FileLog.cs`
- Test: `HotkeyTests.cs`, `SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `[Flags] enum HotkeyModifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }`
  - `sealed record Hotkey(HotkeyModifiers Modifiers, string Key)` — `Key` is an Avalonia `Key` enum name (`"D5"`, `"F9"`, `"PrintScreen"`, `"A"`). `ToString()` → display text: modifiers in order Ctrl, Alt, Shift, Win joined with `+`, then key display (`"D5"`→`"5"`, others as-is): `"Ctrl+Alt+5"`.
  - `enum HotkeyId { CaptureArea, CaptureFullScreen }`
  - `static class HotkeyRules { Hotkey DefaultFor(HotkeyId id); bool IsValid(Hotkey h); }` — defaults Ctrl+Alt+`D5` / Ctrl+Alt+`D6`. Valid when it has a modifier, or the key is `F1`..`F24` or `PrintScreen`. Modifier-only keys (`LeftCtrl`, `RightAlt`, `LWin`, …) are never valid as `Key`.
  - `sealed record AppSettings { string Language; Hotkey? AreaHotkey; Hotkey? FullScreenHotkey; ImageFormatKind DefaultFormat; int JpgQuality; string SaveFolder; bool StartWithWindows; static AppSettings CreateDefault(string picturesFolder, string language); }` — null hotkey = cleared.
  - `sealed record SettingsLoadResult(AppSettings Settings, bool WasCorrupt)`
  - `sealed class SettingsStore(string path, Func<AppSettings> defaults) { SettingsLoadResult Load(); void Save(AppSettings s); }` — missing file → defaults, `WasCorrupt=false`; unparsable JSON → defaults, `WasCorrupt=true`; out-of-range `JpgQuality` clamped to 1..100. `Save` writes to `path + ".tmp"` then `File.Move(..., overwrite: true)`, creating the directory. System.Text.Json, enums as strings, indented.
  - `static class FileLog { void Init(string path); void Info(string message); void Error(string message, Exception? ex = null); }` — append with timestamp, never throws.

- [ ] **Step 1: Write failing tests:**
  - `Default_AreaHotkey_IsCtrlAlt5` (`ToString() == "Ctrl+Alt+5"`).
  - `IsValid_RequiresModifier_ExceptFunctionKeysAndPrintScreen` (`A` no-mod false; `F9` true; `PrintScreen` true; Shift+`A` true; Ctrl+`LeftCtrl` false).
  - `Load_MissingFile_ReturnsDefaults`.
  - `SaveThenLoad_RoundTrips` (all fields, including a null `FullScreenHotkey`).
  - `Load_CorruptFile_ReturnsDefaultsAndFlag` (write `"{ not json"` → `WasCorrupt` true, settings == defaults).
  - `Load_ClampsJpgQuality` (file with 250 → 100).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: hotkey model, settings store, file log`.

---

### Task 7: Localization

**Files:**
- Create: `src/ScreenCapture.Core/Localization/Strings.resx`, `Strings.uk.resx`, `Loc.cs`
- Test: `LocalizationTests.cs`

**Interfaces:**
- Produces:
  - `sealed class Loc : INotifyPropertyChanged { static Loc Instance; string this[string key]; string Language; void SetLanguage(string code); static IReadOnlyList<LanguageOption> AvailableLanguages(); static string ResolveInitialLanguage(string osUiCulture, IReadOnlyList<LanguageOption> available); string Format(string key, params object[] args); }` — indexer reads `Strings.ResourceManager.GetString(key, culture)`, falling back to the key itself if missing. `SetLanguage` raises `PropertyChanged("Item[]")` so XAML bindings `{Binding [Key], Source={x:Static loc:Loc.Instance}}` refresh live.
  - `sealed record LanguageOption(string Code, string DisplayName)` — DisplayName = culture `NativeName` with first letter upper-cased ("English", "Українська").
  - `AvailableLanguages()` = `"en"` plus every neutral culture for which `ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false)` is non-null — no hand-kept list.
  - `ResolveInitialLanguage("uk-UA", ...)` → `"uk"`; `"de-DE"` → `"en"`.

Keys (exact values):

| Key | en | uk |
|---|---|---|
| App_Name | ScreenCapture | ScreenCapture |
| Tray_CaptureArea | Capture area | Захопити область |
| Tray_CaptureFullScreen | Capture full screen | Захопити весь екран |
| Tray_Settings | Settings | Налаштування |
| Tray_Exit | Exit | Вихід |
| Overlay_Hint | Drag to select an area. Esc to cancel. | Виділіть область мишкою. Esc — скасувати. |
| Editor_Title | ScreenCapture — Editor | ScreenCapture — Редактор |
| Tool_Select | Select | Вибір |
| Tool_Arrow | Arrow | Стрілка |
| Tool_Line | Line | Лінія |
| Tool_Rectangle | Rectangle | Рамка |
| Tool_Marker | Marker | Маркер |
| Tool_Text | Text | Текст |
| Tool_Blur | Blur | Розмиття |
| Stroke_Thin | Thin | Тонка |
| Stroke_Medium | Medium | Середня |
| Stroke_Thick | Thick | Товста |
| Editor_Undo | Undo | Скасувати дію |
| Editor_Redo | Redo | Повторити |
| Editor_Copy | Copy | Копіювати |
| Editor_Save | Save | Зберегти |
| Save_JpgQualityTitle | JPG quality | Якість JPG |
| Common_OK | OK | OK |
| Common_Cancel | Cancel | Скасувати |
| Common_Browse | Browse… | Огляд… |
| Settings_Title | Settings | Налаштування |
| Settings_Language | Language | Мова |
| Settings_HotkeyArea | Capture area | Захопити область |
| Settings_HotkeyFullScreen | Capture full screen | Захопити весь екран |
| Settings_HotkeyPrompt | Press a key combination… | Натисніть комбінацію клавіш… |
| Settings_HotkeyNone | None | Немає |
| Settings_HotkeyReset | Reset | Скинути |
| Settings_HotkeyClear | Clear | Очистити |
| Settings_DefaultFormat | Default format | Формат за замовчуванням |
| Settings_JpgQuality | JPG quality | Якість JPG |
| Settings_SaveFolder | Save folder | Папка для збереження |
| Settings_StartWithWindows | Start with Windows | Запускати разом з Windows |
| Error_ClipboardFailed | Could not copy the image to the clipboard. Try again. | Не вдалося скопіювати зображення в буфер обміну. Спробуйте ще раз. |
| Error_SaveFailed | Could not save the file: {0} | Не вдалося зберегти файл: {0} |
| Error_CaptureFailed | Could not capture the screen. | Не вдалося зробити знімок екрана. |
| Error_HotkeyTaken | The combination {0} is already used by another application. | Комбінація {0} уже використовується іншою програмою. |
| Error_HotkeyDuplicate | The combination {0} is already assigned to the other capture mode. | Комбінація {0} уже призначена іншому режиму захоплення. |
| Error_HotkeyInvalid | Add Ctrl, Alt, Shift or Win to this key. | Додайте до цієї клавіші Ctrl, Alt, Shift або Win. |
| Error_SettingsCorrupt | The settings file was damaged. Default settings are used. | Файл налаштувань пошкоджено. Використано налаштування за замовчуванням. |
| Error_AutostartFailed | Could not change the start-with-Windows setting. | Не вдалося змінити автозапуск разом з Windows. |
| Error_Unexpected | Unexpected error. Details were written to the log: {0} | Неочікувана помилка. Деталі записано в журнал: {0} |

- [ ] **Step 1: Write failing tests:**
  - `EveryKey_ExistsInEveryLanguage` — for each `AvailableLanguages()` code, the resource set (tryParents false) contains every key of the neutral set and no value is empty.
  - `AvailableLanguages_ContainsEnglishAndUkrainian`.
  - `ResolveInitialLanguage_UkrainianOs_ReturnsUk`, `ResolveInitialLanguage_UnsupportedOs_ReturnsEn`.
  - `SetLanguage_ChangesIndexerAndRaisesItemChanged` (`Loc.Instance["Tray_Exit"]` == "Вихід" after `SetLanguage("uk")`; event name `"Item[]"`).
  - `Format_SubstitutesArgs` (`Format("Error_SaveFailed", "x")` contains "x").
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement (resx with `PublicResXFileCodeGenerator`, namespace `ScreenCapture.Core.Localization`). **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: runtime-switchable EN/UK localization`.

---

### Task 8: Platform interfaces and Windows screen capture

**Files:**
- Create: `src/ScreenCapture.Platform/IScreenCapturer.cs` (+ the three other interfaces), `src/ScreenCapture.Platform.Windows/NativeMethods.cs`, `GdiScreenCapturer.cs`
- Test: `tests/ScreenCapture.Platform.Windows.Tests/GdiScreenCapturerTests.cs`

**Interfaces:**
- Produces (in `ScreenCapture.Platform`):
  - `sealed record MonitorInfo(string DeviceName, SKRectI BoundsPx, double Scale, bool IsPrimary)`
  - `sealed record ScreenSnapshot(MonitorInfo Monitor, SKBitmap Image)` — `Image` size == `BoundsPx` size, opaque.
  - `interface IScreenCapturer { IReadOnlyList<MonitorInfo> GetMonitors(); IReadOnlyList<ScreenSnapshot> CaptureAllMonitors(); ScreenSnapshot CaptureMonitorAtCursor(); }`
  - `interface IGlobalHotkeyService : IDisposable { HotkeyRegistrationResult Register(HotkeyId id, Hotkey hotkey); void Unregister(HotkeyId id); event EventHandler<HotkeyId>? Pressed; }`, `enum HotkeyRegistrationResult { Ok, TakenByOtherApp, Invalid }`
  - `interface IClipboardImageService { bool TrySetImage(SKBitmap image); }`
  - `interface IAutostartService { bool IsEnabled(); void SetEnabled(bool enabled); }`
- Produces (Windows): `sealed class GdiScreenCapturer : IScreenCapturer`.

Approach: `EnumDisplayMonitors` + `GetMonitorInfoW` (`rcMonitor`, `szDevice`, `MONITORINFOF_PRIMARY`) + `GetDpiForMonitor(MDT_EFFECTIVE_DPI)` → `Scale = dpiX / 96.0`. Per monitor: `GetDC(IntPtr.Zero)`, `CreateCompatibleDC`, `CreateDIBSection` 32-bpp top-down, `BitBlt(..., SRCCOPY | CAPTUREBLT)`, copy bits into an `SKBitmap` (`Bgra8888`, `Opaque`) and force alpha 255. Release every GDI handle in `finally`. `CaptureMonitorAtCursor` uses `GetCursorPos` + `MonitorFromPoint(MONITOR_DEFAULTTONEAREST)`. All `LibraryImport` declarations go in `NativeMethods.cs`. Tests must run in a DPI-aware process: the test calls `SetProcessDpiAwarenessContext(-4)` (PMv2) once in a fixture before capturing.

- [ ] **Step 1: Write failing tests:** `GetMonitors_ReturnsAtLeastOnePrimary`, `CaptureAll_ImageSizesMatchPhysicalBounds`, `CaptureAll_ImagesAreOpaque` (sample pixels alpha 255), `CaptureMonitorAtCursor_ReturnsAMonitorFromList`.
- [ ] **Step 2:** Run `dotnet test tests/ScreenCapture.Platform.Windows.Tests` → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: platform interfaces and GDI screen capture`.

---

### Task 9: Windows clipboard, hotkeys, autostart

**Files:**
- Create: `src/ScreenCapture.Platform.Windows/Win32ClipboardImageService.cs`, `Win32HotkeyService.cs`, `RegistryAutostartService.cs`
- Test: `Win32ClipboardImageServiceTests.cs`, `Win32HotkeyServiceTests.cs`, `RegistryAutostartServiceTests.cs`

**Interfaces:**
- Consumes: interfaces from Task 8; `Hotkey`, `HotkeyId`, `HotkeyRules` from Task 6.
- Produces:
  - `Win32ClipboardImageService` — `OpenClipboard` retried up to 10 times with 50 ms between attempts; `EmptyClipboard`; puts **both** `CF_DIB` (BITMAPINFOHEADER, `BI_RGB`, bottom-up, 32-bpp, opaque pixels) and the registered `"PNG"` format (PNG bytes from `ImageExporter.Encode`); returns false (never throws) on any failure.
  - `Win32HotkeyService(Action<Action> postToUiThread)` — creates a message-only window (`HWND_MESSAGE`) on the calling thread; `RegisterHotKey` with `MOD_NOREPEAT` + mapped modifiers; maps `Hotkey.Key` (Avalonia key names) to virtual keys (`D0..D9` → `0x30..0x39`, `A..Z`, `F1..F24`, `PrintScreen` → `VK_SNAPSHOT`, `NumPad0..9`, `OemMinus`, `OemPlus`, etc.); unmappable key → `Invalid`; `RegisterHotKey` failure → `TakenByOtherApp`. `Register` for an id that already has a hotkey unregisters the old one first. `WM_HOTKEY` raises `Pressed` via `postToUiThread`.
  - `RegistryAutostartService(string valueName, string executablePath)` — `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value = `"\"<exe>\""`. `IsEnabled` true only if the value exists **and** points at `executablePath` (case-insensitive).

- [ ] **Step 1: Write failing tests:**
  - `Clipboard_TrySetImage_PutsPngAndDib` (run on a dedicated STA thread; read back with `IsClipboardFormatAvailable` for `CF_DIB` and the PNG format → both true; decode PNG bytes → same size).
  - `Hotkey_SameComboTwice_SecondIsTaken` (register Ctrl+Alt+Shift+F12 under `CaptureArea` on one service instance, then the same combo under `CaptureArea` on a second instance → `TakenByOtherApp`; dispose both).
  - `Hotkey_UnmappableKey_IsInvalid` (`new Hotkey(Ctrl, "NotAKey")`).
  - `Autostart_EnableDisable_RoundTrips` (value name `"ScreenCapture.Tests." + Guid`, always deleted in `finally`).
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement. **Step 4:** Run → PASS.
- [ ] **Step 5:** Commit `feat: Windows clipboard, global hotkeys, autostart`.

---

### Task 10: App shell — single instance, composition root, tray menu

**Files:**
- Create/modify: `src/ScreenCapture.App/Program.cs`, `App.axaml(.cs)`, `app.manifest`, `Assets/tray.ico`, `Services/AppServices.cs`, `Services/UserNotifier.cs`, `Common/MessageWindow.axaml(.cs)`, `Tray/TrayController.cs`, `Tray/TrayMenuWindow.axaml(.cs)`; delete template `MainWindow`/`MainWindowViewModel`.

**Interfaces:**
- Consumes: everything in Core and Platform.
- Produces:
  - `sealed class AppServices { Loc Loc; SettingsStore SettingsStore; AppSettings Settings (get/set, set saves); IScreenCapturer Capturer; IGlobalHotkeyService Hotkeys; IClipboardImageService Clipboard; IAutostartService Autostart; UserNotifier Notifier; CaptureGate Gate; }`
  - `sealed class UserNotifier { void ShowError(string message); }` — non-modal `MessageWindow` (title `App_Name`, message, OK), also writes `FileLog.Error`.
  - `TrayController(AppServices, Action captureArea, Action captureFullScreen, Action openSettings)` — owns the `TrayIcon`.

Behavior:
- `Program.Main`: named mutex `Local\ScreenCapture.SingleInstance`; if not acquired, exit with code 0. `FileLog.Init`. Hook `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException` → log + `Error_Unexpected` with log path.
- `app.manifest`: `<dpiAwareness>PerMonitorV2</dpiAwareness>`, `<dpiAware>true/pm</dpiAware>`, Windows 10 `supportedOS`.
- `App`: `ShutdownMode.OnExplicitShutdown`, no main window. Startup: load settings (if `WasCorrupt` → `Error_SettingsCorrupt`), `Loc.SetLanguage(settings.Language)` (first run: `ResolveInitialLanguage(CultureInfo.CurrentUICulture.Name, …)`).
- Tray: `TrayIcon` with `ToolTipText = App_Name`, native `Menu` (right click) and `Clicked` (left click) → `TrayMenuWindow` near the cursor: borderless, topmost, `ShowInTaskbar=false`, closes on deactivation/Esc. Both menus show the same items: `Tray_CaptureArea` + hotkey text, `Tray_CaptureFullScreen` + hotkey text, `Tray_Settings`, `Tray_Exit`. Menu texts refresh on language/hotkey change. Exit: unregister hotkeys, dispose tray, `Shutdown()`.
- In this task the capture/settings actions are stubs that log only.

- [ ] **Step 1:** Implement the above.
- [ ] **Step 2:** `dotnet build` → success, `dotnet test` → all pass.
- [ ] **Step 3: Manual check** `dotnet run --project src/ScreenCapture.App`: tray icon appears; left click shows the menu near the cursor; right click shows the same items; Esc/click elsewhere closes it; Exit quits; a second `dotnet run` exits immediately while the first runs; deleting/corrupting `%APPDATA%\ScreenCapture\settings.json` with `{ x` shows the corrupt-settings message.
- [ ] **Step 4:** Commit `feat: tray app shell with single instance`.

---

### Task 11: Capture flow — overlays and full screen

**Files:**
- Create: `src/ScreenCapture.App/Capture/CaptureCoordinator.cs`, `Capture/OverlayWindow.axaml(.cs)`; minimal `Editor/EditorWindow.axaml(.cs)` that just displays an `SKBitmap` (completed in Task 12).

**Interfaces:**
- Consumes: `IScreenCapturer`, `CaptureGate`, `SelectionMath`.
- Produces: `sealed class CaptureCoordinator(AppServices services, Action<SKBitmap> openEditor) { void StartArea(); void StartFullScreen(); }`; `EditorWindow(SKBitmap image, AppServices services)` constructor used from here on.

Behavior:
- `StartArea`: `if (!Gate.TryEnter()) return;` → `CaptureAllMonitors()` before any window shows → one `OverlayWindow` per snapshot: `SystemDecorations.None`, `Topmost`, `ShowInTaskbar=false`, `Position = BoundsPx.TopLeft` (PixelPoint), size set so it exactly covers the monitor (use `WindowState.FullScreen` after positioning, then verify `RenderScaling` matches `Scale`), cross cursor. It draws the frozen image scaled to the window, a 50 % black dim over everything, the selection undimmed with a 1-px white border, `W × H` px label near the selection, and `Overlay_Hint` at the top center until dragging starts.
- Pointer press/drag/release on one overlay; selection never leaves that overlay. Release → `SelectionMath.ToPixelRect(start, end, RenderScaling, imageSize)`; if `IsTooSmall` → reset and keep overlays; else crop (`SKBitmap.ExtractSubset` → copy), close **all** overlays, `Gate.Exit()`, `openEditor(cropped)`.
- Esc on any overlay → close all, `Gate.Exit()`. Any exception → close all, `Gate.Exit()`, `Error_CaptureFailed`.
- `StartFullScreen`: same gate; `CaptureMonitorAtCursor()` → `Gate.Exit()` → `openEditor(image)`.
- Wire tray actions to the coordinator.

- [ ] **Step 1:** Implement.
- [ ] **Step 2:** `dotnet build`, `dotnet test` → pass.
- [ ] **Step 3: Manual check** (record results in the commit message body): area capture on a 100 % monitor and, if available, on a 125–200 % monitor — the editor image matches exactly what was selected, sharp; reversed drag works; click without drag keeps the overlay; Esc cancels; triggering capture twice quickly opens only one overlay set; full-screen capture takes the monitor under the cursor; with two monitors the overlay covers both and selection works on each.
- [ ] **Step 4:** Commit `feat: area and full-screen capture flow`.

---

### Task 12: Editor — tools, selection, undo/redo

**Files:**
- Create: `src/ScreenCapture.Core/Editing/EditorSession.cs`; `src/ScreenCapture.App/Editor/EditorViewModel.cs`, `Editor/AnnotationCanvas.cs`; complete `Editor/EditorWindow.axaml(.cs)`
- Test: `tests/ScreenCapture.Core.Tests/EditorSessionTests.cs`

**Interfaces:**
- Consumes: `AnnotationDocument`, commands, annotations, `AnnotationRenderer`, `ViewportMath`.
- Produces:
  - `enum EditorTool { Select, Arrow, Line, Rectangle, Marker, Text, Blur }`
  - `sealed class EditorSession(SKBitmap image) { AnnotationDocument Document; EditorTool Tool; SKColor Color; StrokeWidth Width; Annotation? Draft; Guid? SelectedId; void PointerDown(SKPoint imagePt); void PointerMove(SKPoint imagePt); void PointerUp(SKPoint imagePt); void DeleteSelected(); void AddText(SKPoint position, string text); void ReplaceText(Guid id, string text); IEnumerable<Annotation> RenderList { get; } }` — `RenderList` = document annotations + `Draft` (if any), with a selected item drawn in place during a move.
  - Defaults: `Tool = Arrow`, `Color = AnnotationPalette.Default`, `Width = Medium`; text `FontSize = 12 + 4 * (int)Width` (20 for Medium).

Session rules:
- Drawing tools: down starts `Draft`, move updates it, up commits `AddAnnotationCommand` if the shape is at least 3 px long (line-like) or 3×3 px (rect-like); otherwise discards. Switching tools clears `SelectedId`.
- Select tool: down hit-tests (tolerance 4 / viewport scale is applied by the caller) → sets `SelectedId` or clears it; drag moves a preview; up commits **one** `ReplaceAnnotationCommand` (so undo reverts the whole drag); a zero-distance click commits nothing.
- `DeleteSelected` → `RemoveAnnotationCommand`, clears selection. `AddText` with empty/whitespace text does nothing.

UI (`EditorWindow`):
- Title `Editor_Title`; window sized to the image up to 90 % of the work area, centered, min 640×400; normal window (taskbar entry), several can be open.
- Toolbar: tool toggle buttons (`Tool_*` tooltips), 8 color swatches, 3 stroke buttons, Undo, Redo (disabled when not possible), spacer, Save, **Copy** (accent style, primary).
- `AnnotationCanvas`: computes `ViewportMath.Fit` on resize; renders `AnnotationRenderer.Draw(original, session.RenderList)` into a cached `WriteableBitmap` at image resolution whenever the document or draft changes, then draws it scaled; draws a dashed selection box around the selected annotation's `Bounds`. Converts pointer positions with `ViewportMath.ToImage`.
- Text tool: click → a `TextBox` overlay at the click point; Enter or focus loss → `AddText`; Esc cancels. Double-click a text annotation in Select → edit it → `ReplaceText`.
- Keys: Ctrl+Z undo; Ctrl+Y and Ctrl+Shift+Z redo; Delete removes selected; Esc clears draft/selection (does **not** close the window). While the text box has focus, keys go to it.

- [ ] **Step 1: Write failing tests:** `Drag_WithRectangleTool_AddsOneRectangle`, `TinyDrag_AddsNothing`, `Draft_VisibleInRenderListDuringDrag`, `SelectAndDrag_MovesWithSingleUndoStep` (after move, one `Undo` restores the original position), `Click_OnEmpty_ClearsSelection`, `DeleteSelected_ThenUndo_Restores`, `AddText_Empty_AddsNothing`, `ReplaceText_IsUndoable`.
- [ ] **Step 2:** Run → FAIL. **Step 3:** Implement `EditorSession`. **Step 4:** Run → PASS.
- [ ] **Step 5:** Implement `EditorViewModel` (CommunityToolkit.Mvvm; commands for tools, colors, widths, undo/redo bound to `Document.CanUndo/CanRedo`), `AnnotationCanvas` and `EditorWindow`.
- [ ] **Step 6: Manual check:** draw every tool type; move and delete; undo/redo through a mixed sequence; blur over text makes it unreadable and moving the blur re-blurs the new spot; resize the window — annotations stay aligned to the image; language set to Ukrainian shows Ukrainian tooltips.
- [ ] **Step 7:** Commit `feat: annotation editor with tools and undo/redo`.

---

### Task 13: Editor output — Copy and Save

**Files:**
- Modify: `src/ScreenCapture.App/Editor/EditorViewModel.cs`, `Editor/EditorWindow.axaml(.cs)`
- Create: `Editor/JpegQualityDialog.axaml(.cs)`

**Interfaces:**
- Consumes: `AnnotationRenderer.Flatten`, `ImageExporter`, `IClipboardImageService`, `AppServices.Settings`, `UserNotifier`.
- Produces: `Task CopyAsync()`, `Task SaveAsync()` on `EditorViewModel`; `JpegQualityDialog.ShowAsync(Window owner, int initial) : Task<int?>` (null = cancelled).

Behavior:
- **Copy** (button / Ctrl+C when the text box is not focused): `Flatten` → `TrySetImage`; true → close the window; false → `Error_ClipboardFailed`, window stays open.
- **Save** (button / Ctrl+S): ensure `Settings.SaveFolder` exists (create it; if that fails, fall back to the Pictures folder); `StorageProvider.SaveFilePickerAsync` with suggested name `ImageExporter.DefaultFileName(DateTime.Now, Settings.DefaultFormat)`, start location = save folder, file types PNG (`*.png`) and JPG (`*.jpg;*.jpeg`) with the default format first. Cancel → nothing. If the chosen path is JPG → `JpegQualityDialog` (slider 1–100, initial `Settings.JpgQuality`, value label, `Common_OK`/`Common_Cancel`); cancel → nothing saved. Then `ImageExporter.Save`; on exception → `Format("Error_SaveFailed", ex.Message)`. Editor stays open in every case.

- [ ] **Step 1:** Implement.
- [ ] **Step 2:** `dotnet build`, `dotnet test` → pass.
- [ ] **Step 3: Manual check:** Copy pastes the annotated image into Paint and into a browser/chat input (PNG path) and Word/older app (DIB path), and closes the editor; Save as PNG, then Copy still works from the same editor; Save as JPG with quality 20 vs 95 gives visibly different file sizes; saving into a read-only folder shows the error and keeps the editor; deleting the configured save folder before saving recreates it.
- [ ] **Step 4:** Commit `feat: copy to clipboard and save as PNG/JPG`.

---

### Task 14: Settings window, hotkey reassignment, live language

**Files:**
- Create: `src/ScreenCapture.App/Services/HotkeyManager.cs`, `Settings/SettingsWindow.axaml(.cs)`, `Settings/SettingsViewModel.cs`, `Settings/HotkeyBox.cs`
- Modify: `App.axaml.cs`, `Tray/TrayController.cs` (open settings; refresh hotkey text)

**Interfaces:**
- Consumes: `IGlobalHotkeyService`, `HotkeyRules`, `AppSettings`, `Loc`, `IAutostartService`.
- Produces:
  - `enum HotkeyAssignResult { Ok, Invalid, Duplicate, TakenByOtherApp }`
  - `sealed class HotkeyManager(IGlobalHotkeyService service, AppServices services) { void RegisterFromSettings(); HotkeyAssignResult TryAssign(HotkeyId id, Hotkey? hotkey); bool Suspended { get; set; } event EventHandler? HotkeysChanged; event EventHandler<HotkeyId>? Triggered; }` — `Triggered` re-raises the service's `Pressed` unless `Suspended`; the capture coordinator subscribes to `Triggered`. — `TryAssign`: null → unregister, save, Ok. Not `IsValid` → Invalid. Equal to the other id's hotkey → Duplicate. Otherwise register; on `TakenByOtherApp` re-register the previous hotkey and return it; on Ok save settings and raise `HotkeysChanged`. `RegisterFromSettings` at startup reports each failure once via `Error_HotkeyTaken`.

Settings window (single instance — reopening activates the existing one; changes apply immediately, no OK button):
- Language: ComboBox of `Loc.AvailableLanguages()`; change → `Loc.SetLanguage`, save; all open windows and the tray menu update without restart.
- Two rows (`Settings_HotkeyArea`, `Settings_HotkeyFullScreen`): `HotkeyBox` + `Settings_HotkeyReset` + `Settings_HotkeyClear`. `HotkeyBox`: shows `Hotkey.ToString()` or `Settings_HotkeyNone`; when focused shows `Settings_HotkeyPrompt` and sets `HotkeyManager.Suspended = true` (false on focus loss), so pressing the current combo doesn't start a capture; on KeyDown of a non-modifier key builds `Hotkey` from `KeyModifiers` + `Key.ToString()` and calls `TryAssign`; Esc leaves the old value. Result messages: Invalid → `Error_HotkeyInvalid`, Duplicate → `Format("Error_HotkeyDuplicate", combo)`, Taken → `Format("Error_HotkeyTaken", combo)`, shown inline under the row in red (not a popup).
- Default format: PNG/JPG radio; JPG quality slider 1–100 with value label; Save folder: text + `Common_Browse` (`OpenFolderPickerAsync`); Start with Windows checkbox → `Autostart.SetEnabled`; failure → `Error_AutostartFailed` and the checkbox reverts. On open, the checkbox reflects `Autostart.IsEnabled()` (not just the stored setting).

- [ ] **Step 1:** Implement.
- [ ] **Step 2:** `dotnet build`, `dotnet test` → pass.
- [ ] **Step 3: Manual check:** reassign area hotkey to Ctrl+Shift+Q — new combo works at once, old one no longer triggers, tray menu shows the new text; assign the full-screen combo to area → duplicate message, old value kept; assign a combo held by another app (e.g. register it in a second test tool or use one known to be taken on the machine) → taken message, old value kept; plain `A` → invalid message; `F9` alone accepted; Clear → no hotkey, tray shows none; Reset → Ctrl+Alt+5; switch to Українська → settings, tray menu and an open editor switch immediately; autostart on → registry `Run` value exists, off → removed; restart the app — all settings persisted.
- [ ] **Step 4:** Commit `feat: settings window with hotkey reassignment and live language switch`.

---

### Task 15: Team build and README

**Files:**
- Create: `publish.ps1`, `README.md`

- [ ] **Step 1:** `publish.ps1` runs `dotnet test` (stop on failure), then `dotnet publish src/ScreenCapture.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish`, then zips to `artifacts/ScreenCapture-win-x64.zip`. Header comment: how to run it (`pwsh ./publish.ps1`).
- [ ] **Step 2:** `README.md` (English): what it is, how to build/run/test, how to publish, how to add a language (copy `Strings.uk.resx` to `Strings.<code>.resx`, translate, rebuild — it appears in Settings automatically), default hotkeys, known limits (no HDR, some protected windows capture black, unsigned exe → SmartScreen warning).
- [ ] **Step 3:** Run `pwsh ./publish.ps1` → zip exists; unzip to a clean folder, run the exe on a machine/user without the SDK if available: tray works, Ukrainian is selectable (satellite resources survived single-file publish), capture → copy works.
- [ ] **Step 4:** Commit `build: single-file publish script and README`.
