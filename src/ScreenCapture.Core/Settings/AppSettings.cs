using ScreenCapture.Core.Export;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.Core.Settings;

public sealed record AppSettings
{
    public string Language { get; init; } = "en";
    public Hotkey? AreaHotkey { get; init; }
    public Hotkey? FullScreenHotkey { get; init; }
    public Hotkey? RecognizeHotkey { get; init; }
    public ImageFormatKind DefaultFormat { get; init; } = ImageFormatKind.Png;
    public int JpgQuality { get; init; } = 90;
    public string SaveFolder { get; init; } = "";
    public bool StartWithWindows { get; init; }

    public static AppSettings CreateDefault(string picturesFolder, string language) => new()
    {
        Language = language,
        AreaHotkey = HotkeyRules.DefaultFor(HotkeyId.CaptureArea),
        FullScreenHotkey = HotkeyRules.DefaultFor(HotkeyId.CaptureFullScreen),
        RecognizeHotkey = HotkeyRules.DefaultFor(HotkeyId.RecognizeArea),
        DefaultFormat = ImageFormatKind.Png,
        JpgQuality = 90,
        SaveFolder = Path.Combine(picturesFolder, "Screenshots"),
        StartWithWindows = false,
    };
}

public sealed record SettingsLoadResult(AppSettings Settings, bool WasCorrupt);
