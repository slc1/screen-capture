using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace ScreenCapture.Core.Localization;

public sealed record LanguageOption(string Code, string DisplayName);

public sealed class Loc : INotifyPropertyChanged
{
    private const string EnglishCode = "en";

    private static readonly ResourceManager Resources =
        new("ScreenCapture.Core.Localization.Strings", typeof(Loc).Assembly);

    public static Loc Instance { get; } = new();

    private CultureInfo _culture = CultureInfo.InvariantCulture;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = EnglishCode;

    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    public string Format(string key, params object[] args) =>
        string.Format(_culture, this[key], args);

    public void SetLanguage(string code)
    {
        // English is the neutral resource set; an unknown code falls back to it.
        var known = AvailableLanguages().Any(l => l.Code == code);
        Language = known ? code : EnglishCode;
        _culture = Language == EnglishCode ? CultureInfo.InvariantCulture : CultureInfo.GetCultureInfo(Language);
        // Avalonia bindings refresh an indexer on "Item"; WPF-style listeners use "Item[]".
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public static IReadOnlyList<LanguageOption> AvailableLanguages()
    {
        var result = new List<LanguageOption> { Option(CultureInfo.GetCultureInfo(EnglishCode)) };
        var others = CultureInfo.GetCultures(CultureTypes.NeutralCultures)
            .Where(c => c.Name.Length > 0 && c.Name != EnglishCode)
            .Where(c => Resources.GetResourceSet(c, createIfNotExists: true, tryParents: false) != null)
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .Select(Option);
        result.AddRange(others);
        return result;
    }

    public static string ResolveInitialLanguage(string osUiCulture, IReadOnlyList<LanguageOption> available)
    {
        var code = osUiCulture.Split('-', '_')[0].ToLowerInvariant();
        return available.Any(l => l.Code == code) ? code : EnglishCode;
    }

    private static LanguageOption Option(CultureInfo c)
    {
        var name = c.NativeName;
        return new LanguageOption(c.Name, name.Length == 0 ? name : char.ToUpper(name[0], c) + name[1..]);
    }
}
