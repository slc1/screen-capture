using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Export;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Core.Localization;
using ScreenCapture.Core.Settings;

namespace ScreenCapture.App.Settings;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly AppServices _services;
    private readonly HotkeyManager _hotkeys;
    private readonly Dictionary<HotkeyId, Func<string>> _errors = new();
    private LanguageOption _selectedLanguage;
    private bool _startWithWindows;

    public SettingsViewModel(AppServices services, HotkeyManager hotkeys)
    {
        _services = services;
        _hotkeys = hotkeys;
        Languages = Loc.AvailableLanguages();
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == services.Loc.Language) ?? Languages[0];

        _startWithWindows = ReadAutostart(services.Settings.StartWithWindows);
        if (_startWithWindows != services.Settings.StartWithWindows)
            Persist(s => s with { StartWithWindows = _startWithWindows });

        _hotkeys.HotkeysChanged += OnHotkeysChanged;
        _services.Loc.PropertyChanged += OnLocChanged;
    }

    public IReadOnlyList<LanguageOption> Languages { get; }

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || value == _selectedLanguage) return;
            _selectedLanguage = value;
            OnPropertyChanged();
            _services.Loc.SetLanguage(value.Code);
            Persist(s => s with { Language = value.Code });
        }
    }

    public Hotkey? AreaHotkey => _hotkeys.Get(HotkeyId.CaptureArea);

    public Hotkey? FullScreenHotkey => _hotkeys.Get(HotkeyId.CaptureFullScreen);

    public string? AreaError => _errors.GetValueOrDefault(HotkeyId.CaptureArea)?.Invoke();

    public string? FullScreenError => _errors.GetValueOrDefault(HotkeyId.CaptureFullScreen)?.Invoke();

    public bool IsPng
    {
        get => _services.Settings.DefaultFormat == ImageFormatKind.Png;
        set
        {
            if (value) SetFormat(ImageFormatKind.Png);
        }
    }

    public bool IsJpg
    {
        get => _services.Settings.DefaultFormat == ImageFormatKind.Jpg;
        set
        {
            if (value) SetFormat(ImageFormatKind.Jpg);
        }
    }

    public int JpgQuality
    {
        get => _services.Settings.JpgQuality;
        set
        {
            value = Math.Clamp(value, 1, 100);
            if (value == JpgQuality) return;
            Persist(s => s with { JpgQuality = value });
            OnPropertyChanged();
        }
    }

    public string SaveFolder
    {
        get => _services.Settings.SaveFolder;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value == SaveFolder) return;
            Persist(s => s with { SaveFolder = value });
            OnPropertyChanged();
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value == _startWithWindows) return;
            try
            {
                _services.Autostart.SetEnabled(value);
                _startWithWindows = value;
            }
            catch (Exception ex)
            {
                FileLog.Error("Could not change autostart", ex);
                _services.Notifier.ShowError(_services.Loc["Error_AutostartFailed"]);
                _startWithWindows = ReadAutostart(_startWithWindows);
                // The CheckBox ignores a change notification raised while it is still pushing its own value.
                Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(StartWithWindows)));
            }

            if (_startWithWindows != _services.Settings.StartWithWindows)
                Persist(s => s with { StartWithWindows = _startWithWindows });
            OnPropertyChanged();
        }
    }

    public void Assign(HotkeyId id, Hotkey? hotkey)
    {
        var result = _hotkeys.TryAssign(id, hotkey);
        var combo = hotkey?.ToString() ?? "";
        Func<string>? error = result switch
        {
            HotkeyAssignResult.Ok => null,
            HotkeyAssignResult.Invalid => () => _services.Loc["Error_HotkeyInvalid"],
            HotkeyAssignResult.Duplicate => () => _services.Loc.Format("Error_HotkeyDuplicate", combo),
            _ => () => _services.Loc.Format("Error_HotkeyTaken", combo),
        };
        if (error is null) _errors.Remove(id);
        else _errors[id] = error;
        RaiseErrorChanged(id);
    }

    [RelayCommand]
    private void Reset(HotkeyId id) => Assign(id, HotkeyRules.DefaultFor(id));

    [RelayCommand]
    private void Clear(HotkeyId id) => Assign(id, null);

    public void Dispose()
    {
        _hotkeys.HotkeysChanged -= OnHotkeysChanged;
        _services.Loc.PropertyChanged -= OnLocChanged;
    }

    private void SetFormat(ImageFormatKind format)
    {
        if (_services.Settings.DefaultFormat == format) return;
        Persist(s => s with { DefaultFormat = format });
        OnPropertyChanged(nameof(IsPng));
        OnPropertyChanged(nameof(IsJpg));
    }

    private bool ReadAutostart(bool fallback)
    {
        try
        {
            return _services.Autostart.IsEnabled();
        }
        catch (Exception ex)
        {
            FileLog.Error("Could not read autostart state", ex);
            return fallback;
        }
    }

    private void Persist(Func<AppSettings, AppSettings> change)
    {
        try
        {
            _services.Settings = change(_services.Settings);
        }
        catch (Exception ex)
        {
            FileLog.Error("Could not save settings", ex);
            _services.Notifier.ShowError(_services.Loc.Format("Error_Unexpected", Program.LogPath));
        }
    }

    private void RaiseErrorChanged(HotkeyId id) =>
        OnPropertyChanged(id == HotkeyId.CaptureArea ? nameof(AreaError) : nameof(FullScreenError));

    private void OnHotkeysChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(AreaHotkey));
        OnPropertyChanged(nameof(FullScreenHotkey));
    }

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e)
    {
        RaiseErrorChanged(HotkeyId.CaptureArea);
        RaiseErrorChanged(HotkeyId.CaptureFullScreen);
    }
}
