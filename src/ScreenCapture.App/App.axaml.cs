using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
    private TrayController? _tray;

    public static AppServices? Services { get; private set; }

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
            var loaded = store.Load();
            loc.SetLanguage(loaded.Settings.Language);

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

            _tray = new TrayController(
                services,
                captureArea: () => FileLog.Info("Capture area requested"),
                captureFullScreen: () => FileLog.Info("Capture full screen requested"),
                openSettings: () => FileLog.Info("Settings requested"));

            if (loaded.WasCorrupt)
                services.Notifier.ShowError(loc["Error_SettingsCorrupt"]);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
