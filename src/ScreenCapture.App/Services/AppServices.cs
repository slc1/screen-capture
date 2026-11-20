using ScreenCapture.Core.Capture;
using ScreenCapture.Core.Localization;
using ScreenCapture.Core.Settings;
using ScreenCapture.Platform;

namespace ScreenCapture.App.Services;

public sealed class AppServices(
    Loc loc,
    SettingsStore settingsStore,
    AppSettings settings,
    IScreenCapturer capturer,
    IGlobalHotkeyService hotkeys,
    IClipboardImageService clipboard,
    IAutostartService autostart,
    UserNotifier notifier,
    CaptureGate gate,
    IOcrService ocr,
    IClipboardTextService textClipboard)
{
    private AppSettings _settings = settings;

    public Loc Loc { get; } = loc;
    public SettingsStore SettingsStore { get; } = settingsStore;
    public IScreenCapturer Capturer { get; } = capturer;
    public IGlobalHotkeyService Hotkeys { get; } = hotkeys;
    public IClipboardImageService Clipboard { get; } = clipboard;
    public IAutostartService Autostart { get; } = autostart;
    public UserNotifier Notifier { get; } = notifier;
    public CaptureGate Gate { get; } = gate;
    public IOcrService Ocr { get; } = ocr;
    public IClipboardTextService TextClipboard { get; } = textClipboard;

    public AppSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            SettingsStore.Save(value);
        }
    }
}
