using ScreenCapture.Core.Annotations;
using ScreenCapture.Core.Export;
using ScreenCapture.Core.Rendering;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public class AnnotationRendererTests
{
    private static SKBitmap Solid(int w, int h, SKColor c)
    {
        var bmp = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(c);
        return bmp;
    }

    [Fact]
    public void Flatten_NoAnnotations_EqualsOriginalPixels()
    {
        using var original = Solid(50, 50, SKColors.Blue);
        using var result = AnnotationRenderer.Flatten(original, Array.Empty<Annotation>());
        Assert.Equal(50, result.Width);
        Assert.Equal(50, result.Height);
        Assert.Equal(SKColors.Blue, result.GetPixel(0, 0));
        Assert.Equal(SKColors.Blue, result.GetPixel(25, 25));
        Assert.Equal(SKColors.Blue, result.GetPixel(49, 49));
    }

    [Fact]
    public void Flatten_Rectangle_PaintsBorderNotInside()
    {
        using var original = Solid(50, 50, SKColors.Blue);
        var rect = new RectangleAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new SKRect(10, 10, 40, 40));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { rect });
        var border = result.GetPixel(10, 25);
        Assert.True(border.Red > 200 && border.Blue < 60, $"border was {border}");
        Assert.Equal(SKColors.Blue, result.GetPixel(25, 25));
    }

    [Fact]
    public void Flatten_Blur_ReplacesBlockWithAverage()
    {
        using var original = Solid(24, 24, SKColors.Black);
        using (var canvas = new SKCanvas(original))
        using (var paint = new SKPaint { Color = SKColors.White })
            canvas.DrawRect(new SKRect(6, 0, 24, 24), paint);
        var blur = new BlurAnnotation(Guid.NewGuid(), SKColors.Black, StrokeWidth.Medium, new SKRect(0, 0, 12, 12));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { blur });
        var p = result.GetPixel(2, 2);
        Assert.InRange((int)p.Red, 60, 200);
        Assert.Equal(p, result.GetPixel(10, 10));
    }

    [Fact]
    public void Flatten_Blur_ReadsOriginalNotOtherAnnotations()
    {
        using var original = Solid(24, 24, SKColors.Blue);
        var rect = new RectangleAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thick, new SKRect(2, 2, 22, 22));
        var blur = new BlurAnnotation(Guid.NewGuid(), SKColors.Black, StrokeWidth.Medium, new SKRect(0, 0, 24, 24));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { rect, blur });
        var p = result.GetPixel(2, 12);
        Assert.Equal(p.Red, p.Green);
        Assert.Equal(SKColors.Blue, p);
    }

    [Fact]
    public void Flatten_Marker_IsTranslucent()
    {
        using var original = Solid(50, 50, SKColors.White);
        var yellow = new SKColor(255, 255, 0);
        var marker = new MarkerAnnotation(Guid.NewGuid(), yellow, StrokeWidth.Medium, new SKPoint(5, 25), new SKPoint(45, 25));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { marker });
        var p = result.GetPixel(25, 25);
        Assert.NotEqual(SKColors.White, p);
        Assert.NotEqual(yellow, p);
        Assert.True(p.Blue < 255);
    }

    [Fact]
    public void Flatten_DoesNotMutateOriginal()
    {
        using var original = Solid(20, 20, SKColors.Blue);
        var line = new LineAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thick, new SKPoint(0, 10), new SKPoint(20, 10));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { line });
        Assert.Equal(SKColors.Blue, original.GetPixel(10, 10));
        Assert.NotEqual(SKColors.Blue, result.GetPixel(10, 10));
    }

    [Fact]
    public void Flatten_ArrowAndText_DrawWithoutError()
    {
        using var original = Solid(100, 60, SKColors.White);
        var arrow = new ArrowAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new SKPoint(5, 30), new SKPoint(90, 30));
        var text = new TextAnnotation(Guid.NewGuid(), SKColors.Black, StrokeWidth.Medium, new SKPoint(5, 5), "Hi", 20f);
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { arrow, text });
        Assert.NotEqual(SKColors.White, result.GetPixel(60, 30));
    }
    [Fact]
    public void Flatten_LightMarkerOnBlack_IsVisible()
    {
        using var original = Solid(50, 50, SKColors.Black);
        var marker = new MarkerAnnotation(Guid.NewGuid(), SKColors.White, StrokeWidth.Medium, new SKPoint(5, 25), new SKPoint(45, 25));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { marker });
        var p = result.GetPixel(25, 25);
        Assert.NotEqual(SKColors.Black, p);
        Assert.InRange((int)p.Red, 80, 120);
    }

    [Theory]
    [InlineData(0xFFFFFFFFu, SKBlendMode.SrcOver)]
    [InlineData(0xFFFFFF00u, SKBlendMode.SrcOver)]
    [InlineData(0xFF000000u, SKBlendMode.Multiply)]
    [InlineData(0xFFFF0000u, SKBlendMode.Multiply)]
    [InlineData(0xFF00FF00u, SKBlendMode.Multiply)]
    public void MarkerBlendMode_MultipliesOnlyNonLightColours(uint argb, SKBlendMode expected) =>
        Assert.Equal(expected, AnnotationRenderer.MarkerBlendMode(new SKColor(argb)));

    [Fact]
    public void Flatten_OpaqueOriginal_EncodesPngWithoutAlpha()
    {
        using var original = new SKBitmap(new SKImageInfo(20, 20, SKColorType.Bgra8888, SKAlphaType.Opaque));
        original.Erase(SKColors.Blue);
        var marker = new MarkerAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new SKPoint(2, 10), new SKPoint(18, 10));
        using var result = AnnotationRenderer.Flatten(original, new Annotation[] { marker });
        Assert.Equal(SKAlphaType.Opaque, result.AlphaType);

        var png = ImageExporter.Encode(result, ImageFormatKind.Png, 100);
        // IHDR colour type byte: 2 = RGB, 6 = RGBA.
        Assert.Equal(2, png[25]);
        using var decoded = SKBitmap.Decode(png);
        Assert.Equal(SKColors.Blue, decoded.GetPixel(0, 0));
    }

    [Fact]
    public void Flatten_TranslucentOriginal_KeepsAlpha()
    {
        using var original = Solid(10, 10, SKColors.Blue.WithAlpha(128));
        using var result = AnnotationRenderer.Flatten(original, Array.Empty<Annotation>());
        Assert.NotEqual(SKAlphaType.Opaque, result.AlphaType);
        Assert.Equal(128, result.GetPixel(5, 5).Alpha);
    }
}
