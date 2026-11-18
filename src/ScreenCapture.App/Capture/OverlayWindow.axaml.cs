using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ScreenCapture.App.Common;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Geometry;
using ScreenCapture.Platform;
using SkiaSharp;

namespace ScreenCapture.App.Capture;

public partial class OverlayWindow : Window
{
    private const double LabelGap = 6;

    private Bitmap? _frozen;
    private Point _dragStart;
    private bool _dragging;
    private bool _closed;

    public OverlayWindow()
    {
        InitializeComponent();
        Cursor = new Cursor(StandardCursorType.None);
        Surface.PointerPressed += OnSurfacePressed;
        Surface.PointerMoved += OnSurfaceMoved;
        Surface.PointerEntered += (_, e) => Surface.Pointer = e.GetPosition(Surface);
        Surface.PointerExited += (_, _) => Surface.Pointer = null;
        Surface.PointerReleased += OnSurfaceReleased;
        Surface.PointerCaptureLost += OnSurfaceCaptureLost;
    }

    public OverlayWindow(ScreenSnapshot snapshot) : this()
    {
        Snapshot = snapshot;
        _frozen = SkiaBitmapConversion.ToAvalonia(snapshot.Image);
        Surface.Image = _frozen;
        Surface.Source = snapshot.Image;
        Position = new PixelPoint(snapshot.Monitor.BoundsPx.Left, snapshot.Monitor.BoundsPx.Top);
        WindowState = WindowState.FullScreen;
    }

    public ScreenSnapshot? Snapshot { get; }

    /// <summary>
    /// Creates, shows and closes an invisible overlay once, so the first capture does not pay for loading
    /// its XAML, styles and window code.
    /// </summary>
    public static void Prewarm()
    {
        try
        {
            var window = new OverlayWindow
            {
                Opacity = 0,
                ShowActivated = false,
                Width = 1,
                Height = 1,
                Position = new PixelPoint(-32000, -32000),
            };
            window.Show();
            window.Close();
        }
        catch (Exception ex)
        {
            FileLog.Error("Could not pre-warm the overlay", ex);
        }
    }

    /// <summary>Raised with the selection in image pixels when a large-enough drag ends.</summary>
    public event Action<OverlayWindow, SKRectI>? SelectionCompleted;

    public event Action? CancelRequested;

    /// <summary>Raised with details when the window cannot be made to cover its monitor 1:1.</summary>
    public event Action<string>? CoverageFailed;

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (Snapshot is not { } s) return;
        if (Math.Abs(RenderScaling - s.Monitor.Scale) > 0.001)
            FileLog.Info($"Overlay on {s.Monitor.DeviceName}: RenderScaling {RenderScaling} differs from monitor scale {s.Monitor.Scale}");

        if (CoverageMismatch() is not { } mismatch) return;
        FileLog.Info($"Overlay on {s.Monitor.DeviceName} does not cover the monitor ({mismatch}); applying explicit bounds");
        var bounds = s.Monitor.BoundsPx;
        WindowState = WindowState.Normal;
        Position = new PixelPoint(bounds.Left, bounds.Top);
        Width = bounds.Width / RenderScaling;
        Height = bounds.Height / RenderScaling;
        Dispatcher.UIThread.Post(() =>
        {
            if (_closed || CoverageMismatch() is not { } still) return;
            CoverageFailed?.Invoke($"Overlay on {s.Monitor.DeviceName} does not cover the monitor: {still}");
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Null when the client area maps 1:1 onto the snapshot's pixels at the monitor's position.</summary>
    private string? CoverageMismatch()
    {
        var s = Snapshot!;
        var bounds = s.Monitor.BoundsPx;
        var w = (int)Math.Round(ClientSize.Width * RenderScaling);
        var h = (int)Math.Round(ClientSize.Height * RenderScaling);
        var pos = Position;
        if (w == s.Image.Width && h == s.Image.Height && pos.X == bounds.Left && pos.Y == bounds.Top) return null;
        return $"window at {pos.X},{pos.Y} {w}x{h} px, monitor at {bounds.Left},{bounds.Top} {s.Image.Width}x{s.Image.Height} px";
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (!IsActive) Activate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        CancelRequested?.Invoke();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _closed = true;
        ReleaseFrozen();
    }

    public void ReleaseFrozen()
    {
        Surface.Image = null;
        Surface.Source = null;
        _frozen?.Dispose();
        _frozen = null;
    }

    private void OnSurfacePressed(object? sender, PointerPressedEventArgs e)
    {
        if (Snapshot is null || !e.GetCurrentPoint(Surface).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        _dragStart = e.GetPosition(Surface);
        _dragging = true;
        e.Pointer.Capture(Surface);
        Hint.IsVisible = false;
        ShowSelection(ToPixelRect(_dragStart));
    }

    private void OnSurfaceMoved(object? sender, PointerEventArgs e)
    {
        Surface.Pointer = e.GetPosition(Surface);
        if (!_dragging) return;
        ShowSelection(ToPixelRect(e.GetPosition(Surface)));
    }

    private void OnSurfaceReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || e.InitialPressMouseButton != MouseButton.Left) return;
        e.Handled = true;
        var rect = ToPixelRect(e.GetPosition(Surface));
        _dragging = false;
        e.Pointer.Capture(null);
        if (SelectionMath.IsTooSmall(rect))
        {
            ResetSelection();
            return;
        }

        SelectionCompleted?.Invoke(this, rect);
    }

    private void OnSurfaceCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        ResetSelection();
    }

    private SKRectI ToPixelRect(Point end)
    {
        var image = Snapshot!.Image;
        return SelectionMath.ToPixelRect(
            new SKPoint((float)_dragStart.X, (float)_dragStart.Y),
            new SKPoint((float)end.X, (float)end.Y),
            RenderScaling,
            new SKSizeI(image.Width, image.Height));
    }

    private void ShowSelection(SKRectI px)
    {
        var scale = RenderScaling;
        var dip = new Rect(px.Left / scale, px.Top / scale, px.Width / scale, px.Height / scale);
        Surface.Selection = dip;

        SizeText.Text = $"{px.Width} × {px.Height}";
        SizeLabel.IsVisible = true;
        SizeLabel.Measure(Size.Infinity);
        var label = SizeLabel.DesiredSize;
        var area = Surface.Bounds.Size;

        var top = dip.Bottom + LabelGap;
        if (top + label.Height > area.Height) top = dip.Top - LabelGap - label.Height;
        if (top < 0) top = dip.Top + LabelGap;
        var left = Math.Clamp(dip.Left, 0, Math.Max(0, area.Width - label.Width));
        top = Math.Clamp(top, 0, Math.Max(0, area.Height - label.Height));
        Canvas.SetLeft(SizeLabel, left);
        Canvas.SetTop(SizeLabel, top);
    }

    private void ResetSelection()
    {
        Surface.Selection = null;
        SizeLabel.IsVisible = false;
        Hint.IsVisible = true;
    }
}
