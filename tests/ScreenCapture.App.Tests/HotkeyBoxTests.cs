using Avalonia.Input;
using ScreenCapture.App.Settings;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Platform.Windows;

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

    // Aliases (Return/Enter, Oem1/OemSemicolon, ...) share one enum value; whichever name ToString() picks must map.
    [Theory]
    [InlineData(Key.Enter, 0x0D)]
    [InlineData(Key.Back, 0x08)]
    [InlineData(Key.Oem1, 0xBA)]
    [InlineData(Key.Oem2, 0xBF)]
    [InlineData(Key.Oem3, 0xC0)]
    [InlineData(Key.Oem4, 0xDB)]
    [InlineData(Key.Oem5, 0xDC)]
    [InlineData(Key.Oem6, 0xDD)]
    [InlineData(Key.Oem7, 0xDE)]
    [InlineData(Key.Oem102, 0xE2)]
    [InlineData(Key.Multiply, 0x6A)]
    [InlineData(Key.Add, 0x6B)]
    [InlineData(Key.Subtract, 0x6D)]
    [InlineData(Key.Decimal, 0x6E)]
    [InlineData(Key.Divide, 0x6F)]
    [InlineData(Key.PageUp, 0x21)]
    public void Avalonia_key_names_are_accepted_by_the_hotkey_service(Key key, int vk)
    {
        var hotkey = HotkeyBox.FromKey(key, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.NotNull(hotkey);
        Assert.Equal((uint)vk, Win32HotkeyService.MapKey(hotkey.Key));
    }

    [Fact]
    public void Tab_is_not_accepted_by_the_hotkey_service() =>
        Assert.Null(Win32HotkeyService.MapKey(HotkeyBox.FromKey(Key.Tab, KeyModifiers.Control)!.Key));
}
