using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.Core.Settings;

public sealed class SettingsStore(string path, Func<AppSettings> defaults)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsLoadResult Load()
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return new(defaults(), false);
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject root)
                return new(defaults(), true);
            var s = root.Deserialize<AppSettings>(Options);
            if (s is null) return new(defaults(), true);
            return Validate(root, s);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            FileLog.Error($"Failed to load settings from '{path}'", ex);
            return new(defaults(), true);
        }
    }

    private SettingsLoadResult Validate(JsonObject root, AppSettings s)
    {
        var d = defaults();
        var repaired = false;

        if (string.IsNullOrWhiteSpace(s.Language)) { s = s with { Language = d.Language }; repaired = true; }
        if (string.IsNullOrWhiteSpace(s.SaveFolder)) { s = s with { SaveFolder = d.SaveFolder }; repaired = true; }
        if (!Enum.IsDefined(s.DefaultFormat)) { s = s with { DefaultFormat = d.DefaultFormat }; repaired = true; }

        var area = FixHotkey(root, nameof(AppSettings.AreaHotkey), s.AreaHotkey, d.AreaHotkey, ref repaired);
        var full = FixHotkey(root, nameof(AppSettings.FullScreenHotkey), s.FullScreenHotkey, d.FullScreenHotkey, ref repaired);
        // Added after 0.1.1: an older file lacks it, which is an upgrade, not damage.
        var recognize = root.ContainsKey(nameof(AppSettings.RecognizeHotkey))
            ? FixHotkey(root, nameof(AppSettings.RecognizeHotkey), s.RecognizeHotkey, d.RecognizeHotkey, ref repaired)
            : d.RecognizeHotkey;

        s = s with
        {
            AreaHotkey = area,
            FullScreenHotkey = full,
            RecognizeHotkey = recognize,
            JpgQuality = Math.Clamp(s.JpgQuality, 1, 100),
        };
        return new(s, repaired);
    }

    private static Hotkey? FixHotkey(JsonObject root, string name, Hotkey? value, Hotkey? fallback, ref bool repaired)
    {
        if (!root.ContainsKey(name)) { repaired = true; return fallback; }
        if (root[name] is null) return null;
        const int allowed = (int)(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win);
        if (value is null || ((int)value.Modifiers & ~allowed) != 0 || !HotkeyRules.IsValid(value))
        {
            repaired = true;
            return fallback;
        }
        return value;
    }

    public void Save(AppSettings s)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(s, Options));
        File.Move(tmp, path, overwrite: true);
    }
}
