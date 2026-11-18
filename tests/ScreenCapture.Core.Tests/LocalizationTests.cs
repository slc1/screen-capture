using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using ScreenCapture.Core.Localization;

namespace ScreenCapture.Core.Tests;

[Collection("Loc")]
public sealed class LocalizationTests : IDisposable
{
    private readonly string _previousLanguage = Loc.Instance.Language;

    public void Dispose() => Loc.Instance.SetLanguage(_previousLanguage);

    private static ResourceManager Rm() =>
        new("ScreenCapture.Core.Localization.Strings", typeof(Loc).Assembly);

    private static Dictionary<string, string> Set(ResourceManager rm, CultureInfo culture, bool parents)
    {
        var set = rm.GetResourceSet(culture, createIfNotExists: true, tryParents: parents);
        Assert.NotNull(set);
        return set!.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string?)e.Value ?? "");
    }

    [Fact]
    public void EveryKey_ExistsInEveryLanguage()
    {
        var rm = Rm();
        var neutral = Set(rm, CultureInfo.InvariantCulture, false);
        Assert.NotEmpty(neutral);
        foreach (var lang in Loc.AvailableLanguages())
        {
            var culture = lang.Code == "en" ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(lang.Code);
            var set = Set(rm, culture, false);
            foreach (var key in neutral.Keys)
            {
                Assert.True(set.ContainsKey(key), $"{lang.Code} is missing {key}");
                Assert.False(string.IsNullOrWhiteSpace(set[key]), $"{lang.Code}/{key} is empty");
            }
        }
    }

    [Fact]
    public void AvailableLanguages_ContainsEnglishAndUkrainian()
    {
        var langs = Loc.AvailableLanguages();
        Assert.Equal("en", langs[0].Code);
        Assert.Equal("English", langs[0].DisplayName);
        Assert.Contains(langs, l => l.Code == "uk" && l.DisplayName == "Українська");
    }

    [Fact]
    public void ResolveInitialLanguage_UkrainianOs_ReturnsUk() =>
        Assert.Equal("uk", Loc.ResolveInitialLanguage("uk-UA", Loc.AvailableLanguages()));

    [Fact]
    public void ResolveInitialLanguage_UnsupportedOs_ReturnsEn() =>
        Assert.Equal("en", Loc.ResolveInitialLanguage("de-DE", Loc.AvailableLanguages()));

    [Fact]
    public void SetLanguage_ChangesIndexerAndRaisesItemChanged()
    {
        Loc.Instance.SetLanguage("en");
        Assert.Equal("Exit", Loc.Instance["Tray_Exit"]);
        var names = new List<string?>();
        PropertyChangedEventHandler h = (_, e) => names.Add(e.PropertyName);
        Loc.Instance.PropertyChanged += h;
        try
        {
            Loc.Instance.SetLanguage("uk");
        }
        finally
        {
            Loc.Instance.PropertyChanged -= h;
        }
        Assert.Equal("Вихід", Loc.Instance["Tray_Exit"]);
        Assert.Equal("uk", Loc.Instance.Language);
        Assert.Contains("Item[]", names);
    }

    [Fact]
    public void Indexer_MissingKey_ReturnsKey() =>
        Assert.Equal("No_Such_Key", Loc.Instance["No_Such_Key"]);

    [Fact]
    public void Format_SubstitutesArgs()
    {
        Loc.Instance.SetLanguage("en");
        Assert.Contains("x", Loc.Instance.Format("Error_SaveFailed", "x"));
    }
}
