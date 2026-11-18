using SkiaSharp;

namespace ScreenCapture.Core.Geometry;

public readonly record struct Viewport(float Scale, SKPoint Offset);

public static class ViewportMath
{
    public static Viewport Fit(SKSizeI image, SKSize available)
    {
        float scale = Math.Min(1f, Math.Min(available.Width / image.Width, available.Height / image.Height));
        var offset = new SKPoint((available.Width - image.Width * scale) / 2f, (available.Height - image.Height * scale) / 2f);
        return new Viewport(scale, offset);
    }

    public static SKPoint ToImage(Viewport v, SKPoint viewPoint) =>
        new SKPoint((viewPoint.X - v.Offset.X) / v.Scale, (viewPoint.Y - v.Offset.Y) / v.Scale);

    public static SKPoint ToView(Viewport v, SKPoint imagePoint) =>
        new SKPoint(imagePoint.X * v.Scale + v.Offset.X, imagePoint.Y * v.Scale + v.Offset.Y);
}
