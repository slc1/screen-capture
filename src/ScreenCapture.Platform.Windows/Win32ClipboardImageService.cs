using System.Runtime.InteropServices;
using ScreenCapture.Core.Export;
using SkiaSharp;

namespace ScreenCapture.Platform.Windows;

public sealed class Win32ClipboardImageService : IClipboardImageService
{
    private const int OpenAttempts = 10;
    private const int OpenRetryDelayMs = 50;

    public string? LastError { get; private set; }

    public bool TrySetImage(SKBitmap image)
    {
        LastError = null;
        try
        {
            var dib = BuildDib(image);
            var png = ImageExporter.Encode(image, ImageFormatKind.Png, 100);
            var pngFormat = NativeMethods.RegisterClipboardFormatW("PNG");
            if (pngFormat == 0) return Fail("RegisterClipboardFormat");

            if (!OpenWithRetry()) return Fail("OpenClipboard");
            try
            {
                // Each Fail runs before the finally, so CloseClipboard cannot overwrite the error code.
                if (!NativeMethods.EmptyClipboard()) return Fail("EmptyClipboard");
                return SetData(NativeMethods.CF_DIB, dib, "CF_DIB") && SetData(pngFormat, png, "PNG");
            }
            finally
            {
                NativeMethods.CloseClipboard();
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private bool Fail(string step)
    {
        LastError = $"{step}: Win32 error {Marshal.GetLastPInvokeError()}";
        return false;
    }

    private static bool OpenWithRetry()
    {
        for (var i = 0; i < OpenAttempts; i++)
        {
            if (NativeMethods.OpenClipboard(0)) return true;
            if (i < OpenAttempts - 1) Thread.Sleep(OpenRetryDelayMs);
        }
        return false;
    }

    private static unsafe byte[] BuildDib(SKBitmap image)
    {
        using var bgra = image.Copy(SKColorType.Bgra8888) ?? throw new InvalidOperationException("Bitmap conversion failed");
        var w = bgra.Width;
        var h = bgra.Height;
        var stride = w * 4;
        var header = sizeof(NativeMethods.BITMAPINFOHEADER);
        var buf = new byte[header + stride * h];

        fixed (byte* p = buf)
        {
            *(NativeMethods.BITMAPINFOHEADER*)p = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)header,
                biWidth = w,
                biHeight = h,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = NativeMethods.BI_RGB,
                biSizeImage = (uint)(stride * h),
            };
        }

        var src = bgra.GetPixelSpan();
        for (var y = 0; y < h; y++)
        {
            var srcRow = src.Slice(y * bgra.RowBytes, stride);
            var dst = buf.AsSpan(header + (h - 1 - y) * stride, stride);
            srcRow.CopyTo(dst);
            for (var x = 3; x < stride; x += 4) dst[x] = 255;
        }
        return buf;
    }

    private bool SetData(uint format, byte[] data, string name)
    {
        var h = NativeMethods.GlobalAlloc(NativeMethods.GMEM_MOVEABLE, (nuint)data.Length);
        if (h == 0) return Fail($"GlobalAlloc for {name}");
        var ptr = NativeMethods.GlobalLock(h);
        if (ptr == 0)
        {
            Fail($"GlobalLock for {name}");
            NativeMethods.GlobalFree(h);
            return false;
        }
        Marshal.Copy(data, 0, ptr, data.Length);
        NativeMethods.GlobalUnlock(h);

        if (NativeMethods.SetClipboardData(format, h) == 0)
        {
            Fail($"SetClipboardData {name}");
            NativeMethods.GlobalFree(h);
            return false;
        }
        return true;
    }
}
