using System.Runtime.InteropServices;
using Avalonia.Controls;

namespace ScreenCapture.App.Common;

/// <summary>
/// Brings a window to the foreground despite Windows' foreground lock (a hotkey or tray click does not always
/// give this process the right to take focus, so <c>Activate()</c> alone can leave keyboard input elsewhere).
/// </summary>
internal static class NativeForeground
{
    /// <summary>Activates the window through Avalonia and forces it to the foreground; never throws.</summary>
    public static void Bring(Window window)
    {
        window.Activate();
        if (window.TryGetPlatformHandle()?.Handle is not { } hwnd || hwnd == IntPtr.Zero)
            return;

        try
        {
            Force(hwnd);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Not on a desktop Windows user32; Activate() above is the best we can do.
        }
    }

    private static void Force(IntPtr hwnd)
    {
        var foreground = GetForegroundWindow();
        if (foreground == hwnd)
            return;

        var thisThread = GetCurrentThreadId();
        // Attaching to a hung window's input queue can block this thread until that app responds.
        var foregroundThread = foreground == IntPtr.Zero || IsHungAppWindow(foreground)
            ? 0u
            : GetWindowThreadProcessId(foreground, out _);
        var attached = foregroundThread != 0 && foregroundThread != thisThread
            && AttachThreadInput(thisThread, foregroundThread, true);
        try
        {
            BringWindowToTop(hwnd);
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
        }
        finally
        {
            if (attached)
                AttachThreadInput(thisThread, foregroundThread, false);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsHungAppWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hwnd);
}
