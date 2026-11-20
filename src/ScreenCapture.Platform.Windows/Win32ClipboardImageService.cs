using ScreenCapture.Core.Export;
using SkiaSharp;

namespace ScreenCapture.Platform.Windows;

public sealed class Win32ClipboardImageService : IClipboardImageService
{
    public string? LastError { get; private set; }

    public bool TrySetImage(SKBitmap image)
    {
        LastError = null;
        try
        {
            var dib = BuildDib(image);
            var png = ImageExporter.Encode(image, ImageFormatKind.Png, 100);
            var pngFormat = NativeMethods.RegisterClipboardFormatW("PNG");
            if (pngFormat == 0)
            {
                LastError = Win32Clipboard.Error("RegisterClipboardFormat");
                return false;
            }

            LastError = Win32Clipboard.Replace((NativeMethods.CF_DIB, dib, "CF_DIB"), (pngFormat, png, "PNG"));
            return LastError is null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
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
}
