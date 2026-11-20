using System.Runtime.InteropServices;

namespace ScreenCapture.Platform.Windows;

internal static class Win32Clipboard
{
    private const int OpenAttempts = 10;
    private const int OpenRetryDelayMs = 50;

    /// <summary>
    /// Replaces the clipboard contents with all <paramref name="items"/>. Returns null on success, otherwise the
    /// failing step with its Win32 error code.
    /// </summary>
    public static string? Replace(params (uint Format, byte[] Data, string Name)[] items)
    {
        if (!OpenWithRetry()) return Error("OpenClipboard");
        try
        {
            // Each Error runs before the finally, so CloseClipboard cannot overwrite the error code.
            if (!NativeMethods.EmptyClipboard()) return Error("EmptyClipboard");
            foreach (var (format, data, name) in items)
            {
                if (SetData(format, data, name) is { } error) return error;
            }
            return null;
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public static string Error(string step) => $"{step}: Win32 error {Marshal.GetLastPInvokeError()}";

    private static bool OpenWithRetry()
    {
        for (var i = 0; i < OpenAttempts; i++)
        {
            if (NativeMethods.OpenClipboard(0)) return true;
            if (i < OpenAttempts - 1) Thread.Sleep(OpenRetryDelayMs);
        }
        return false;
    }

    private static string? SetData(uint format, byte[] data, string name)
    {
        var h = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (nuint)data.Length);
        if (h == 0) return Error($"GlobalAlloc for {name}");
        var ptr = NativeMethods.GlobalLock(h);
        if (ptr == 0)
        {
            var error = Error($"GlobalLock for {name}");
            NativeMethods.GlobalFree(h);
            return error;
        }
        Marshal.Copy(data, 0, ptr, data.Length);
        NativeMethods.GlobalUnlock(h);

        if (NativeMethods.SetClipboardData(format, h) == 0)
        {
            var error = Error($"SetClipboardData {name}");
            NativeMethods.GlobalFree(h);
            return error;
        }
        return null;
    }
}
