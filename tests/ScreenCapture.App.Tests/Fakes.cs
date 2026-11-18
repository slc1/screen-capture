using ScreenCapture.App.Services;
using ScreenCapture.Core.Capture;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Core.Localization;
using ScreenCapture.Core.Settings;
using ScreenCapture.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Tests;

/// <summary>Mirrors Win32HotkeyService: Register drops the id's previous hotkey before trying the new one.</summary>
internal sealed class FakeHotkeyService : IGlobalHotkeyService
{
    public Dictionary<HotkeyId, Hotkey> Registered { get; } = new();
    public HashSet<Hotkey> TakenByOthers { get; } = new();
    public List<(HotkeyId Id, Hotkey Hotkey)> RegisterCalls { get; } = new();

    public event EventHandler<HotkeyId>? Pressed;

    public HotkeyRegistrationResult Register(HotkeyId id, Hotkey hotkey)
    {
        RegisterCalls.Add((id, hotkey));
        if (!HotkeyRules.IsValid(hotkey)) return HotkeyRegistrationResult.Invalid;
        Unregister(id);
        if (TakenByOthers.Contains(hotkey)) return HotkeyRegistrationResult.TakenByOtherApp;
        Registered[id] = hotkey;
        return HotkeyRegistrationResult.Ok;
    }

    public void Unregister(HotkeyId id) => Registered.Remove(id);

    public void Press(HotkeyId id) => Pressed?.Invoke(this, id);

    public void Dispose()
    {
    }
}

internal sealed class UnusedCapturer : IScreenCapturer
{
    public IReadOnlyList<MonitorInfo> GetMonitors() => throw new NotSupportedException();
    public IReadOnlyList<ScreenSnapshot> CaptureAllMonitors() => throw new NotSupportedException();
    public ScreenSnapshot CaptureMonitorAtCursor() => throw new NotSupportedException();
}

internal sealed class UnusedClipboard : IClipboardImageService
{
    public bool TrySetImage(SKBitmap image) => throw new NotSupportedException();
}

internal sealed class FakeAutostart : IAutostartService
{
    public bool Enabled { get; set; }
    public bool IsEnabled() => Enabled;
    public void SetEnabled(bool enabled) => Enabled = enabled;
}

internal sealed class TestEnvironment : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ScreenCaptureAppTests", Guid.NewGuid().ToString("N"));

    public TestEnvironment()
    {
        Directory.CreateDirectory(_folder);
        SettingsPath = Path.Combine(_folder, "settings.json");
        Store = new SettingsStore(SettingsPath, () => AppSettings.CreateDefault(_folder, "en"));
        Hotkeys = new FakeHotkeyService();
        Services = new AppServices(
            Loc.Instance,
            Store,
            AppSettings.CreateDefault(_folder, "en"),
            new UnusedCapturer(),
            Hotkeys,
            new UnusedClipboard(),
            new FakeAutostart(),
            new UserNotifier(),
            new CaptureGate());
    }

    public string SettingsPath { get; }
    public SettingsStore Store { get; }
    public FakeHotkeyService Hotkeys { get; }
    public AppServices Services { get; }

    public AppSettings Saved => Store.Load().Settings;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
