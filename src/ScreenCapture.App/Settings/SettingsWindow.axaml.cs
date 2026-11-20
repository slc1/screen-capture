using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ScreenCapture.App.Common;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Hotkeys;

namespace ScreenCapture.App.Settings;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _current;

    private readonly AppServices? _services;
    private readonly HotkeyManager? _hotkeys;
    private readonly SettingsViewModel? _viewModel;

    public SettingsWindow() => InitializeComponent();

    private SettingsWindow(AppServices services, HotkeyManager hotkeys) : this()
    {
        _services = services;
        _hotkeys = hotkeys;
        _viewModel = new SettingsViewModel(services, hotkeys);
        DataContext = _viewModel;

        foreach (var box in new[] { AreaBox, FullScreenBox, RecognizeBox })
        {
            box.HotkeyCaptured += OnHotkeyCaptured;
            box.RecordingChanged += (_, _) => UpdateSuspended();
        }

        // A global hotkey's own keystroke never reaches the focused box, so it is fed in from WM_HOTKEY.
        services.Hotkeys.Pressed += OnGlobalHotkeyPressed;
        Deactivated += (_, _) => FocusManager?.ClearFocus();
    }

    public static void ShowOrActivate(AppServices services, HotkeyManager hotkeys)
    {
        if (_current is { } open)
        {
            if (open.WindowState == WindowState.Minimized) open.WindowState = WindowState.Normal;
            NativeForeground.Bring(open);
            return;
        }

        var window = new SettingsWindow(services, hotkeys);
        _current = window;
        window.Show();
        NativeForeground.Bring(window);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (ReferenceEquals(_current, this)) _current = null;
        if (_services is not null) _services.Hotkeys.Pressed -= OnGlobalHotkeyPressed;
        if (_hotkeys is not null) _hotkeys.Suspended = false;
        _viewModel?.Dispose();
        base.OnClosed(e);
    }

    private HotkeyBox? RecordingBox =>
        new[] { AreaBox, FullScreenBox, RecognizeBox }.FirstOrDefault(b => b.IsRecording);

    private void UpdateSuspended()
    {
        if (_hotkeys is not null) _hotkeys.Suspended = RecordingBox is not null;
    }

    private void OnHotkeyCaptured(object? sender, Hotkey hotkey)
    {
        if (sender is not HotkeyBox box) return;
        _viewModel?.Assign(box.Id, hotkey);
        FocusManager?.ClearFocus();
    }

    private void OnGlobalHotkeyPressed(object? sender, HotkeyId id)
    {
        if (RecordingBox is { } box && _hotkeys?.Get(id) is { } hotkey) box.Capture(hotkey);
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || _services is null) return;
        try
        {
            var start = await StorageProvider.TryGetFolderFromPathAsync(_viewModel.SaveFolder);
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = _services.Loc["Settings_SaveFolder"],
                AllowMultiple = false,
                SuggestedStartLocation = start,
            });
            if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } path) _viewModel.SaveFolder = path;
        }
        catch (Exception ex)
        {
            FileLog.Error("Folder picker failed", ex);
            _services.Notifier.ShowError(_services.Loc.Format("Error_Unexpected", Program.LogPath));
        }
    }
}
