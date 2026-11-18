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
}
