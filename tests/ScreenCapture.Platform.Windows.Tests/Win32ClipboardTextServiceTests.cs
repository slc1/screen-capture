using System.Runtime.InteropServices;
using ScreenCapture.Platform.Windows;

namespace ScreenCapture.Platform.Windows.Tests;

public class Win32ClipboardTextServiceTests
{
    private const uint CF_UNICODETEXT = 13;

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

    [Fact]
    public void TrySetText_PutsUnicodeText()
    {
        const string expected = "Привіт, ґанок!\r\nSecond line: Ctrl+Alt+7";
        bool ok = false;
        string? lastError = "unset", read = null;
        Exception? error = null;
        var t = new Thread(() =>
        {
            try
            {
                var service = new Win32ClipboardTextService();
                ok = service.TrySetText(expected);
                lastError = service.LastError;
                Assert.True(OpenClipboard(0));
                try
                {
                    var h = GetClipboardData(CF_UNICODETEXT);
                    Assert.NotEqual(0, h);
                    var p = GlobalLock(h);
                    try { read = Marshal.PtrToStringUni(p); }
                    finally { GlobalUnlock(h); }
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
        Assert.Equal(expected, read);
    }
}
