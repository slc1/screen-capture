using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ScreenCapture.App.Capture;
using ScreenCapture.App.Common;
using ScreenCapture.App.Editor;
using ScreenCapture.App.Services;
using ScreenCapture.App.Settings;
using ScreenCapture.App.Tray;
using ScreenCapture.Core.Capture;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Hotkeys;
using ScreenCapture.Core.Localization;
using ScreenCapture.Core.Settings;
using ScreenCapture.Platform.Windows;

namespace ScreenCapture.App;

public partial class App : Application
{
    public static AppServices? Services { get; private set; }

    public static TrayController? Tray { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            try
            {
                Start();
            }
            catch (Exception ex)
            {
                FileLog.Error("Startup failed", ex);
                Tray?.Dispose();
                Services?.Hotkeys.Dispose();
                // Without a tray icon the user could not exit, so closing the message ends the app.
                var message = UserNotifier.Show(Loc.Instance.Format("Error_Unexpected", Program.LogPath));
                if (message is null) desktop.Shutdown(1);
                else message.Closed += (_, _) => desktop.Shutdown(1);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        FileLog.Error("Unhandled UI exception", e.Exception);
        var message = Loc.Instance.Format("Error_Unexpected", Program.LogPath);
        if (Services is { } services) services.Notifier.ShowError(message);
        else UserNotifier.Show(message);
    }

    private static void Start()
    {
        var loc = Loc.Instance;
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var store = new SettingsStore(Program.SettingsPath, () => AppSettings.CreateDefault(
            pictures, Loc.ResolveInitialLanguage(CultureInfo.CurrentUICulture.Name, Loc.AvailableLanguages())));
        var firstRun = !File.Exists(Program.SettingsPath);
        var loaded = store.Load();
        loc.SetLanguage(loaded.Settings.Language);
        if (firstRun || loaded.WasCorrupt)
        {
            try
            {
                store.Save(loaded.Settings);
            }
            catch (Exception ex)
            {
                FileLog.Error("Could not save initial settings", ex);
            }
        }

        var services = new AppServices(
            loc,
            store,
            loaded.Settings,
            new GdiScreenCapturer(),
            new Win32HotkeyService(a => Dispatcher.UIThread.Post(a)),
            new Win32ClipboardImageService(),
            new RegistryAutostartService("ScreenCapture", Environment.ProcessPath ?? ""),
            new UserNotifier(),
            new CaptureGate(),
            new TesseractOcrService(TesseractOcrService.DefaultRoot),
            new Win32ClipboardTextService());
        Services = services;

        var recognizer = TextRecognizer.Create(services);
        var capture = new CaptureCoordinator(services, image =>
        {
            var editor = new EditorWindow(image, services, recognizer);
            try
            {
                editor.Show();
                NativeForeground.Bring(editor);
                return editor;
            }
            catch
            {
                editor.ReleaseImages();
                throw;
            }
        }, selection => _ = recognizer.RecognizeAsync(selection));

        var hotkeys = new HotkeyManager(services.Hotkeys, services);
        hotkeys.Triggered += (_, id) =>
        {
            FileLog.Info($"Hotkey {id}");
            var timing = new CaptureTiming($"{id}, hotkey");
            switch (id)
            {
                case HotkeyId.CaptureArea:
                    capture.StartArea(timing);
                    break;
                case HotkeyId.CaptureFullScreen:
                    capture.StartFullScreen(timing);
                    break;
                case HotkeyId.RecognizeArea:
                    capture.StartRecognizeArea(timing);
                    break;
            }
        };

        Tray = new TrayController(
            services,
            captureArea: capture.StartArea,
            captureFullScreen: capture.StartFullScreen,
            recognizeArea: capture.StartRecognizeArea,
            openSettings: () => SettingsWindow.ShowOrActivate(services, hotkeys));
        hotkeys.HotkeysChanged += (_, _) => Tray?.Refresh();

        if (loaded.WasCorrupt)
            Dispatcher.UIThread.Post(() => services.Notifier.ShowError(loc["Error_SettingsCorrupt"]));

        hotkeys.RegisterFromSettings();
        Dispatcher.UIThread.Post(OverlayWindow.Prewarm, DispatcherPriority.Background);
    }
}
