using SkiaSharp;

namespace ScreenCapture.Core.Export;

public enum ImageFormatKind
{
    Png,
    Jpg,
}

public static class ImageExporter
{
    public static byte[] Encode(SKBitmap image, ImageFormatKind format, int jpgQuality)
    {
        if (format == ImageFormatKind.Png)
        {
            using var png = SKImage.FromBitmap(image);
            using var data = png.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        // JPEG has no alpha: flatten onto white so transparent areas do not turn black.
        using var flat = new SKBitmap(image.Width, image.Height);
        using (var canvas = new SKCanvas(flat))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(image, 0, 0);
        }
        using var img = SKImage.FromBitmap(flat);
        using var jpg = img.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(jpgQuality, 1, 100));
        return jpg.ToArray();
    }

    public static void Save(SKBitmap image, string path, ImageFormatKind format, int jpgQuality)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, Encode(image, format, jpgQuality));
    }

    public static string DefaultFileName(DateTime now, ImageFormatKind format) =>
        $"Screenshot {now:yyyy-MM-dd HH-mm-ss}.{(format == ImageFormatKind.Jpg ? "jpg" : "png")}";

    public static ImageFormatKind FormatFromPath(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            ? ImageFormatKind.Jpg
            : ImageFormatKind.Png;
    }
}
