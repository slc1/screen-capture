using Avalonia;
using Avalonia.Threading;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Localization;

namespace ScreenCapture.App;

sealed class Program
{
    private static readonly string DataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenCapture");

    public static string SettingsPath { get; } = Path.Combine(DataFolder, "settings.json");
    public static string LogPath { get; } = Path.Combine(DataFolder, "logs", "app.log");

    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\ScreenCapture.SingleInstance", out var isFirst);
        if (!isFirst) return;

        FileLog.Init(LogPath);
        FileLog.Info("Starting");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportUnexpected(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ReportUnexpected(e.Exception);
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        FileLog.Info("Exited");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void ReportUnexpected(Exception? ex)
    {
        FileLog.Error("Unhandled exception", ex);
        try
        {
            var message = Loc.Instance.Format("Error_Unexpected", LogPath);
            Dispatcher.UIThread.Post(() => App.Services?.Notifier.ShowError(message));
        }
        catch
        {
            // The app may be going down before Avalonia is running.
        }
    }
}
