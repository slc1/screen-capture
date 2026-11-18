using System.Runtime.InteropServices;
using Avalonia;

namespace ScreenCapture.App.Common;

internal static class NativeCursor
{
    /// <summary>Cursor position in physical virtual-desktop pixels, or null if unavailable.</summary>
    public static PixelPoint? Position() =>
        GetCursorPos(out var p) ? new PixelPoint(p.X, p.Y) : null;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
