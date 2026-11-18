using ScreenCapture.Core.Annotations;
using SkiaSharp;

namespace ScreenCapture.Core.Rendering;

public static class AnnotationRenderer
{
    private const int BlurBlock = 12;

    public static SKBitmap Flatten(SKBitmap original, IEnumerable<Annotation> annotations)
    {
        var result = new SKBitmap(original.Width, original.Height);
        using var canvas = new SKCanvas(result);
        Draw(canvas, original, annotations);
        return result;
    }

    public static void Draw(SKCanvas canvas, SKBitmap original, IEnumerable<Annotation> annotations)
    {
        canvas.DrawBitmap(original, 0, 0);
        foreach (var a in annotations)
        {
            switch (a)
            {
                case LineAnnotation l: DrawLine(canvas, l.Start, l.End, l); break;
                case ArrowAnnotation ar: DrawArrow(canvas, ar); break;
                case RectangleAnnotation r: DrawRectangle(canvas, r); break;
                case MarkerAnnotation m: DrawMarker(canvas, m); break;
                case TextAnnotation t: DrawText(canvas, t); break;
                case BlurAnnotation b: DrawBlur(canvas, original, b); break;
            }
        }
    }

    private static SKPaint StrokePaint(Annotation a) => new()
    {
        Color = a.Color,
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = (int)a.Width,
        StrokeCap = SKStrokeCap.Round,
    };

    private static void DrawLine(SKCanvas canvas, SKPoint start, SKPoint end, Annotation a)
    {
        using var paint = StrokePaint(a);
        canvas.DrawLine(start, end, paint);
    }

    private static void DrawArrow(SKCanvas canvas, ArrowAnnotation a)
    {
        DrawLine(canvas, a.Start, a.End, a);

        var width = (int)a.Width;
        var headLength = Math.Max(10f, 4f * width);
        var angle = MathF.Atan2(a.End.Y - a.Start.Y, a.End.X - a.Start.X);
        var half = 25f * MathF.PI / 180f;
        var p1 = new SKPoint(
            a.End.X - headLength * MathF.Cos(angle - half),
            a.End.Y - headLength * MathF.Sin(angle - half));
        var p2 = new SKPoint(
            a.End.X - headLength * MathF.Cos(angle + half),
            a.End.Y - headLength * MathF.Sin(angle + half));

        using var path = new SKPath();
        path.MoveTo(a.End);
        path.LineTo(p1);
        path.LineTo(p2);
        path.Close();
        using var fill = new SKPaint { Color = a.Color, IsAntialias = true, Style = SKPaintStyle.Fill };
        canvas.DrawPath(path, fill);
    }

    private static void DrawRectangle(SKCanvas canvas, RectangleAnnotation r)
    {
        using var paint = StrokePaint(r);
        paint.StrokeCap = SKStrokeCap.Butt;
        canvas.DrawRect(r.Rect, paint);
    }

    private static void DrawMarker(SKCanvas canvas, MarkerAnnotation m)
    {
        using var paint = new SKPaint
        {
            Color = m.Color.WithAlpha(102),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = (int)m.Width * 4,
            StrokeCap = SKStrokeCap.Square,
            BlendMode = SKBlendMode.Multiply,
        };
        canvas.DrawLine(m.Start, m.End, paint);
    }

    // Same typeface setup as TextAnnotation.Bounds so measured and drawn text agree.
    private static void DrawText(SKCanvas canvas, TextAnnotation t)
    {
        using var font = new SKFont(SKTypeface.Default, t.FontSize);
        using var paint = new SKPaint { Color = t.Color, IsAntialias = true };
        canvas.DrawText(t.Text, t.Position.X, t.Position.Y + t.FontSize, font, paint);
    }

    private static void DrawBlur(SKCanvas canvas, SKBitmap original, BlurAnnotation b)
    {
        var left = Math.Max(0, (int)MathF.Floor(b.Rect.Left));
        var top = Math.Max(0, (int)MathF.Floor(b.Rect.Top));
        var right = Math.Min(original.Width, (int)MathF.Ceiling(b.Rect.Right));
        var bottom = Math.Min(original.Height, (int)MathF.Ceiling(b.Rect.Bottom));
        if (right <= left || bottom <= top) return;

        // 32-bit pixels are read straight from memory; a premultiplied pixel that is not fully opaque
        // still goes through GetPixel so the result stays identical to the per-pixel GetPixel path.
        var direct = original.ColorType is SKColorType.Bgra8888 or SKColorType.Rgba8888;
        var swapRb = original.ColorType == SKColorType.Rgba8888;
        var unpremul = original.AlphaType == SKAlphaType.Unpremul;
        var pixels = direct ? original.GetPixelSpan() : default;
        var rowBytes = original.RowBytes;

        using var paint = new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = false };
        for (var by = top; by < bottom; by += BlurBlock)
        {
            var bh = Math.Min(BlurBlock, bottom - by);
            for (var bx = left; bx < right; bx += BlurBlock)
            {
                var bw = Math.Min(BlurBlock, right - bx);
                long r = 0, g = 0, bl = 0, al = 0;
                for (var y = by; y < by + bh; y++)
                {
                    var row = direct ? pixels.Slice(y * rowBytes + bx * 4, bw * 4) : default;
                    for (var x = bx; x < bx + bw; x++)
                    {
                        if (direct)
                        {
                            var i = (x - bx) * 4;
                            var a = row[i + 3];
                            if (a == 255 || unpremul)
                            {
                                var c0 = row[i];
                                var c2 = row[i + 2];
                                r += swapRb ? c0 : c2; g += row[i + 1]; bl += swapRb ? c2 : c0; al += a;
                                continue;
                            }
                        }

                        var px = original.GetPixel(x, y);
                        r += px.Red; g += px.Green; bl += px.Blue; al += px.Alpha;
                    }
                }
                var n = bw * bh;
                paint.Color = new SKColor((byte)(r / n), (byte)(g / n), (byte)(bl / n), (byte)(al / n));
                canvas.DrawRect(SKRect.Create(bx, by, bw, bh), paint);
            }
        }
    }
}
