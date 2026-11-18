using System.Runtime.InteropServices;
using ScreenCapture.Platform.Windows;
using SkiaSharp;

namespace ScreenCapture.Platform.Windows.Tests;

public class Win32ClipboardImageServiceTests
{
    private const uint CF_DIB = 8;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormatW(string name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern nint GetClipboardData(uint format);

    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint h);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(nint h);

    [DllImport("kernel32.dll")]
    private static extern nuint GlobalSize(nint h);

    private static readonly nint HWND_MESSAGE = -3;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint exStyle, string className, string? windowName, uint style,
        int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint hwnd);

    private static byte[] ReadClipboard(uint format)
    {
        var h = GetClipboardData(format);
        Assert.NotEqual(0, h);
        var size = (int)GlobalSize(h);
        var p = GlobalLock(h);
        try
        {
            var bytes = new byte[size];
            Marshal.Copy(p, bytes, 0, size);
            return bytes;
        }
        finally { GlobalUnlock(h); }
    }

    [Fact]
    public void Clipboard_TrySetImage_PutsPngAndDib()
    {
        bool ok = false, dib = false, png = false;
        string? lastError = "unset";
        byte[] dibBytes = [], pngBytes = [];
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                using var bmp = new SKBitmap(7, 5);
                bmp.Erase(SKColors.Blue);
                for (var x = 0; x < 7; x++) bmp.SetPixel(x, 0, SKColors.Red);

                var service = new Win32ClipboardImageService();
                ok = service.TrySetImage(bmp);
                lastError = service.LastError;
                var pngFormat = RegisterClipboardFormatW("PNG");
                dib = IsClipboardFormatAvailable(CF_DIB);
                png = IsClipboardFormatAvailable(pngFormat);
                Assert.True(OpenClipboard(0));
                try
                {
                    dibBytes = ReadClipboard(CF_DIB);
                    pngBytes = ReadClipboard(pngFormat);
                }
                finally { CloseClipboard(); }
            }
            catch (Exception e) { error = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.Null(error);
        Assert.True(ok);
        Assert.Null(lastError);
        Assert.True(dib);
        Assert.True(png);

        Assert.Equal(40, BitConverter.ToInt32(dibBytes, 0));
        Assert.Equal(7, BitConverter.ToInt32(dibBytes, 4));
        Assert.Equal(5, BitConverter.ToInt32(dibBytes, 8));
        Assert.Equal(32, BitConverter.ToUInt16(dibBytes, 14));
        // GlobalSize may round the allocation up.
        Assert.True(dibBytes.Length >= 40 + 7 * 4 * 5, $"DIB was {dibBytes.Length} bytes");
        // Bottom-up: first stored row is the image's bottom row (blue, BGRA), last stored row is red.
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, dibBytes[40..44]);
        var lastRow = 40 + 4 * 7 * 4;
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, dibBytes[lastRow..(lastRow + 4)]);

        using var decoded = SKBitmap.Decode(pngBytes);
        Assert.Equal(7, decoded.Width);
        Assert.Equal(5, decoded.Height);
        Assert.Equal(SKColors.Red, decoded.GetPixel(0, 0));
    }
    [Fact]
    public void Clipboard_TrySetImage_HeldByAnotherThread_ReportsWin32Error()
    {
        using var opened = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var holderOpened = false;
        var holder = new Thread(() =>
        {
            // The clipboard is held by a window; OpenClipboard(NULL) does not block other threads.
            var hwnd = CreateWindowExW(0, "STATIC", null, 0, 0, 0, 0, 0, HWND_MESSAGE, 0, 0, 0);
            holderOpened = hwnd != 0 && OpenClipboard(hwnd);
            opened.Set();
            release.Wait();
            if (holderOpened) CloseClipboard();
            if (hwnd != 0) DestroyWindow(hwnd);
        });
        holder.SetApartmentState(ApartmentState.STA);
        holder.Start();

        bool ok = true;
        string? lastError = null;
        Exception? error = null;
        try
        {
            opened.Wait();
            var t = new Thread(() =>
            {
                try
                {
                    using var bmp = new SKBitmap(3, 3);
                    bmp.Erase(SKColors.Blue);
                    var service = new Win32ClipboardImageService();
                    ok = service.TrySetImage(bmp);
                    lastError = service.LastError;
                }
                catch (Exception e) { error = e; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
        }
        finally
        {
            release.Set();
            holder.Join();
        }

        Assert.True(holderOpened);
        Assert.Null(error);
        Assert.False(ok);
        Assert.NotNull(lastError);
        Assert.Contains("Win32 error", lastError);
    }
}
