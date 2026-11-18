using ScreenCapture.Core.Export;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public class ImageExporterTests
{
    private static SKBitmap Noise(int w, int h)
    {
        var rnd = new Random(42);
        var bmp = new SKBitmap(w, h);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                bmp.SetPixel(x, y, new SKColor((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256)));
        return bmp;
    }

    [Fact]
    public void Encode_Png_RoundTripsSize()
    {
        using var img = Noise(30, 20);
        var bytes = ImageExporter.Encode(img, ImageFormatKind.Png, 90);
        using var decoded = SKBitmap.Decode(bytes);
        Assert.Equal(30, decoded.Width);
        Assert.Equal(20, decoded.Height);
    }

    [Fact]
    public void Encode_Jpg_LowerQualityIsSmaller()
    {
        using var img = Noise(100, 100);
        var low = ImageExporter.Encode(img, ImageFormatKind.Jpg, 20);
        var high = ImageExporter.Encode(img, ImageFormatKind.Jpg, 95);
        Assert.True(low.Length < high.Length);
    }

    [Fact]
    public void Encode_Jpg_ClampsQuality()
    {
        using var img = Noise(20, 20);
        Assert.NotEmpty(ImageExporter.Encode(img, ImageFormatKind.Jpg, 0));
        Assert.NotEmpty(ImageExporter.Encode(img, ImageFormatKind.Jpg, 500));
    }

    [Fact]
    public void DefaultFileName_MatchesPattern()
    {
        var now = new DateTime(2026, 11, 18, 21, 4, 5);
        Assert.Equal("Screenshot 2026-11-18 21-04-05.png", ImageExporter.DefaultFileName(now, ImageFormatKind.Png));
        Assert.Equal("Screenshot 2026-11-18 21-04-05.jpg", ImageExporter.DefaultFileName(now, ImageFormatKind.Jpg));
    }

    [Theory]
    [InlineData("a.jpg", ImageFormatKind.Jpg)]
    [InlineData("a.JPEG", ImageFormatKind.Jpg)]
    [InlineData("a.Jpg", ImageFormatKind.Jpg)]
    [InlineData("a.png", ImageFormatKind.Png)]
    [InlineData("a.bmp", ImageFormatKind.Png)]
    [InlineData("a", ImageFormatKind.Png)]
    public void FormatFromPath_JpegVariants(string path, ImageFormatKind expected) =>
        Assert.Equal(expected, ImageExporter.FormatFromPath(path));

    [Fact]
    public void Save_CreatesMissingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sc-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "sub", "dir", "x.png");
            using var img = Noise(10, 10);
            ImageExporter.Save(img, path, ImageFormatKind.Png, 90);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
