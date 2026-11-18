using ScreenCapture.Core.Geometry;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public class ViewportMathTests
{
    [Fact]
    public void Fit_SmallImage_NotUpscaled()
    {
        var v = ViewportMath.Fit(new SKSizeI(100, 100), new SKSize(800, 600));
        Assert.Equal(1f, v.Scale);
        Assert.Equal(new SKPoint(350, 250), v.Offset);
    }

    [Fact]
    public void Fit_LargeImage_ScaledDown()
    {
        var v = ViewportMath.Fit(new SKSizeI(3840, 2160), new SKSize(1920, 1080));
        Assert.Equal(0.5f, v.Scale);
    }

    [Fact]
    public void ToImage_RoundTripsToView()
    {
        var v = ViewportMath.Fit(new SKSizeI(3840, 2160), new SKSize(1000, 700));
        var p = new SKPoint(1234, 567);
        var back = ViewportMath.ToImage(v, ViewportMath.ToView(v, p));
        Assert.Equal(p.X, back.X, 2);
        Assert.Equal(p.Y, back.Y, 2);
    }
}
