using ScreenCapture.Core.Editing;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public sealed class EditorSessionRecognizeTests : IDisposable
{
    private readonly SKBitmap _image = new(200, 100);

    public void Dispose() => _image.Dispose();

    private (EditorSession Session, List<SKRectI> Requests) NewSession()
    {
        var s = new EditorSession(_image) { Tool = EditorTool.Recognize };
        var requests = new List<SKRectI>();
        s.RecognizeRequested += (_, r) => requests.Add(r);
        return (s, requests);
    }

    [Fact]
    public void Drag_requests_the_dragged_area_in_whole_pixels_and_adds_nothing()
    {
        var (s, requests) = NewSession();

        s.PointerDown(new SKPoint(50.6f, 40.2f));
        s.PointerMove(new SKPoint(20f, 10f));
        Assert.Equal(new SKRect(20f, 10f, 50.6f, 40.2f), s.RecognizeArea);
        s.PointerUp(new SKPoint(10.5f, 5.5f));

        Assert.Equal([new SKRectI(10, 5, 51, 41)], requests);
        Assert.Null(s.RecognizeArea);
        Assert.Empty(s.Document.Annotations);
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void Area_is_clamped_to_the_image()
    {
        var (s, requests) = NewSession();

        s.PointerDown(new SKPoint(-30f, 50f));
        s.PointerUp(new SKPoint(500f, 500f));

        Assert.Equal([new SKRectI(0, 50, 200, 100)], requests);
    }

    [Theory]
    [InlineData(10f, 10f, 10f, 10f)]
    [InlineData(10f, 10f, 12f, 60f)]
    [InlineData(-50f, -50f, -10f, -10f)]
    public void Click_or_tiny_drag_requests_the_whole_image(float x1, float y1, float x2, float y2)
    {
        var (s, requests) = NewSession();

        s.PointerDown(new SKPoint(x1, y1));
        s.PointerUp(new SKPoint(x2, y2));

        Assert.Equal([new SKRectI(0, 0, 200, 100)], requests);
    }

    [Fact]
    public void Cancel_drops_the_area_without_a_request()
    {
        var (s, requests) = NewSession();

        s.PointerDown(new SKPoint(10f, 10f));
        s.PointerMove(new SKPoint(80f, 60f));
        s.Cancel();
        s.PointerUp(new SKPoint(80f, 60f));

        Assert.Empty(requests);
        Assert.Null(s.RecognizeArea);
    }

    [Fact]
    public void CopyForRecognition_keeps_blur_but_leaves_out_drawn_shapes()
    {
        _image.Erase(SKColors.White);
        for (var x = 0; x < 200; x += 2)
            for (var y = 0; y < 100; y++)
                _image.SetPixel(x, y, SKColors.Black);
        var s = new EditorSession(_image) { Tool = EditorTool.Blur };
        s.PointerDown(new SKPoint(0, 0));
        s.PointerUp(new SKPoint(50, 50));
        s.Tool = EditorTool.Rectangle;
        s.Color = SKColors.Red;
        s.PointerDown(new SKPoint(100, 10));
        s.PointerUp(new SKPoint(190, 90));
        Assert.Equal(2, s.Document.Annotations.Count);

        using var copy = s.CopyForRecognition(new SKRectI(0, 0, 200, 100));

        Assert.Equal(200, copy.Width);
        Assert.Equal(100, copy.Height);
        // Inside the blur the 1-px stripes are gone; elsewhere the original stripes are intact and no red is drawn.
        Assert.NotEqual(copy.GetPixel(20, 20), _image.GetPixel(20, 20));
        Assert.Equal(_image.GetPixel(100, 10), copy.GetPixel(100, 10));
        Assert.Equal(_image.GetPixel(101, 10), copy.GetPixel(101, 10));
        Assert.Equal(SKColors.White, _image.GetPixel(21, 20));
    }

    [Fact]
    public void CopyForRecognition_returns_only_the_area()
    {
        var s = new EditorSession(_image);

        using var copy = s.CopyForRecognition(new SKRectI(10, 20, 40, 30));

        Assert.Equal(30, copy.Width);
        Assert.Equal(10, copy.Height);
    }

    [Fact]
    public void Area_is_raised_after_the_session_is_back_to_idle()
    {
        var (s, _) = NewSession();
        SKRect? areaDuringRequest = new SKRect(1, 1, 1, 1);
        s.RecognizeRequested += (_, _) => areaDuringRequest = s.RecognizeArea;

        s.PointerDown(new SKPoint(10f, 10f));
        s.PointerUp(new SKPoint(80f, 60f));

        Assert.Null(areaDuringRequest);
    }
}
