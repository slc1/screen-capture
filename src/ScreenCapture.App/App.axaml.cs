using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ScreenCapture.App.Capture;
using ScreenCapture.App.Editor;
using ScreenCapture.App.Services;
using ScreenCapture.App.Tray;
using ScreenCapture.Core.Capture;
using ScreenCapture.Core.Diagnostics;
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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
                new CaptureGate());
            Services = services;

            var capture = new CaptureCoordinator(services, image => new EditorWindow(image, services).Show());

            Tray = new TrayController(
                services,
                captureArea: capture.StartArea,
                captureFullScreen: capture.StartFullScreen,
                openSettings: () => FileLog.Info("Settings requested"));

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                e.Handled = true;
                FileLog.Error("Unhandled UI exception", e.Exception);
                services.Notifier.ShowError(loc.Format("Error_Unexpected", Program.LogPath));
            };

            if (loaded.WasCorrupt)
                Dispatcher.UIThread.Post(() => services.Notifier.ShowError(loc["Error_SettingsCorrupt"]));
        }

        base.OnFrameworkInitializationCompleted();
    }
}
