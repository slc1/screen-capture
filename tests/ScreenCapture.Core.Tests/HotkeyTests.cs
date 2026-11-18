using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.Core.Tests;

public class HotkeyTests
{
    [Fact]
    public void Default_AreaHotkey_IsCtrlAlt5()
    {
        Assert.Equal("Ctrl+Alt+5", HotkeyRules.DefaultFor(HotkeyId.CaptureArea).ToString());
        Assert.Equal("Ctrl+Alt+6", HotkeyRules.DefaultFor(HotkeyId.CaptureFullScreen).ToString());
    }

    [Fact]
    public void IsValid_RequiresModifier_ExceptFunctionKeysAndPrintScreen()
    {
        Assert.False(HotkeyRules.IsValid(new(HotkeyModifiers.None, "A")));
        Assert.True(HotkeyRules.IsValid(new(HotkeyModifiers.None, "F9")));
        Assert.True(HotkeyRules.IsValid(new(HotkeyModifiers.None, "PrintScreen")));
        Assert.True(HotkeyRules.IsValid(new(HotkeyModifiers.Shift, "A")));
        Assert.False(HotkeyRules.IsValid(new(HotkeyModifiers.Ctrl, "LeftCtrl")));
    }

    [Theory]
    [InlineData("F0", false)]
    [InlineData("F1", true)]
    [InlineData("F24", true)]
    [InlineData("F25", false)]
    [InlineData("F09", false)]
    [InlineData("F", false)]
    public void IsValid_FunctionKeyBoundaries(string key, bool expected) =>
        Assert.Equal(expected, HotkeyRules.IsValid(new(HotkeyModifiers.None, key)));
}
