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

    [Fact]
    public void Clipboard_TrySetImage_PutsPngAndDib()
    {
        bool ok = false, dib = false, png = false;
        var t = new Thread(() =>
        {
            using var bmp = new SKBitmap(7, 5);
            bmp.Erase(SKColors.Red);
            ok = new Win32ClipboardImageService().TrySetImage(bmp);
            dib = IsClipboardFormatAvailable(CF_DIB);
            png = IsClipboardFormatAvailable(RegisterClipboardFormatW("PNG"));
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        Assert.True(ok);
        Assert.True(dib);
        Assert.True(png);
    }
}
