using SkiaSharp;

namespace ScreenCapture.Core.Annotations;

public abstract record Annotation(Guid Id, SKColor Color, StrokeWidth Width)
{
    public abstract SKRect Bounds { get; }

    public abstract Annotation MoveBy(float dx, float dy);

    public abstract bool HitTest(SKPoint p, float tolerance);

    protected float StrokeTolerance(float tolerance) => tolerance + (int)Width / 2f;

    protected SKRect InflatedByStroke(SKRect r)
    {
        var h = (int)Width / 2f;
        return new SKRect(r.Left - h, r.Top - h, r.Right + h, r.Bottom + h);
    }

    protected static SKRect Offset(SKRect r, float dx, float dy) =>
        new(r.Left + dx, r.Top + dy, r.Right + dx, r.Bottom + dy);

    protected static SKPoint Offset(SKPoint p, float dx, float dy) => new(p.X + dx, p.Y + dy);

    protected static bool Contains(SKRect r, SKPoint p, float inflate) =>
        p.X >= r.Left - inflate && p.X <= r.Right + inflate &&
        p.Y >= r.Top - inflate && p.Y <= r.Bottom + inflate;

    protected static float DistanceToSegment(SKPoint p, SKPoint a, SKPoint b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;
        var t = lenSq == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0f, 1f);
        var cx = a.X + t * dx;
        var cy = a.Y + t * dy;
        var ex = p.X - cx;
        var ey = p.Y - cy;
        return MathF.Sqrt(ex * ex + ey * ey);
    }

    protected static SKRect FromPoints(SKPoint a, SKPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
}

public sealed record ArrowAnnotation(Guid Id, SKColor Color, StrokeWidth Width, SKPoint Start, SKPoint End)
    : Annotation(Id, Color, Width)
{
    public override SKRect Bounds => InflatedByStroke(FromPoints(Start, End));

    public override Annotation MoveBy(float dx, float dy) =>
        this with { Start = Offset(Start, dx, dy), End = Offset(End, dx, dy) };

    public override bool HitTest(SKPoint p, float tolerance) =>
        DistanceToSegment(p, Start, End) <= StrokeTolerance(tolerance);
}

public sealed record LineAnnotation(Guid Id, SKColor Color, StrokeWidth Width, SKPoint Start, SKPoint End)
    : Annotation(Id, Color, Width)
{
    public override SKRect Bounds => InflatedByStroke(FromPoints(Start, End));

    public override Annotation MoveBy(float dx, float dy) =>
        this with { Start = Offset(Start, dx, dy), End = Offset(End, dx, dy) };

    public override bool HitTest(SKPoint p, float tolerance) =>
        DistanceToSegment(p, Start, End) <= StrokeTolerance(tolerance);
}

public sealed record MarkerAnnotation(Guid Id, SKColor Color, StrokeWidth Width, SKPoint Start, SKPoint End)
    : Annotation(Id, Color, Width)
{
    public override SKRect Bounds => InflatedByStroke(FromPoints(Start, End));

    public override Annotation MoveBy(float dx, float dy) =>
        this with { Start = Offset(Start, dx, dy), End = Offset(End, dx, dy) };

    public override bool HitTest(SKPoint p, float tolerance) =>
        DistanceToSegment(p, Start, End) <= StrokeTolerance(tolerance);
}

public sealed record RectangleAnnotation(Guid Id, SKColor Color, StrokeWidth Width, SKRect Rect)
    : Annotation(Id, Color, Width)
{
    public SKRect Rect { get; init => field = value.Standardized; } = Rect.Standardized;

    public static RectangleAnnotation FromCorners(Guid id, SKColor color, StrokeWidth width, SKPoint a, SKPoint b) =>
        new(id, color, width, FromPoints(a, b));

    public override SKRect Bounds => InflatedByStroke(Rect);

    public override Annotation MoveBy(float dx, float dy) => this with { Rect = Offset(Rect, dx, dy) };

    public override bool HitTest(SKPoint p, float tolerance)
    {
        var tol = StrokeTolerance(tolerance);
        var r = Rect;
        var tl = new SKPoint(r.Left, r.Top);
        var tr = new SKPoint(r.Right, r.Top);
        var br = new SKPoint(r.Right, r.Bottom);
        var bl = new SKPoint(r.Left, r.Bottom);
        return DistanceToSegment(p, tl, tr) <= tol
            || DistanceToSegment(p, tr, br) <= tol
            || DistanceToSegment(p, br, bl) <= tol
            || DistanceToSegment(p, bl, tl) <= tol;
    }
}

public sealed record BlurAnnotation(Guid Id, SKColor Color, StrokeWidth Width, SKRect Rect)
    : Annotation(Id, Color, Width)
{
    public SKRect Rect { get; init => field = value.Standardized; } = Rect.Standardized;

    public static BlurAnnotation FromCorners(Guid id, SKColor color, StrokeWidth width, SKPoint a, SKPoint b) =>
        new(id, color, width, FromPoints(a, b));

    public override SKRect Bounds => Rect;

    public override Annotation MoveBy(float dx, float dy) => this with { Rect = Offset(Rect, dx, dy) };

    public override bool HitTest(SKPoint p, float tolerance) => Contains(Rect, p, tolerance);
}

public sealed record TextAnnotation(
    Guid Id, SKColor Color, StrokeWidth Width, SKPoint Position, string Text, float FontSize)
    : Annotation(Id, Color, Width)
{
    public override SKRect Bounds
    {
        get
        {
            using var paint = new SKPaint { Typeface = SKTypeface.Default, TextSize = FontSize };
            var w = paint.MeasureText(Text);
            return new SKRect(Position.X, Position.Y, Position.X + w, Position.Y + FontSize * 1.25f);
        }
    }

    public override Annotation MoveBy(float dx, float dy) => this with { Position = Offset(Position, dx, dy) };

    public override bool HitTest(SKPoint p, float tolerance) => Contains(Bounds, p, tolerance);
}
