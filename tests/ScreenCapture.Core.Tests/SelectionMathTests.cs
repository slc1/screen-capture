using ScreenCapture.Core.Geometry;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public class SelectionMathTests
{
    private static readonly SKSizeI Image = new SKSizeI(1920, 1080);

    [Fact]
    public void ToPixelRect_AppliesScale()
    {
        var r = SelectionMath.ToPixelRect(new SKPoint(10, 10), new SKPoint(20, 30), 1.5, Image);
        Assert.Equal(new SKRectI(15, 15, 30, 45), r);
    }

    [Fact]
    public void ReversedDrag_IsNormalized()
    {
        var r = SelectionMath.ToPixelRect(new SKPoint(20, 30), new SKPoint(10, 10), 1, Image);
        Assert.Equal(new SKRectI(10, 10, 20, 30), r);
    }

    [Fact]
    public void OutOfBounds_IsClamped()
    {
        var r = SelectionMath.ToPixelRect(new SKPoint(100, 100), new SKPoint(5000, -50), 1, Image);
        Assert.Equal(1920, r.Right);
        Assert.Equal(0, r.Top);
    }

    [Fact]
    public void TooSmallSelection_IsRejected()
    {
        Assert.True(SelectionMath.IsTooSmall(new SKRectI(5, 5, 7, 50)));
        Assert.False(SelectionMath.IsTooSmall(new SKRectI(5, 5, 8, 8)));
    }
}
