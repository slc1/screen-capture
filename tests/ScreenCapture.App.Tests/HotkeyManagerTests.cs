using ScreenCapture.App.Services;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.App.Tests;

[Collection("FileLog")]
public sealed class HotkeyManagerTests : IDisposable
{
    private static readonly Hotkey AreaDefault = HotkeyRules.DefaultFor(HotkeyId.CaptureArea);
    private static readonly Hotkey FullDefault = HotkeyRules.DefaultFor(HotkeyId.CaptureFullScreen);
    private static readonly Hotkey CtrlShiftQ = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Q");

    private readonly TestEnvironment _env = new();
    private readonly HotkeyManager _manager;
    private int _changed;

    public HotkeyManagerTests()
    {
        _manager = new HotkeyManager(_env.Hotkeys, _env.Services);
        _manager.HotkeysChanged += (_, _) => _changed++;
    }

    public void Dispose() => _env.Dispose();

    [Fact]
    public void RegisterFromSettings_registers_both_hotkeys()
    {
        _manager.RegisterFromSettings();

        Assert.Equal(AreaDefault, _env.Hotkeys.Registered[HotkeyId.CaptureArea]);
        Assert.Equal(FullDefault, _env.Hotkeys.Registered[HotkeyId.CaptureFullScreen]);
    }

    [Fact]
    public void RegisterFromSettings_skips_cleared_hotkeys()
    {
        _env.Services.Settings = _env.Services.Settings with { FullScreenHotkey = null };

        _manager.RegisterFromSettings();

        Assert.DoesNotContain(_env.Hotkeys.RegisterCalls, c => c.Id == HotkeyId.CaptureFullScreen);
    }

    [Fact]
    public void RegisterFromSettings_reports_each_failure_once_and_keeps_settings()
    {
        var log = Path.Combine(Path.GetTempPath(), "ScreenCaptureAppTests", Guid.NewGuid().ToString("N") + ".log");
        FileLog.Init(log);
        _env.Hotkeys.TakenByOthers.Add(AreaDefault);
        _env.Hotkeys.TakenByOthers.Add(FullDefault);

        _manager.RegisterFromSettings();

        var text = File.ReadAllText(log);
        Assert.Equal(1, Count(text, _env.Services.Loc.Format("Error_HotkeyTaken", AreaDefault)));
        Assert.Equal(1, Count(text, _env.Services.Loc.Format("Error_HotkeyTaken", FullDefault)));
        Assert.Equal(AreaDefault, _env.Services.Settings.AreaHotkey);
        Assert.Equal(FullDefault, _env.Services.Settings.FullScreenHotkey);
    }

    [Fact]
    public void TryAssign_ok_registers_saves_and_raises_changed()
    {
        _manager.RegisterFromSettings();

        var result = _manager.TryAssign(HotkeyId.CaptureArea, CtrlShiftQ);

        Assert.Equal(HotkeyAssignResult.Ok, result);
        Assert.Equal(CtrlShiftQ, _env.Hotkeys.Registered[HotkeyId.CaptureArea]);
        Assert.Equal(CtrlShiftQ, _env.Services.Settings.AreaHotkey);
        Assert.Equal(CtrlShiftQ, _env.Saved.AreaHotkey);
        Assert.Equal(FullDefault, _env.Saved.FullScreenHotkey);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void TryAssign_accepts_a_function_key_alone()
    {
        Assert.Equal(HotkeyAssignResult.Ok, _manager.TryAssign(HotkeyId.CaptureFullScreen, new Hotkey(HotkeyModifiers.None, "F9")));
        Assert.Equal("F9", _env.Saved.FullScreenHotkey?.Key);
    }

    [Fact]
    public void TryAssign_invalid_changes_nothing()
    {
        _manager.RegisterFromSettings();

        var result = _manager.TryAssign(HotkeyId.CaptureArea, new Hotkey(HotkeyModifiers.None, "A"));

        Assert.Equal(HotkeyAssignResult.Invalid, result);
        Assert.Equal(AreaDefault, _env.Hotkeys.Registered[HotkeyId.CaptureArea]);
        Assert.Equal(AreaDefault, _env.Services.Settings.AreaHotkey);
        Assert.False(File.Exists(_env.SettingsPath));
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void TryAssign_the_other_ids_hotkey_is_duplicate()
    {
        _manager.RegisterFromSettings();
        _env.Hotkeys.RegisterCalls.Clear();

        var result = _manager.TryAssign(HotkeyId.CaptureArea, FullDefault);

        Assert.Equal(HotkeyAssignResult.Duplicate, result);
        Assert.Empty(_env.Hotkeys.RegisterCalls);
        Assert.Equal(AreaDefault, _env.Hotkeys.Registered[HotkeyId.CaptureArea]);
        Assert.Equal(AreaDefault, _env.Services.Settings.AreaHotkey);
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void TryAssign_own_current_hotkey_is_not_duplicate()
    {
        _manager.RegisterFromSettings();

        Assert.Equal(HotkeyAssignResult.Ok, _manager.TryAssign(HotkeyId.CaptureArea, AreaDefault));
        Assert.Equal(AreaDefault, _env.Hotkeys.Registered[HotkeyId.CaptureArea]);
    }

    [Fact]
    public void TryAssign_taken_restores_previous_registration_and_keeps_settings()
    {
        _manager.RegisterFromSettings();
        _env.Hotkeys.TakenByOthers.Add(CtrlShiftQ);

        var result = _manager.TryAssign(HotkeyId.CaptureArea, CtrlShiftQ);

        Assert.Equal(HotkeyAssignResult.TakenByOtherApp, result);
        Assert.Equal(AreaDefault, _env.Hotkeys.Registered[HotkeyId.CaptureArea]);
        Assert.Equal(AreaDefault, _env.Services.Settings.AreaHotkey);
        Assert.False(File.Exists(_env.SettingsPath));
        Assert.Equal(0, _changed);
    }

    [Fact]
    public void TryAssign_taken_with_no_previous_hotkey_leaves_it_cleared()
    {
        _env.Services.Settings = _env.Services.Settings with { AreaHotkey = null };
        _manager.RegisterFromSettings();
        _env.Hotkeys.TakenByOthers.Add(CtrlShiftQ);

        Assert.Equal(HotkeyAssignResult.TakenByOtherApp, _manager.TryAssign(HotkeyId.CaptureArea, CtrlShiftQ));
        Assert.False(_env.Hotkeys.Registered.ContainsKey(HotkeyId.CaptureArea));
        Assert.Null(_env.Services.Settings.AreaHotkey);
    }

    [Fact]
    public void TryAssign_null_unregisters_saves_and_raises_changed()
    {
        _manager.RegisterFromSettings();

        var result = _manager.TryAssign(HotkeyId.CaptureFullScreen, null);

        Assert.Equal(HotkeyAssignResult.Ok, result);
        Assert.False(_env.Hotkeys.Registered.ContainsKey(HotkeyId.CaptureFullScreen));
        Assert.Null(_env.Saved.FullScreenHotkey);
        Assert.Equal(AreaDefault, _env.Saved.AreaHotkey);
        Assert.Equal(1, _changed);
    }

    [Fact]
    public void Cleared_hotkey_can_be_assigned_to_the_other_id()
    {
        _manager.RegisterFromSettings();
        _manager.TryAssign(HotkeyId.CaptureFullScreen, null);

        Assert.Equal(HotkeyAssignResult.Ok, _manager.TryAssign(HotkeyId.CaptureArea, FullDefault));
    }

    [Fact]
    public void Triggered_reraises_pressed_unless_suspended()
    {
        var triggered = new List<HotkeyId>();
        _manager.Triggered += (_, id) => triggered.Add(id);

        _env.Hotkeys.Press(HotkeyId.CaptureArea);
        _manager.Suspended = true;
        _env.Hotkeys.Press(HotkeyId.CaptureFullScreen);
        _manager.Suspended = false;
        _env.Hotkeys.Press(HotkeyId.CaptureFullScreen);

        Assert.Equal([HotkeyId.CaptureArea, HotkeyId.CaptureFullScreen], triggered);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}

[CollectionDefinition("FileLog", DisableParallelization = true)]
public sealed class FileLogCollection;
