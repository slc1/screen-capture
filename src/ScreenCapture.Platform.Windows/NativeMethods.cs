using System.Runtime.InteropServices;

namespace ScreenCapture.Platform.Windows;

internal static unsafe partial class NativeMethods
{
    public const uint SRCCOPY = 0x00CC0020;
    public const uint CAPTUREBLT = 0x40000000;
    public const uint MONITORINFOF_PRIMARY = 1;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int MDT_EFFECTIVE_DPI = 0;
    public const uint DIB_RGB_COLORS = 0;
    public const uint BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFOEXW
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        public fixed char szDevice[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    public delegate int MonitorEnumProc(nint hMonitor, nint hdcMonitor, RECT* lprcMonitor, nint dwData);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfoW(nint hMonitor, ref MONITORINFOEXW lpmi);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateDIBSection(nint hdc, in BITMAPINFOHEADER pbmi, uint usage, out nint ppvBits, nint hSection, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(nint hdcDest, int x, int y, int cx, int cy, nint hdcSrc, int x1, int y1, uint rop);
}
