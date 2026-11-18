using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Threading;
using ScreenCapture.App.Common;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.App.Tray;

public sealed class TrayController : IDisposable
{
    private readonly AppServices _services;
    private readonly Action _captureArea;
    private readonly Action _captureFullScreen;
    private readonly Action _openSettings;
    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _areaItem = new();
    private readonly NativeMenuItem _fullItem = new();
    private readonly NativeMenuItem _settingsItem = new();
    private readonly NativeMenuItem _exitItem = new();
    private TrayMenuWindow? _menuWindow;
    private bool _disposed;

    public TrayController(AppServices services, Action captureArea, Action captureFullScreen, Action openSettings)
    {
        _services = services;
        _captureArea = () => AfterMenuCloses(captureArea);
        _captureFullScreen = () => AfterMenuCloses(captureFullScreen);
        _openSettings = openSettings;

        _areaItem.Click += (_, _) => _captureArea();
        _fullItem.Click += (_, _) => _captureFullScreen();
        _settingsItem.Click += (_, _) => _openSettings();
        _exitItem.Click += (_, _) => Exit();

        var menu = new NativeMenu();
        menu.Add(_areaItem);
        menu.Add(_fullItem);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(_settingsItem);
        menu.Add(_exitItem);

        _icon = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://ScreenCapture.App/Assets/tray.ico"))),
            Menu = menu,
            IsVisible = true,
        };
        _icon.Clicked += (_, _) => ShowMenuWindow();
        TrayIcon.SetIcons(Application.Current!, [_icon]);

        _services.Loc.PropertyChanged += OnLocChanged;
        Refresh();
    }

    public void Refresh()
    {
        if (_disposed) return;
        var loc = _services.Loc;
        _icon.ToolTipText = loc["App_Name"];
        _areaItem.Header = WithHotkey(loc["Tray_CaptureArea"], _services.Settings.AreaHotkey);
        _fullItem.Header = WithHotkey(loc["Tray_CaptureFullScreen"], _services.Settings.FullScreenHotkey);
        _settingsItem.Header = loc["Tray_Settings"];
        _exitItem.Header = loc["Tray_Exit"];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _services.Loc.PropertyChanged -= OnLocChanged;
        _menuWindow?.Close();
        _icon.IsVisible = false;
        _icon.Dispose();
    }

    /// <summary>
    /// Screen capture reads the composited desktop, which still shows the just-closed menu until DWM
    /// composes another frame, so the capture waits for pending UI work plus one DWM frame.
    /// </summary>
    private static void AfterMenuCloses(Action capture) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (!NativeDwm.Flush()) FileLog.Info("DwmFlush failed before capture");
            capture();
        }, DispatcherPriority.Background);

    private static string WithHotkey(string text, Hotkey? hotkey) =>
        hotkey is null ? text : $"{text} ({hotkey})";

    private void OnLocChanged(object? sender, PropertyChangedEventArgs e) =>
        Dispatcher.UIThread.Post(Refresh);

    private void ShowMenuWindow()
    {
        _menuWindow?.Close();
        var window = new TrayMenuWindow(
            _services.Settings.AreaHotkey?.ToString(),
            _services.Settings.FullScreenHotkey?.ToString(),
            _captureArea,
            _captureFullScreen,
            _openSettings,
            Exit);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_menuWindow, window)) _menuWindow = null;
        };
        _menuWindow = window;
        window.Show();
    }

    private void Exit()
    {
        FileLog.Info("Exit requested");
        _services.Hotkeys.Unregister(HotkeyId.CaptureArea);
        _services.Hotkeys.Unregister(HotkeyId.CaptureFullScreen);
        _services.Hotkeys.Dispose();
        Dispose();
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }
}
