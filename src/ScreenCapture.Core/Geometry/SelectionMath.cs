using SkiaSharp;

namespace ScreenCapture.Core.Geometry;

public static class SelectionMath
{
    public const int MinSizePx = 3;

    public static SKRectI ToPixelRect(SKPoint startDip, SKPoint endDip, double scale, SKSizeI imageSize)
    {
        double x1 = startDip.X * scale, y1 = startDip.Y * scale;
        double x2 = endDip.X * scale, y2 = endDip.Y * scale;

        int left = Clamp((int)Math.Floor(Math.Min(x1, x2)), imageSize.Width);
        int top = Clamp((int)Math.Floor(Math.Min(y1, y2)), imageSize.Height);
        int right = Clamp((int)Math.Ceiling(Math.Max(x1, x2)), imageSize.Width);
        int bottom = Clamp((int)Math.Ceiling(Math.Max(y1, y2)), imageSize.Height);
        return new SKRectI(left, top, right, bottom);
    }

    public static bool IsTooSmall(SKRectI rect) => rect.Width < MinSizePx || rect.Height < MinSizePx;

    private static int Clamp(int v, int max) => Math.Clamp(v, 0, max);
}
