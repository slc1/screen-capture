using System.Runtime.InteropServices;
using SkiaSharp;

namespace ScreenCapture.Core.Ocr;

/// <summary>
/// Turns a screenshot into what Tesseract reads best: dark text on white, 8-bit gray, upscaled, with a margin.
/// Small screen fonts and dark themes are recognized poorly without these steps.
/// </summary>
public static class OcrImagePrep
{
    public const int Margin = 10;

    /// <summary>Upscaling stops when the doubled image would be longer than this on a side.</summary>
    public const int MaxUpscaledSide = 8000;

    public static int ScaleFor(int width, int height) => Math.Max(width, height) * 2 <= MaxUpscaledSide ? 2 : 1;

    public static SKBitmap Prepare(SKBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Width <= 0 || source.Height <= 0)
            throw new ArgumentException("The image is empty.", nameof(source));

        using var gray = ToGray(source);
        var scale = ScaleFor(source.Width, source.Height);
        using var scaled = scale == 1 ? gray.Copy() : Upscale(gray, scale);
        return WithMargin(scaled);
    }

    /// <summary>Gray8 luminance; inverted when the mean is below 50 % so the text ends up dark on light.</summary>
    private static SKBitmap ToGray(SKBitmap source)
    {
        using var bgra = source.ColorType == SKColorType.Bgra8888 ? null : source.Copy(SKColorType.Bgra8888);
        var src = bgra ?? source;
        if (src is null || src.ColorType != SKColorType.Bgra8888)
            throw new InvalidOperationException("Could not convert the image for text recognition.");

        var w = src.Width;
        var h = src.Height;
        var gray = new SKBitmap(new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque));
        var from = src.GetPixelSpan();
        var to = Writable(gray);
        long sum = 0;
        for (var y = 0; y < h; y++)
        {
            var row = from.Slice(y * src.RowBytes, w * 4);
            var outRow = to.Slice(y * gray.RowBytes, w);
            for (var x = 0; x < w; x++)
            {
                var b = row[x * 4];
                var g = row[x * 4 + 1];
                var r = row[x * 4 + 2];
                var l = (byte)((299 * r + 587 * g + 114 * b + 500) / 1000);
                outRow[x] = l;
                sum += l;
            }
        }

        if (sum * 2 < 255L * w * h)
        {
            for (var y = 0; y < h; y++)
            {
                var outRow = to.Slice(y * gray.RowBytes, w);
                for (var x = 0; x < w; x++)
                    outRow[x] = (byte)(255 - outRow[x]);
            }
        }

        return gray;
    }

    private static SKBitmap Upscale(SKBitmap gray, int scale)
    {
        var scaled = new SKBitmap(new SKImageInfo(gray.Width * scale, gray.Height * scale, SKColorType.Gray8, SKAlphaType.Opaque));
        if (!gray.ScalePixels(scaled, SKFilterQuality.High))
        {
            scaled.Dispose();
            throw new InvalidOperationException("Could not scale the image for text recognition.");
        }

        return scaled;
    }

    private static SKBitmap WithMargin(SKBitmap image)
    {
        var w = image.Width + 2 * Margin;
        var h = image.Height + 2 * Margin;
        var result = new SKBitmap(new SKImageInfo(w, h, SKColorType.Gray8, SKAlphaType.Opaque));
        var to = Writable(result);
        to.Fill(255);
        var from = image.GetPixelSpan();
        for (var y = 0; y < image.Height; y++)
            from.Slice(y * image.RowBytes, image.Width).CopyTo(to.Slice((y + Margin) * result.RowBytes + Margin, image.Width));
        return result;
    }

    /// <summary>The bitmap owns its pixel memory, so writing through the span SkiaSharp exposes read-only is safe.</summary>
    private static Span<byte> Writable(SKBitmap bitmap)
    {
        var pixels = bitmap.GetPixelSpan();
        return MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(pixels), pixels.Length);
    }
}