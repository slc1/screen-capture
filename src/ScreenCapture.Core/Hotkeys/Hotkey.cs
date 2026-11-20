using System.Text;

namespace ScreenCapture.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers { None = 0, Ctrl = 1, Alt = 2, Shift = 4, Win = 8 }

public enum HotkeyId { CaptureArea, CaptureFullScreen, RecognizeArea }

/// <summary>Key is an Avalonia Key enum name (e.g. "D5", "F9", "PrintScreen").</summary>
public sealed record Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) sb.Append("Win+");
        sb.Append(Key.Length == 2 && Key[0] == 'D' && char.IsDigit(Key[1]) ? Key[1..] : Key);
        return sb.ToString();
    }
}

public static class HotkeyRules
{
    private static readonly HashSet<string> ModifierKeys = new(StringComparer.Ordinal)
    {
        "LeftCtrl", "RightCtrl", "LeftAlt", "RightAlt", "LeftShift", "RightShift", "LWin", "RWin",
    };

    public static Hotkey DefaultFor(HotkeyId id) => id switch
    {
        HotkeyId.CaptureArea => new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "D5"),
        HotkeyId.CaptureFullScreen => new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "D6"),
        HotkeyId.RecognizeArea => new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "D7"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    public static bool IsValid(Hotkey h)
    {
        if (string.IsNullOrEmpty(h.Key) || ModifierKeys.Contains(h.Key)) return false;
        return h.Modifiers != HotkeyModifiers.None || h.Key == "PrintScreen" || IsFunctionKey(h.Key);
    }

    private static bool IsFunctionKey(string key) =>
        key.Length is 2 or 3 && key[0] == 'F' && key.Skip(1).All(char.IsAsciiDigit)
        && key[1] != '0' && int.Parse(key.AsSpan(1)) is >= 1 and <= 24;
}
