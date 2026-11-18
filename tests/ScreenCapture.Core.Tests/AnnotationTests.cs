using ScreenCapture.Core.Annotations;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public class AnnotationTests
{
    [Fact]
    public void Line_HitTest_NearSegment()
    {
        var a = new LineAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new(0, 0), new(100, 0));
        Assert.True(a.HitTest(new(50, 3), 4));
        Assert.False(a.HitTest(new(50, 10), 4));
        Assert.False(a.HitTest(new(110, 0), 4));
    }

    [Fact]
    public void Rectangle_HitTest_OnlyOnBorder()
    {
        var r = new RectangleAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new SKRect(10, 10, 110, 60));
        Assert.True(r.HitTest(new(10, 30), 4));
        Assert.False(r.HitTest(new(60, 35), 4));
    }

    [Fact]
    public void Blur_HitTest_Inside()
    {
        var b = new BlurAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new SKRect(10, 10, 110, 60));
        Assert.True(b.HitTest(new(60, 35), 4));
    }

    [Fact]
    public void MoveBy_KeepsIdAndShiftsGeometry()
    {
        var a = new ArrowAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new(0, 0), new(10, 10));
        var m = (ArrowAnnotation)a.MoveBy(5, -2);
        Assert.Equal(a.Id, m.Id);
        Assert.Equal(new SKPoint(5, -2), m.Start);
        Assert.Equal(new SKPoint(15, 8), m.End);
    }

    [Fact]
    public void Rectangle_FromReversedCorners_IsNormalized()
    {
        var r = RectangleAnnotation.FromCorners(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new(100, 80), new(10, 20));
        Assert.Equal(new SKRect(10, 20, 100, 80), r.Rect);
    }

    [Fact]
    public void Text_HitTest_InsideEstimatedBounds()
    {
        var t = new TextAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new(10, 10), "Hello", 20);
        Assert.True(t.HitTest(new(15, 20), 4));
        Assert.False(t.HitTest(new(500, 500), 4));
    }

    [Fact]
    public void Rectangle_DirectlyConstructedReversedRect_IsNormalized()
    {
        var r = new RectangleAnnotation(Guid.NewGuid(), SKColors.Red, StrokeWidth.Thin, new SKRect(110, 60, 10, 10));
        Assert.Equal(new SKRect(10, 10, 110, 60), r.Rect);
        Assert.True(r.HitTest(new(10, 30), 4));
    }

    [Fact]
    public void Palette_HasEightColorsAndRedDefault()
    {
        Assert.Equal(8, AnnotationPalette.Colors.Count);
        Assert.Equal(SKColor.Parse("#E53935"), AnnotationPalette.Default);
        Assert.Equal(SKColor.Parse("#FFFFFF"), AnnotationPalette.Colors[7]);
    }
}
