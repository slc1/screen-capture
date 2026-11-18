using ScreenCapture.Core.Annotations;
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
}

/// <summary>Pointer interaction over one image. All coordinates are image pixels.</summary>
public sealed class EditorSession
{
    private const float MinShapeSize = 3f;

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
            OnChanged();
        };
    }

    public event EventHandler? Changed;

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

    public Annotation? Draft { get; private set; }

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
        _downPoint = imagePt;

        switch (Tool)
        {
            case EditorTool.Select:
                var hit = Document.HitTest(imagePt, HitTolerance);
                SelectedId = hit?.Id;
                _moveOriginal = hit;
                break;
            case EditorTool.Text:
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
            _movePreview = _moveOriginal.MoveBy(imagePt.X - _downPoint.X, imagePt.Y - _downPoint.Y);
            OnChanged();
        }
        else if (Draft is not null)
        {
            Draft = CreateShape(imagePt);
            OnChanged();
        }
    }

    public void PointerUp(SKPoint imagePt)
    {
        if (!_pressed)
            return;

        if (_moveOriginal is { } original)
        {
            var dx = imagePt.X - _downPoint.X;
            var dy = imagePt.Y - _downPoint.Y;
            if ((dx != 0 || dy != 0) && Find(original.Id) is not null)
                Document.Execute(new ReplaceAnnotationCommand(original, original.MoveBy(dx, dy)));
        }
        else if (Draft is not null)
        {
            var shape = CreateShape(imagePt);
            Draft = null;
            if (IsLargeEnough(shape))
                Document.Execute(new AddAnnotationCommand(shape));
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

    public void AddText(SKPoint position, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;
        Document.Execute(new AddAnnotationCommand(
            new TextAnnotation(Guid.NewGuid(), Color, Width, position, text, FontSizeFor(Width))));
    }

    public void ReplaceText(Guid id, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Find(id) is not TextAnnotation before || before.Text == text)
            return;
        Document.Execute(new ReplaceAnnotationCommand(before, before with { Text = text }));
    }

    // Brief: 16 / 20 / 24 px for Thin / Medium / Thick.
    private static float FontSizeFor(StrokeWidth width) => width switch
    {
        StrokeWidth.Thin => 16f,
        StrokeWidth.Thick => 24f,
        _ => 20f,
    };

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
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
