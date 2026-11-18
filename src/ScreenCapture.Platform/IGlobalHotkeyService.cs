using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.Platform;

public enum HotkeyRegistrationResult { Ok, TakenByOtherApp, Invalid }

public interface IGlobalHotkeyService : IDisposable
{
    HotkeyRegistrationResult Register(HotkeyId id, Hotkey hotkey);
    void Unregister(HotkeyId id);
    event EventHandler<HotkeyId>? Pressed;
}
