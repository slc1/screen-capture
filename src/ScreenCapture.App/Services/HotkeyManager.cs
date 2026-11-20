using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Platform;

namespace ScreenCapture.App.Services;

public enum HotkeyAssignResult { Ok, Invalid, Duplicate, TakenByOtherApp }

public sealed class HotkeyManager
{
    public static readonly IReadOnlyList<HotkeyId> Ids = [HotkeyId.CaptureArea, HotkeyId.CaptureFullScreen, HotkeyId.RecognizeArea];

    private readonly IGlobalHotkeyService _service;
    private readonly AppServices _services;

    public HotkeyManager(IGlobalHotkeyService service, AppServices services)
    {
        _service = service;
        _services = services;
        _service.Pressed += OnPressed;
    }

    /// <summary>While true, presses are swallowed (e.g. while the user records a new combination).</summary>
    public bool Suspended { get; set; }

    public event EventHandler? HotkeysChanged;

    public event EventHandler<HotkeyId>? Triggered;

    public Hotkey? Get(HotkeyId id) => id switch
    {
        HotkeyId.CaptureArea => _services.Settings.AreaHotkey,
        HotkeyId.CaptureFullScreen => _services.Settings.FullScreenHotkey,
        HotkeyId.RecognizeArea => _services.Settings.RecognizeHotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(id)),
    };

    public void RegisterFromSettings()
    {
        foreach (var id in Ids)
        {
            if (Get(id) is not { } hotkey) continue;
            var result = _service.Register(id, hotkey);
            if (result == HotkeyRegistrationResult.Ok) continue;
            FileLog.Info($"Hotkey {hotkey} for {id} not registered: {result}");
            _services.Notifier.ShowError(_services.Loc.Format("Error_HotkeyTaken", hotkey));
        }
    }

    public HotkeyAssignResult TryAssign(HotkeyId id, Hotkey? hotkey)
    {
        if (hotkey is null)
        {
            _service.Unregister(id);
            Store(id, null);
            return HotkeyAssignResult.Ok;
        }

        if (!HotkeyRules.IsValid(hotkey)) return HotkeyAssignResult.Invalid;
        if (Ids.Any(other => other != id && Get(other) == hotkey)) return HotkeyAssignResult.Duplicate;

        var previous = Get(id);
        switch (_service.Register(id, hotkey))
        {
            case HotkeyRegistrationResult.Ok:
                Store(id, hotkey);
                return HotkeyAssignResult.Ok;
            case HotkeyRegistrationResult.Invalid:
                return HotkeyAssignResult.Invalid;
            default:
                // Register already dropped the previous combination before failing.
                if (previous is not null) RestorePrevious(id, previous);
                return HotkeyAssignResult.TakenByOtherApp;
        }
    }

    private void RestorePrevious(HotkeyId id, Hotkey previous)
    {
        var result = _service.Register(id, previous);
        if (result == HotkeyRegistrationResult.Ok) return;
        FileLog.Info($"Could not restore hotkey {previous} for {id}: {result}");
        _services.Notifier.ShowError(_services.Loc.Format("Error_HotkeyTaken", previous));
    }

    private void Store(HotkeyId id, Hotkey? hotkey)
    {
        var settings = _services.Settings;
        try
        {
            _services.Settings = id switch
            {
                HotkeyId.CaptureArea => settings with { AreaHotkey = hotkey },
                HotkeyId.CaptureFullScreen => settings with { FullScreenHotkey = hotkey },
                HotkeyId.RecognizeArea => settings with { RecognizeHotkey = hotkey },
                _ => throw new ArgumentOutOfRangeException(nameof(id)),
            };
        }
        catch (Exception ex)
        {
            FileLog.Error("Could not save settings", ex);
            _services.Notifier.ShowError(_services.Loc.Format("Error_Unexpected", Program.LogPath));
        }
        finally
        {
            // The in-memory settings already changed even if writing the file failed.
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPressed(object? sender, HotkeyId id)
    {
        if (!Suspended) Triggered?.Invoke(this, id);
    }
}
