using SkiaSharp;

namespace ScreenCapture.Core.Geometry;

public readonly record struct Viewport(float Scale, SKPoint Offset);

public static class ViewportMath
{
    public static Viewport Fit(SKSizeI image, SKSize available)
    {
        float availW = Math.Max(0f, available.Width);
        float availH = Math.Max(0f, available.Height);
        if (image.Width <= 0 || image.Height <= 0)
            return new Viewport(1f, SKPoint.Empty);

        float scale = Math.Min(1f, Math.Min(availW / image.Width, availH / image.Height));
        if (scale <= 0f)
            return new Viewport(1f, SKPoint.Empty);

        var offset = new SKPoint((availW - image.Width * scale) / 2f, (availH - image.Height * scale) / 2f);
        return new Viewport(scale, offset);
    }

    public static SKPoint ToImage(Viewport v, SKPoint viewPoint) =>
        new SKPoint((viewPoint.X - v.Offset.X) / v.Scale, (viewPoint.Y - v.Offset.Y) / v.Scale);

    public static SKPoint ToView(Viewport v, SKPoint imagePoint) =>
        new SKPoint(imagePoint.X * v.Scale + v.Offset.X, imagePoint.Y * v.Scale + v.Offset.Y);
}
