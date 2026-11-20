using System.Text;
using ScreenCapture.Core.Ocr;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public sealed class OcrImagePrepTests
{
    private static SKBitmap Solid(int w, int h, SKColor background, SKColor? dot = null)
    {
        var bmp = new SKBitmap(w, h);
        bmp.Erase(background);
        if (dot is { } d)
            bmp.SetPixel(w / 2, h / 2, d);
        return bmp;
    }

    [Fact]
    public void Light_image_is_upscaled_twice_with_white_margin_and_not_inverted()
    {
        using var src = Solid(20, 10, SKColors.White, SKColors.Black);

        using var result = OcrImagePrep.Prepare(src);

        Assert.Equal(SKColorType.Gray8, result.ColorType);
        Assert.Equal(20 * 2 + 2 * OcrImagePrep.Margin, result.Width);
        Assert.Equal(10 * 2 + 2 * OcrImagePrep.Margin, result.Height);
        Assert.Equal(255, result.GetPixel(0, 0).Red);
        Assert.Equal(255, result.GetPixel(OcrImagePrep.Margin + 1, OcrImagePrep.Margin + 1).Red);
        Assert.True(result.GetPixel(OcrImagePrep.Margin + 20, OcrImagePrep.Margin + 10).Red < 128);
    }

    [Fact]
    public void Dark_image_is_inverted_so_text_is_dark_on_light()
    {
        using var src = Solid(20, 10, new SKColor(0x1E, 0x1E, 0x1E), SKColors.White);

        using var result = OcrImagePrep.Prepare(src);

        var background = result.GetPixel(OcrImagePrep.Margin + 1, OcrImagePrep.Margin + 1).Red;
        var text = result.GetPixel(OcrImagePrep.Margin + 20, OcrImagePrep.Margin + 10).Red;
        Assert.True(background > 200, $"background {background}");
        Assert.True(text < 128, $"text {text}");
    }

    [Fact]
    public void Luminance_uses_weighted_channels()
    {
        using var src = Solid(4, 4, new SKColor(0, 255, 0));

        using var result = OcrImagePrep.Prepare(src);

        // 0.587 * 255 = 150: above the 50 % threshold, so not inverted.
        Assert.Equal(150, result.GetPixel(OcrImagePrep.Margin + 2, OcrImagePrep.Margin + 2).Red);
    }

    [Theory]
    [InlineData(4000, 100, 2)]
    [InlineData(4001, 100, 1)]
    [InlineData(100, 4001, 1)]
    public void Huge_images_are_not_upscaled(int w, int h, int scale) =>
        Assert.Equal(scale, OcrImagePrep.ScaleFor(w, h));

    [Fact]
    public void Non_bgra_source_is_accepted()
    {
        using var src = new SKBitmap(new SKImageInfo(6, 6, SKColorType.Rgba8888, SKAlphaType.Premul));
        src.Erase(SKColors.White);

        using var result = OcrImagePrep.Prepare(src);

        Assert.Equal(6 * 2 + 2 * OcrImagePrep.Margin, result.Width);
    }
}

public sealed class OcrTextTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("  \n\f", "")]
    [InlineData("Hello  \nworld\n", "Hello\r\nworld")]
    [InlineData("\n\nA\n\n\n\nB\n\n\f", "A\r\n\r\nB")]
    [InlineData("A\r\nB\rC", "A\r\nB\r\nC")]
    [InlineData("  indented", "  indented")]
    public void Clean_normalizes_lines(string? raw, string expected) =>
        Assert.Equal(expected, OcrText.Clean(raw));
}

public sealed class OcrTextFilesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ScreenCaptureOcrTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Write_creates_folder_and_utf8_file_with_bom()
    {
        var now = new DateTime(2026, 11, 20, 9, 5, 7);

        var path = OcrTextFiles.Write(_folder, now, "Привіт, world");

        Assert.Equal(Path.Combine(_folder, "Text 2026-11-20 09-05-07.txt"), path);
        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("Привіт, world", Encoding.UTF8.GetString(bytes[3..]));
    }

    [Fact]
    public void Write_never_overwrites_an_existing_file()
    {
        var now = new DateTime(2026, 11, 20, 9, 5, 7);

        var first = OcrTextFiles.Write(_folder, now, "one");
        var second = OcrTextFiles.Write(_folder, now, "two");

        Assert.NotEqual(first, second);
        Assert.EndsWith("Text 2026-11-20 09-05-07 (2).txt", second);
        Assert.Equal("one", File.ReadAllText(first));
    }

    [Fact]
    public void DeleteOlderThan_removes_only_old_text_files()
    {
        var now = DateTime.Now;
        var old = OcrTextFiles.Write(_folder, now.AddDays(-3), "old");
        File.SetLastWriteTime(old, now.AddDays(-2));
        var fresh = OcrTextFiles.Write(_folder, now, "fresh");
        var other = Path.Combine(_folder, "notes.txt");
        File.WriteAllText(other, "keep");
        File.SetLastWriteTime(other, now.AddDays(-2));

        OcrTextFiles.DeleteOlderThan(_folder, now, OcrTextFiles.KeepFor);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void DeleteOlderThan_ignores_a_missing_folder() =>
        OcrTextFiles.DeleteOlderThan(Path.Combine(_folder, "missing"), DateTime.Now, OcrTextFiles.KeepFor);
}
