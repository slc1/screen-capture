using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ScreenCapture.Core.Annotations;
using ScreenCapture.Core.Editing;
using ScreenCapture.Core.Geometry;
using ScreenCapture.Core.Rendering;
using SkiaSharp;

namespace ScreenCapture.App.Editor;

/// <summary>
/// Shows the session's image with its annotations, fitted in device pixels: at fit scale 1 every image pixel
/// lands on exactly one device pixel. Pointer input is mapped back with the same transform.
/// </summary>
public sealed class AnnotationCanvas : Control, IDisposable
{
    private static readonly ImmutableDashStyle SelectionDash = new ImmutableDashStyle([4, 4], 0);

    private EditorSession? _session;
    private WriteableBitmap? _composite;
    private bool _dirty = true;
    private bool _gesture;
    private Viewport _viewport = new(1f, SKPoint.Empty);
    private double _scaling = 1;

    public AnnotationCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    /// <summary>Raised for a text-tool click (existing = null) or a double-click on a text annotation in Select.</summary>
    public event Action<SKPoint, TextAnnotation?>? TextRequested;

    public EditorSession? Session
    {
        get => _session;
        set
        {
            if (_session is not null)
                _session.Changed -= OnSessionChanged;
            _session = value;
            if (_session is not null)
                _session.Changed += OnSessionChanged;
            _dirty = true;
            InvalidateVisual();
        }
    }

    /// <summary>DIPs per image pixel at the current fit.</summary>
    public double ImageToDipScale => _viewport.Scale / _scaling;

    public Point ImageToDip(SKPoint imagePoint)
    {
        UpdateViewport();
        var v = ViewportMath.ToView(_viewport, imagePoint);
        return new Point(v.X / _scaling, v.Y / _scaling);
    }

    public void Dispose()
    {
        Session = null;
        _composite?.Dispose();
        _composite = null;
    }

    public override void Render(DrawingContext context)
    {
        if (_session is null)
            return;

        UpdateViewport();
        var composite = EnsureComposite(_session);
        var s = _scaling;
        var dest = new Rect(
            _viewport.Offset.X / s,
            _viewport.Offset.Y / s,
            composite.PixelSize.Width * _viewport.Scale / s,
            composite.PixelSize.Height * _viewport.Scale / s);
        var mode = _viewport.Scale >= 1f ? BitmapInterpolationMode.None : BitmapInterpolationMode.HighQuality;
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = mode }))
            context.DrawImage(composite, new Rect(0, 0, composite.PixelSize.Width, composite.PixelSize.Height), dest);

        if (_session.Selected is { } selected)
            DrawSelection(context, selected.Bounds);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_session is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus();
        e.Handled = true;
        var p = ToImage(e);

        if (_session.Tool == EditorTool.Text)
        {
            TextRequested?.Invoke(p, null);
            return;
        }

        if (_session.Tool == EditorTool.Select && e.ClickCount == 2
            && _session.Document.HitTest(p, _session.HitTolerance) is TextAnnotation text)
        {
            TextRequested?.Invoke(text.Position, text);
            return;
        }

        _gesture = true;
        e.Pointer.Capture(this);
        _session.PointerDown(p);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_gesture && _session is not null)
            _session.PointerMove(ToImage(e));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_gesture || _session is null || e.InitialPressMouseButton != MouseButton.Left)
            return;

        _gesture = false;
        _session.PointerUp(ToImage(e));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_gesture)
            return;
        _gesture = false;
        _session?.Cancel();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        _dirty = true;
        InvalidateVisual();
    }

    private SKPoint ToImage(PointerEventArgs e)
    {
        UpdateViewport();
        var p = e.GetPosition(this);
        return ViewportMath.ToImage(_viewport, new SKPoint((float)(p.X * _scaling), (float)(p.Y * _scaling)));
    }

    private void UpdateViewport()
    {
        if (_session is null)
            return;

        _scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var available = new SKSize((float)(Bounds.Width * _scaling), (float)(Bounds.Height * _scaling));
        var fit = ViewportMath.Fit(new SKSizeI(_session.Image.Width, _session.Image.Height), available);
        // Whole device pixels, so a 1:1 image is not resampled across pixel boundaries.
        _viewport = fit with { Offset = new SKPoint(MathF.Round(fit.Offset.X), MathF.Round(fit.Offset.Y)) };
        _session.HitTolerance = 4f / _viewport.Scale;
    }

    private WriteableBitmap EnsureComposite(EditorSession session)
    {
        var size = new PixelSize(session.Image.Width, session.Image.Height);
        if (_composite is null || _composite.PixelSize != size)
        {
            _composite?.Dispose();
            _composite = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            _dirty = true;
        }

        if (_dirty)
        {
            using var buffer = _composite.Lock();
            var info = new SKImageInfo(buffer.Size.Width, buffer.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var surface = SKSurface.Create(info, buffer.Address, buffer.RowBytes)
                ?? throw new InvalidOperationException("Cannot create a drawing surface for the editor image.");
            AnnotationRenderer.Draw(surface.Canvas, session.Image, session.RenderList);
            surface.Canvas.Flush();
            _dirty = false;
        }

        return _composite;
    }

    private void DrawSelection(DrawingContext context, SKRect bounds)
    {
        var px = 1 / _scaling;
        var tl = ImageToDip(new SKPoint(bounds.Left, bounds.Top));
        var br = ImageToDip(new SKPoint(bounds.Right, bounds.Bottom));
        var rect = new Rect(tl, br).Inflate(3 * px);
        context.DrawRectangle(null, new ImmutablePen(Brushes.White, px), rect);
        context.DrawRectangle(null, new ImmutablePen(Brushes.Black, px, SelectionDash), rect);
    }
}
