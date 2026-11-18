using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using SkiaSharp;

namespace ScreenCapture.App.Capture;

/// <summary>
/// Draws the frozen screen, a 50 % dim and the undimmed selection with a 1-device-pixel two-tone dashed border.
/// </summary>
public sealed class OverlaySurface : Control
{
    private static readonly IBrush Dim = new ImmutableSolidColorBrush(Color.FromArgb(128, 0, 0, 0));

    // Dash lengths are in stroke widths, and the stroke is one device pixel: 6 px black, 6 px gap.
    private static readonly ImmutableDashStyle BorderDash = new([6, 6], 0);

    private IImage? _image;
    private Rect? _selection;
    private Point? _pointer;
    private SKBitmap? _source;

    public IImage? Image
    {
        get => _image;
        set
        {
            _image = value;
            InvalidateVisual();
        }
    }

    /// <summary>Selection in DIPs; the caller aligns it to device pixels.</summary>
    public Rect? Selection
    {
        get => _selection;
        set
        {
            _selection = value;
            InvalidateVisual();
        }
    }

    /// <summary>The frozen pixels, sampled to choose the crosshair colour; not owned by the surface.</summary>
    public SKBitmap? Source
    {
        get => _source;
        set => _source = value;
    }

    /// <summary>Pointer position in DIPs, or null to hide the crosshair.</summary>
    public Point? Pointer
    {
        get => _pointer;
        set
        {
            if (_pointer == value) return;
            _pointer = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (_image is not null)
            context.DrawImage(_image, new Rect(_image.Size), bounds);

        if (_selection is not { } s)
        {
            context.FillRectangle(Dim, bounds);
            DrawCrosshair(context);
            return;
        }

        context.FillRectangle(Dim, new Rect(0, 0, bounds.Width, s.Top));
        context.FillRectangle(Dim, new Rect(0, s.Bottom, bounds.Width, Math.Max(0, bounds.Height - s.Bottom)));
        context.FillRectangle(Dim, new Rect(0, s.Top, s.Left, s.Height));
        context.FillRectangle(Dim, new Rect(s.Right, s.Top, Math.Max(0, bounds.Width - s.Right), s.Height));

        var px = 1 / (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
        // Centered half a device pixel outside the selection, so the border never covers selected pixels.
        var border = s.Inflate(px / 2);
        context.DrawRectangle(null, new ImmutablePen(Brushes.White, px), border);
        context.DrawRectangle(null, new ImmutablePen(Brushes.Black, px, BorderDash), border);
        DrawCrosshair(context);
    }

    private void DrawCrosshair(DrawingContext context)
    {
        if (_pointer is not { } p || _source is not { } src || src.Handle == IntPtr.Zero) return;
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var inside = _selection is { } s
            ? new PixelRect((int)Math.Round(s.Left * scale), (int)Math.Round(s.Top * scale),
                (int)Math.Round(s.Width * scale), (int)Math.Round(s.Height * scale))
            : default;
        foreach (var (x, y) in CrosshairGeometry.ArmPixels(p.X, p.Y, scale, src.Width, src.Height))
        {
            var c = src.GetPixel(x, y);
            var dimmed = !(_selection is not null && inside.Contains(new PixelPoint(x, y)));
            var brush = CrosshairGeometry.UseBlack(c.Red, c.Green, c.Blue, dimmed) ? Brushes.Black : Brushes.White;
            context.FillRectangle(brush, new Rect(x / scale, y / scale, 1 / scale, 1 / scale));
        }
    }

}
