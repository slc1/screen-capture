using ScreenCapture.Core.Annotations;
using ScreenCapture.Core.Rendering;
using SkiaSharp;

namespace ScreenCapture.Core.Editing;

public enum EditorTool
{
    Select,
    Arrow,
    Line,
    Rectangle,
    Marker,
    Text,
    Blur,
    Recognize,
}

/// <summary>Pointer interaction over one image. All coordinates are image pixels.</summary>
public sealed class EditorSession
{
    private const float MinShapeSize = 3f;

    /// <summary>How much of a moved annotation's bounds (per axis, in image px) must stay on the image.</summary>
    private const float MinVisibleOnMove = 8f;

    private bool _pressed;
    private SKPoint _downPoint;
    private Guid _draftId;
    private Annotation? _moveOriginal;
    private Annotation? _movePreview;

    public EditorSession(SKBitmap image)
    {
        Image = image;
        Document.Changed += (_, _) =>
        {
            if (SelectedId is { } id && Find(id) is null)
                SelectedId = null;
            if (_moveOriginal is { } moving && Find(moving.Id) != moving)
            {
                _moveOriginal = null;
                _movePreview = null;
            }

            OnChanged();
        };
    }

    public event EventHandler? Changed;

    /// <summary>Raised when a Recognize gesture ends: the area to read, in whole image pixels (never empty).</summary>
    public event EventHandler<SKRectI>? RecognizeRequested;

    public SKBitmap Image { get; }

    public AnnotationDocument Document { get; } = new();

    public EditorTool Tool
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            ResetGesture();
            SelectedId = null;
            OnChanged();
        }
    } = EditorTool.Arrow;

    public SKColor Color { get; set; } = AnnotationPalette.Default;

    public StrokeWidth Width { get; set; } = StrokeWidth.Medium;

    /// <summary>Selection hit tolerance in image pixels; the view keeps it at a constant screen distance.</summary>
    public float HitTolerance { get; set; } = 4f;

    /// <summary>Font size a new text annotation gets at the current width: 12 / 16 / 20 / 24 / 32 px.</summary>
    public float TextFontSize => Width switch
    {
        StrokeWidth.ExtraThin => 12f,
        StrokeWidth.Thin => 16f,
        StrokeWidth.Thick => 24f,
        StrokeWidth.ExtraThick => 32f,
        _ => 20f,
    };

    public Annotation? Draft { get; private set; }

    /// <summary>The area being dragged with the Recognize tool; not an annotation and never in the document.</summary>
    public SKRect? RecognizeArea { get; private set; }

    public Guid? SelectedId { get; private set; }

    /// <summary>The selected annotation as currently drawn, including an uncommitted move.</summary>
    public Annotation? Selected =>
        SelectedId is not { } id ? null
        : _movePreview?.Id == id ? _movePreview
        : Find(id);

    public IEnumerable<Annotation> RenderList
    {
        get
        {
            foreach (var a in Document.Annotations)
                yield return _movePreview is { } p && p.Id == a.Id ? p : a;
            if (Draft is not null)
                yield return Draft;
        }
    }

    public void PointerDown(SKPoint imagePt)
    {
        ResetGesture();
        _pressed = true;
        _downPoint = Tool is EditorTool.Select or EditorTool.Text ? imagePt : ClampToImage(imagePt);

        switch (Tool)
        {
            case EditorTool.Select:
                var hit = Document.HitTest(imagePt, HitTolerance);
                SelectedId = hit?.Id;
                _moveOriginal = hit;
                break;
            case EditorTool.Text:
                break;
            case EditorTool.Recognize:
                RecognizeArea = Normalized(_downPoint, _downPoint);
                break;
            default:
                _draftId = Guid.NewGuid();
                Draft = CreateShape(imagePt);
                break;
        }

        OnChanged();
    }

    public void PointerMove(SKPoint imagePt)
    {
        if (!_pressed)
            return;

        if (_moveOriginal is not null)
        {
            _movePreview = MovedWithinImage(_moveOriginal, imagePt);
            OnChanged();
        }
        else if (Draft is not null)
        {
            Draft = CreateShape(ClampToImage(imagePt));
            OnChanged();
        }
        else if (RecognizeArea is not null)
        {
            RecognizeArea = Normalized(_downPoint, ClampToImage(imagePt));
            OnChanged();
        }
    }

    public void PointerUp(SKPoint imagePt)
    {
        if (!_pressed)
            return;

        if (_moveOriginal is { } original)
        {
            var moved = MovedWithinImage(original, imagePt);
            if (moved != original && Find(original.Id) is not null)
                Document.Execute(new ReplaceAnnotationCommand(original, moved));
        }
        else if (Draft is not null)
        {
            var shape = CreateShape(ClampToImage(imagePt));
            Draft = null;
            if (IsLargeEnough(shape))
                Document.Execute(new AddAnnotationCommand(shape));
        }
        else if (RecognizeArea is not null)
        {
            var area = ToPixels(Normalized(_downPoint, ClampToImage(imagePt)));
            ResetGesture();
            OnChanged();
            RecognizeRequested?.Invoke(this, area);
            return;
        }

        ResetGesture();
        OnChanged();
    }

    /// <summary>Abandons the current gesture without committing it and clears the selection.</summary>
    public void Cancel()
    {
        ResetGesture();
        SelectedId = null;
        OnChanged();
    }

    public void DeleteSelected()
    {
        if (SelectedId is not { } id)
            return;
        ResetGesture();
        SelectedId = null;
        if (Find(id) is not null)
            Document.Execute(new RemoveAnnotationCommand(id));
        else
            OnChanged();
    }

    /// <summary>
    /// The pixels to recognize in <paramref name="area"/>: the original image with only the Blur annotations applied,
    /// so drawn shapes never damage letters and text the user blurred stays unreadable.
    /// </summary>
    public SKBitmap CopyForRecognition(SKRectI area)
    {
        using var flat = AnnotationRenderer.Flatten(Image, Document.Annotations.OfType<BlurAnnotation>().ToList());
        using var subset = new SKBitmap();
        if (!flat.ExtractSubset(subset, area))
            throw new InvalidOperationException($"Could not extract {area} from a {flat.Width}x{flat.Height} image.");
        return subset.Copy() ?? throw new InvalidOperationException("Could not copy the area.");
    }

    public void AddText(SKPoint position, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        Document.Execute(new AddAnnotationCommand(
            KeepOnImage(new TextAnnotation(Guid.NewGuid(), Color, Width, position, text, TextFontSize))));
    }

    public void ReplaceText(Guid id, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Find(id) is not TextAnnotation before || before.Text == text)
            return;
        Document.Execute(new ReplaceAnnotationCommand(before, KeepOnImage(before with { Text = text })));
    }

    private static SKRect Normalized(SKPoint a, SKPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    /// <summary>Whole pixels covering the area; a click or a tiny drag means the whole image.</summary>
    private SKRectI ToPixels(SKRect area)
    {
        var r = new SKRectI(
            Math.Clamp((int)MathF.Floor(area.Left), 0, Image.Width),
            Math.Clamp((int)MathF.Floor(area.Top), 0, Image.Height),
            Math.Clamp((int)MathF.Ceiling(area.Right), 0, Image.Width),
            Math.Clamp((int)MathF.Ceiling(area.Bottom), 0, Image.Height));
        return r.Width < MinShapeSize || r.Height < MinShapeSize ? new SKRectI(0, 0, Image.Width, Image.Height) : r;
    }

    private SKPoint ClampToImage(SKPoint p) =>
        new(Math.Clamp(p.X, 0f, Image.Width), Math.Clamp(p.Y, 0f, Image.Height));

    /// <summary>Moves the text so its whole bounds lie on the image (left/top-aligned if it is larger).</summary>
    private TextAnnotation KeepOnImage(TextAnnotation t)
    {
        var b = t.Bounds;
        var x = Math.Clamp(t.Position.X, 0f, Image.Width - Math.Min(b.Width, Image.Width));
        var y = Math.Clamp(t.Position.Y, 0f, Image.Height - Math.Min(b.Height, Image.Height));
        return x == t.Position.X && y == t.Position.Y ? t : t with { Position = new SKPoint(x, y) };
    }

    /// <summary>
    /// The original moved by the pointer delta, with the delta clamped so its bounds keep overlapping the image
    /// (it may hang partly off the image, but never leaves it entirely).
    /// </summary>
    private Annotation MovedWithinImage(Annotation original, SKPoint imagePt)
    {
        var b = original.Bounds;
        var dx = ClampDelta(imagePt.X - _downPoint.X, b.Left, b.Right, Image.Width);
        var dy = ClampDelta(imagePt.Y - _downPoint.Y, b.Top, b.Bottom, Image.Height);
        return dx == 0 && dy == 0 ? original : original.MoveBy(dx, dy);
    }

    private static float ClampDelta(float delta, float start, float end, float extent)
    {
        var keep = Math.Min(MinVisibleOnMove, Math.Min(end - start, extent));
        // Always allow 0, so an annotation already showing less than `keep` is never moved by a plain click.
        var min = Math.Min(keep - end, 0f);
        var max = Math.Max(extent - keep - start, 0f);
        return Math.Clamp(delta, min, max);
    }

    private Annotation CreateShape(SKPoint end) => Tool switch
    {
        EditorTool.Arrow => new ArrowAnnotation(_draftId, Color, Width, _downPoint, end),
        EditorTool.Line => new LineAnnotation(_draftId, Color, Width, _downPoint, end),
        EditorTool.Marker => new MarkerAnnotation(_draftId, Color, Width, _downPoint, end),
        EditorTool.Rectangle => RectangleAnnotation.FromCorners(_draftId, Color, Width, _downPoint, end),
        EditorTool.Blur => BlurAnnotation.FromCorners(_draftId, Color, Width, _downPoint, end),
        _ => throw new InvalidOperationException($"{Tool} does not draw shapes."),
    };

    private static bool IsLargeEnough(Annotation shape) => shape switch
    {
        RectangleAnnotation r => r.Rect.Width >= MinShapeSize && r.Rect.Height >= MinShapeSize,
        BlurAnnotation b => b.Rect.Width >= MinShapeSize && b.Rect.Height >= MinShapeSize,
        ArrowAnnotation a => Length(a.Start, a.End) >= MinShapeSize,
        LineAnnotation l => Length(l.Start, l.End) >= MinShapeSize,
        MarkerAnnotation m => Length(m.Start, m.End) >= MinShapeSize,
        _ => false,
    };

    private static float Length(SKPoint a, SKPoint b) => SKPoint.Distance(a, b);

    private Annotation? Find(Guid id)
    {
        foreach (var a in Document.Annotations)
        {
            if (a.Id == id)
                return a;
        }

        return null;
    }

    private void ResetGesture()
    {
        _pressed = false;
        _moveOriginal = null;
        _movePreview = null;
        Draft = null;
        RecognizeArea = null;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
