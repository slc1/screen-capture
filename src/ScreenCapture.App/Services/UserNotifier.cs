using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ScreenCapture.App.Common;
using ScreenCapture.Core.Diagnostics;

namespace ScreenCapture.App.Services;

public sealed class UserNotifier
{
    public void ShowError(string message)
    {
        FileLog.Error(message);
        if (Dispatcher.UIThread.CheckAccess()) Show(message);
        else Dispatcher.UIThread.Post(() => Show(message));
    }

    /// <summary>Shows the message centred on the active app window (else the primary screen) and in front.</summary>
    public static MessageWindow? Show(string message)
    {
        try
        {
            var window = new MessageWindow(message, ActiveAppWindow());
            window.Show();
            NativeForeground.Bring(window);
            return window;
        }
        catch (Exception ex)
        {
            FileLog.Error("Could not show message window", ex);
            return null;
        }
    }

    private static Window? ActiveAppWindow() =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows
            .FirstOrDefault(w => w.IsVisible && w.IsActive && w is not MessageWindow);
}
