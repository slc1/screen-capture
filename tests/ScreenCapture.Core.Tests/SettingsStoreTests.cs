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
            SaveFolder = @"D:/Shots",
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

    private SettingsLoadResult LoadJson(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, json);
        return new SettingsStore(FilePath, Defaults).Load();
    }

    private const string Hk = "{ \"Modifiers\": \"Ctrl\", \"Key\": \"F9\" }";

    [Fact]
    public void Load_JsonNullLiteral_ReturnsDefaultsAndFlag()
    {
        var r = LoadJson("null");
        Assert.True(r.WasCorrupt);
        Assert.Equal(Defaults(), r.Settings);
    }

    [Fact]
    public void Load_NullLanguage_FilledFromDefaultsAndFlag()
    {
        var r = LoadJson("{ \"Language\": null, \"SaveFolder\": \"D:/S\", \"AreaHotkey\": null, \"FullScreenHotkey\": null }");
        Assert.True(r.WasCorrupt);
        Assert.Equal("en", r.Settings.Language);
        Assert.Equal(@"D:/S", r.Settings.SaveFolder);
    }

    [Fact]
    public void Load_MissingHotkeyProperty_UsesDefault()
    {
        var r = LoadJson("{ \"Language\": \"uk\", \"SaveFolder\": \"D:/S\", \"FullScreenHotkey\": null }");
        Assert.Equal(Defaults().AreaHotkey, r.Settings.AreaHotkey);
        Assert.Null(r.Settings.FullScreenHotkey);
    }

    [Fact]
    public void Load_ExplicitNullHotkey_StaysCleared()
    {
        var r = LoadJson("{ \"Language\": \"uk\", \"SaveFolder\": \"D:/S\", \"AreaHotkey\": null, \"FullScreenHotkey\": " + Hk + " }");
        Assert.False(r.WasCorrupt);
        Assert.Null(r.Settings.AreaHotkey);
        Assert.Equal(new Hotkey(HotkeyModifiers.Ctrl, "F9"), r.Settings.FullScreenHotkey);
    }

    [Fact]
    public void Load_InvalidHotkey_UsesDefaultAndFlag()
    {
        var r = LoadJson("{ \"Language\": \"uk\", \"SaveFolder\": \"D:/S\", \"AreaHotkey\": { \"Modifiers\": \"Ctrl\", \"Key\": null }, \"FullScreenHotkey\": null }");
        Assert.True(r.WasCorrupt);
        Assert.Equal(Defaults().AreaHotkey, r.Settings.AreaHotkey);
    }

    [Fact]
    public void Load_UndefinedFormat_UsesDefaultAndFlag()
    {
        var r = LoadJson("{ \"Language\": \"uk\", \"SaveFolder\": \"D:/S\", \"DefaultFormat\": 99, \"AreaHotkey\": null, \"FullScreenHotkey\": null }");
        Assert.True(r.WasCorrupt);
        Assert.Equal(ImageFormatKind.Png, r.Settings.DefaultFormat);
    }

    [Fact]
    public void Load_LockedFile_ReturnsDefaultsAndFlag()
    {
        var store = new SettingsStore(FilePath, Defaults);
        store.Save(Defaults());
        using var _ = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.None);
        var r = store.Load();
        Assert.True(r.WasCorrupt);
        Assert.Equal(Defaults(), r.Settings);
    }

    [Fact]
    public void Save_LeavesNoTmpFile()
    {
        new SettingsStore(FilePath, Defaults).Save(Defaults());
        Assert.True(File.Exists(FilePath));
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    private const string V011File = """
        {
          "Language": "uk",
          "AreaHotkey": { "Modifiers": "Ctrl, Alt", "Key": "D5" },
          "FullScreenHotkey": null,
          "DefaultFormat": "Png",
          "JpgQuality": 90,
          "SaveFolder": "C:\\Pics\\Screenshots",
          "StartWithWindows": false
        }
        """;

    [Fact]
    public void Load_FileWithoutRecognizeHotkey_GetsDefaultWithoutCorruptFlag()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, V011File);

        var r = new SettingsStore(FilePath, Defaults).Load();

        Assert.False(r.WasCorrupt);
        Assert.Equal(HotkeyRules.DefaultFor(HotkeyId.RecognizeArea), r.Settings.RecognizeHotkey);
        Assert.Equal("uk", r.Settings.Language);
        Assert.Null(r.Settings.FullScreenHotkey);
    }

    [Fact]
    public void Load_ExplicitNullRecognizeHotkey_StaysCleared()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, V011File.Replace("\"StartWithWindows\": false", "\"StartWithWindows\": false, \"RecognizeHotkey\": null"));

        var r = new SettingsStore(FilePath, Defaults).Load();

        Assert.False(r.WasCorrupt);
        Assert.Null(r.Settings.RecognizeHotkey);
    }

    [Fact]
    public void Load_InvalidRecognizeHotkey_IsRepairedToDefault()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, V011File.Replace("\"StartWithWindows\": false",
            "\"StartWithWindows\": false, \"RecognizeHotkey\": { \"Modifiers\": \"None\", \"Key\": \"A\" }"));

        var r = new SettingsStore(FilePath, Defaults).Load();

        Assert.True(r.WasCorrupt);
        Assert.Equal(HotkeyRules.DefaultFor(HotkeyId.RecognizeArea), r.Settings.RecognizeHotkey);
    }

    [Fact]
    public void Default_RecognizeHotkey_IsCtrlAlt7() =>
        Assert.Equal("Ctrl+Alt+7", Defaults().RecognizeHotkey?.ToString());
}