using System.Runtime.InteropServices;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.Platform.Windows;

/// <summary>
/// Must be constructed on a thread that pumps messages; WM_HOTKEY is delivered to that thread.
/// </summary>
public sealed class Win32HotkeyService : IGlobalHotkeyService
{
    private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8, MOD_NOREPEAT = 0x4000;

    private readonly Action<Action> _postToUiThread;
    private readonly NativeMethods.WndProc _wndProc; // field keeps the delegate alive for the window's lifetime
    private readonly string _className = "ScreenCapture.Hotkeys." + Guid.NewGuid();
    private readonly nint _hInstance;
    private readonly nint _hwnd;
    private readonly Dictionary<HotkeyId, int> _registered = new();
    private bool _disposed;

    public event EventHandler<HotkeyId>? Pressed;

    public unsafe Win32HotkeyService(Action<Action> postToUiThread)
    {
        _postToUiThread = postToUiThread;
        _wndProc = WndProcImpl;
        _hInstance = NativeMethods.GetModuleHandleW(null);

        var namePtr = Marshal.StringToHGlobalUni(_className);
        try
        {
            var wc = new NativeMethods.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(NativeMethods.WNDCLASSEXW),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = _hInstance,
                lpszClassName = namePtr,
            };
            if (NativeMethods.RegisterClassExW(wc) == 0)
                throw new InvalidOperationException("RegisterClassEx failed: " + Marshal.GetLastWin32Error());
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }

        _hwnd = NativeMethods.CreateWindowExW(0, _className, null, 0, 0, 0, 0, 0,
            NativeMethods.HWND_MESSAGE, 0, _hInstance, 0);
        if (_hwnd == 0)
        {
            var err = Marshal.GetLastWin32Error();
            NativeMethods.UnregisterClassW(_className, _hInstance);
            throw new InvalidOperationException("CreateWindowEx failed: " + err);
        }
    }

    public HotkeyRegistrationResult Register(HotkeyId id, Hotkey hotkey)
    {
        if (MapKey(hotkey.Key) is not uint vk || !HotkeyRules.IsValid(hotkey)) return HotkeyRegistrationResult.Invalid;

        Unregister(id);

        uint mods = MOD_NOREPEAT;
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Ctrl)) mods |= MOD_CONTROL;
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt)) mods |= MOD_ALT;
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift)) mods |= MOD_SHIFT;
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win)) mods |= MOD_WIN;

        var regId = (int)id + 1;
        if (!NativeMethods.RegisterHotKey(_hwnd, regId, mods, vk)) return HotkeyRegistrationResult.TakenByOtherApp;
        _registered[id] = regId;
        return HotkeyRegistrationResult.Ok;
    }

    public void Unregister(HotkeyId id)
    {
        if (_registered.Remove(id, out var regId)) NativeMethods.UnregisterHotKey(_hwnd, regId);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var regId in _registered.Values) NativeMethods.UnregisterHotKey(_hwnd, regId);
        _registered.Clear();
        NativeMethods.DestroyWindow(_hwnd);
        NativeMethods.UnregisterClassW(_className, _hInstance);
    }

    private nint WndProcImpl(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY)
        {
            var regId = (int)wParam;
            foreach (var (id, r) in _registered)
            {
                if (r != regId) continue;
                var pressed = id;
                _postToUiThread(() => Pressed?.Invoke(this, pressed));
                break;
            }
            return 0;
        }
        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    internal static uint? MapKey(string key)
    {
        if (key.Length == 2 && key[0] == 'D' && char.IsAsciiDigit(key[1])) return key[1];
        if (key.Length == 1 && key[0] is >= 'A' and <= 'Z') return key[0];
        if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var f) && f is >= 1 and <= 24
            && key == "F" + f) return (uint)(0x70 + f - 1);
        if (key.Length == 7 && key.StartsWith("NumPad", StringComparison.Ordinal) && char.IsAsciiDigit(key[6]))
            return (uint)(0x60 + key[6] - '0');
        return key switch
        {
            "PrintScreen" => 0x2C,
            "OemMinus" => 0xBD,
            "OemPlus" => 0xBB,
            "OemComma" => 0xBC,
            "OemPeriod" => 0xBE,
            "Space" => 0x20,
            "Insert" => 0x2D,
            "Delete" => 0x2E,
            "Home" => 0x24,
            "End" => 0x23,
            "PageUp" => 0x21,
            "PageDown" => 0x22,
            "Left" => 0x25,
            "Up" => 0x26,
            "Right" => 0x27,
            "Down" => 0x28,
            "Pause" => 0x13,
            _ => null,
        };
    }
}
