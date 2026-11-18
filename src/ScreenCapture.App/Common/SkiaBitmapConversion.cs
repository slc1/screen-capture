using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Common;

internal static class SkiaBitmapConversion
{
    /// <summary>Copies the pixels into a new Avalonia bitmap at 96 DPI, so 1 image px == 1 DIP unit of the bitmap.</summary>
    public static Bitmap ToAvalonia(SKBitmap source)
    {
        if (source.ColorType != SKColorType.Bgra8888)
        {
            using var converted = source.Copy(SKColorType.Bgra8888)
                ?? throw new InvalidOperationException($"Cannot convert {source.ColorType} to BGRA8888.");
            return ToAvalonia(converted);
        }

        var alpha = source.AlphaType switch
        {
            SKAlphaType.Opaque => AlphaFormat.Opaque,
            SKAlphaType.Unpremul => AlphaFormat.Unpremul,
            _ => AlphaFormat.Premul,
        };
        return new Bitmap(PixelFormat.Bgra8888, alpha, source.GetPixels(),
            new PixelSize(source.Width, source.Height), new Vector(96, 96), source.RowBytes);
    }
}
