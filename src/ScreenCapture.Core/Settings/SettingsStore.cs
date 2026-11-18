using System.Text.Json;
using System.Text.Json.Serialization;

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
        if (!File.Exists(path)) return new(defaults(), false);
        try
        {
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            if (s is null) return new(defaults(), true);
            return new(s with { JpgQuality = Math.Clamp(s.JpgQuality, 1, 100) }, false);
        }
        catch (JsonException)
        {
            return new(defaults(), true);
        }
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
