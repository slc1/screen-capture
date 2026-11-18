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

    private static void Show(string message)
    {
        try
        {
            new MessageWindow(message).Show();
        }
        catch (Exception ex)
        {
            FileLog.Error("Could not show message window", ex);
        }
    }
}
