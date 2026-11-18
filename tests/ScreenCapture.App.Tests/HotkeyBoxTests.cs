using Avalonia.Input;
using ScreenCapture.App.Settings;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.App.Tests;

public sealed class HotkeyBoxTests
{
    [Theory]
    [InlineData(Key.LeftCtrl)]
    [InlineData(Key.RightCtrl)]
    [InlineData(Key.LeftAlt)]
    [InlineData(Key.RightAlt)]
    [InlineData(Key.LeftShift)]
    [InlineData(Key.RightShift)]
    [InlineData(Key.LWin)]
    [InlineData(Key.RWin)]
    [InlineData(Key.None)]
    public void Modifier_keys_alone_build_nothing(Key key) =>
        Assert.Null(HotkeyBox.FromKey(key, KeyModifiers.Control));

    [Fact]
    public void Maps_all_modifiers()
    {
        var hotkey = HotkeyBox.FromKey(Key.Q, KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta);

        Assert.Equal(new Hotkey(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Win, "Q"), hotkey);
    }

    [Theory]
    [InlineData(Key.D5, "D5")]
    [InlineData(Key.F9, "F9")]
    [InlineData(Key.PrintScreen, "PrintScreen")]
    [InlineData(Key.PageUp, "PageUp")]
    [InlineData(Key.PageDown, "PageDown")]
    [InlineData(Key.NumPad3, "NumPad3")]
    public void Uses_the_key_names_the_hotkey_service_understands(Key key, string expected) =>
        Assert.Equal(expected, HotkeyBox.FromKey(key, KeyModifiers.None)?.Key);
}
