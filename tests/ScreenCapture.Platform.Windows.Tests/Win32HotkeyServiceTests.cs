using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Platform.Windows;

namespace ScreenCapture.Platform.Windows.Tests;

public class Win32HotkeyServiceTests
{
    private static readonly Hotkey Combo =
        new(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt | HotkeyModifiers.Shift, "F12");

    [Fact]
    public void Hotkey_SameComboTwice_SecondIsTaken()
    {
        using var a = new Win32HotkeyService(act => act());
        using var b = new Win32HotkeyService(act => act());
        Assert.Equal(HotkeyRegistrationResult.Ok, a.Register(HotkeyId.CaptureArea, Combo));
        Assert.Equal(HotkeyRegistrationResult.TakenByOtherApp, b.Register(HotkeyId.CaptureArea, Combo));
    }

    [Fact]
    public void Hotkey_UnmappableKey_IsInvalid()
    {
        using var s = new Win32HotkeyService(act => act());
        Assert.Equal(HotkeyRegistrationResult.Invalid,
            s.Register(HotkeyId.CaptureArea, new Hotkey(HotkeyModifiers.Ctrl, "NotAKey")));
    }
    [Theory]
    [InlineData("Return", 0x0D)]
    [InlineData("Enter", 0x0D)]
    [InlineData("Back", 0x08)]
    [InlineData("Oem1", 0xBA)]
    [InlineData("OemSemicolon", 0xBA)]
    [InlineData("Oem2", 0xBF)]
    [InlineData("OemQuestion", 0xBF)]
    [InlineData("Oem3", 0xC0)]
    [InlineData("OemTilde", 0xC0)]
    [InlineData("Oem4", 0xDB)]
    [InlineData("OemOpenBrackets", 0xDB)]
    [InlineData("Oem5", 0xDC)]
    [InlineData("OemPipe", 0xDC)]
    [InlineData("Oem6", 0xDD)]
    [InlineData("OemCloseBrackets", 0xDD)]
    [InlineData("Oem7", 0xDE)]
    [InlineData("OemQuotes", 0xDE)]
    [InlineData("Oem102", 0xE2)]
    [InlineData("OemBackslash", 0xE2)]
    [InlineData("Multiply", 0x6A)]
    [InlineData("Add", 0x6B)]
    [InlineData("Subtract", 0x6D)]
    [InlineData("Decimal", 0x6E)]
    [InlineData("Divide", 0x6F)]
    public void MapKey_MapsPunctuationEditingAndNumPadOperators(string key, int vk) =>
        Assert.Equal((uint)vk, Win32HotkeyService.MapKey(key));

    [Fact]
    public void MapKey_LeavesTabUnmapped() => Assert.Null(Win32HotkeyService.MapKey("Tab"));
}
