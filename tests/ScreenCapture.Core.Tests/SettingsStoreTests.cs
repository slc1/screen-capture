using ScreenCapture.Core.Export;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Core.Settings;

namespace ScreenCapture.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sc-settings-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");
    private static AppSettings Defaults() => AppSettings.CreateDefault(@"C:\Pics", "en");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var r = new SettingsStore(FilePath, Defaults).Load();
        Assert.False(r.WasCorrupt);
        Assert.Equal(Defaults(), r.Settings);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var store = new SettingsStore(FilePath, Defaults);
        var s = new AppSettings
        {
            Language = "uk",
            AreaHotkey = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "F9"),
            FullScreenHotkey = null,
            DefaultFormat = ImageFormatKind.Jpg,
            JpgQuality = 42,
            SaveFolder = @"D:\Shots",
            StartWithWindows = true,
        };
        store.Save(s);
        var r = store.Load();
        Assert.False(r.WasCorrupt);
        Assert.Equal(s, r.Settings);
        Assert.Null(r.Settings.FullScreenHotkey);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaultsAndFlag()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");
        var r = new SettingsStore(FilePath, Defaults).Load();
        Assert.True(r.WasCorrupt);
        Assert.Equal(Defaults(), r.Settings);
    }

    [Fact]
    public void Load_ClampsJpgQuality()
    {
        var store = new SettingsStore(FilePath, Defaults);
        store.Save(Defaults() with { JpgQuality = 250 });
        Assert.Equal(100, store.Load().Settings.JpgQuality);
        store.Save(Defaults() with { JpgQuality = -5 });
        Assert.Equal(1, store.Load().Settings.JpgQuality);
    }
}
