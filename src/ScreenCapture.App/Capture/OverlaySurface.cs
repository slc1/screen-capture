using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace ScreenCapture.App.Capture;

/// <summary>Draws the frozen screen, a 50 % dim and the undimmed selection with a 1-device-pixel border.</summary>
public sealed class OverlaySurface : Control
{
    private static readonly IBrush Dim = new ImmutableSolidColorBrush(Color.FromArgb(128, 0, 0, 0));

    private IImage? _image;
    private Rect? _selection;

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

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (_image is not null)
            context.DrawImage(_image, new Rect(_image.Size), bounds);

        if (_selection is not { } s)
        {
            context.FillRectangle(Dim, bounds);
            return;
        }

        context.FillRectangle(Dim, new Rect(0, 0, bounds.Width, s.Top));
        context.FillRectangle(Dim, new Rect(0, s.Bottom, bounds.Width, Math.Max(0, bounds.Height - s.Bottom)));
        context.FillRectangle(Dim, new Rect(0, s.Top, s.Left, s.Height));
        context.FillRectangle(Dim, new Rect(s.Right, s.Top, Math.Max(0, bounds.Width - s.Right), s.Height));

        var px = 1 / (TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
        // Centered half a device pixel outside the selection, so the border never covers selected pixels.
        context.DrawRectangle(null, new ImmutablePen(Brushes.White, px), s.Inflate(px / 2));
    }
}
