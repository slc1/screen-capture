using ScreenCapture.Platform.Windows;
using SkiaSharp;

namespace ScreenCapture.Platform.Windows.Tests;

public sealed class TesseractOcrServiceTests : IDisposable
{
    private static readonly string[] Lines =
    [
        "Привіт! Це перевірка розпізнавання: їжак, євро, Київ.",
        "Settings > Hotkeys: Ctrl+Alt+5 captures an area of the screen.",
        "Помилка 404: файл не знайдено (версія 0.1.1).",
    ];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ScreenCaptureOcrTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The native DLLs stay loaded in the test process.
        }
    }

    private static SKBitmap Render(float fontSize, bool dark)
    {
        using var typeface = SKTypeface.FromFamilyName("Segoe UI");
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = fontSize,
            IsAntialias = true,
            Color = dark ? new SKColor(0xD4, 0xD4, 0xD4) : new SKColor(0x20, 0x20, 0x20),
        };
        var lineHeight = (int)(fontSize * 1.5f);
        var bitmap = new SKBitmap((int)Lines.Max(paint.MeasureText) + 20, lineHeight * Lines.Length + 12);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(dark ? new SKColor(0x1E, 0x1E, 0x1E) : SKColors.White);
        for (var i = 0; i < Lines.Length; i++)
            canvas.DrawText(Lines[i], 10, 6 + lineHeight * i + fontSize, paint);
        return bitmap;
    }

    [Theory]
    [InlineData(14f, false)]
    [InlineData(12f, true)]
    public void Recognizes_mixed_ukrainian_and_english_screen_text(float fontSize, bool dark)
    {
        using var image = Render(fontSize, dark);

        var text = new TesseractOcrService(_root).Recognize(image);

        var lines = text.Split("\r\n");
        Assert.Equal(3, lines.Length);
        Assert.Contains("Привіт", lines[0]);
        Assert.Contains("їжак", lines[0]);
        Assert.Contains("Київ", lines[0]);
        Assert.Contains("captures", lines[1]);
        Assert.Contains("Помилка 404", lines[2]);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
    }

    [Fact]
    public void Blank_image_gives_empty_text()
    {
        using var image = new SKBitmap(120, 40);
        image.Erase(SKColors.White);

        Assert.Equal("", new TesseractOcrService(_root).Recognize(image));
    }

    [Fact]
    public void Extracts_engine_files_and_repairs_a_truncated_one()
    {
        using var image = new SKBitmap(20, 20);
        image.Erase(SKColors.White);
        new TesseractOcrService(_root).Recognize(image);

        var model = Path.Combine(_root, "tessdata", "eng.traineddata");
        Assert.True(File.Exists(Path.Combine(_root, "x64", "tesseract55.dll")));
        Assert.True(File.Exists(Path.Combine(_root, "x64", "leptonica-1.85.0.dll")));
        Assert.True(File.Exists(Path.Combine(_root, "tessdata", "ukr.traineddata")));
        var size = new FileInfo(model).Length;

        using (var stream = File.Open(model, FileMode.Open))
            stream.SetLength(10);
        new TesseractOcrService(_root).Recognize(image);

        Assert.Equal(size, new FileInfo(model).Length);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }
}
