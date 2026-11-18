using System.Runtime.InteropServices;
using ScreenCapture.Platform;
using ScreenCapture.Platform.Windows;

namespace ScreenCapture.Platform.Windows.Tests;

/// <summary>
/// Makes the test process Per-Monitor-V2 DPI aware before any capture. Real per-monitor
/// correctness on mixed-DPI setups is verified manually later; these tests only check
/// self-consistency (image size == reported physical bounds).
/// </summary>
public sealed class DpiAwareFixture
{
    public DpiAwareFixture()
    {
        // Fails harmlessly if awareness was already set for this process.
        _ = SetProcessDpiAwarenessContext(-4);
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(nint value);
}

public class GdiScreenCapturerTests(Xunit.Abstractions.ITestOutputHelper output) : IClassFixture<DpiAwareFixture>
{
    private readonly GdiScreenCapturer _capturer = new();

    [Fact]
    public void GetMonitors_ReturnsAtLeastOnePrimary()
    {
        var monitors = _capturer.GetMonitors();
        foreach (var m in monitors) output.WriteLine($"{m.DeviceName} {m.BoundsPx} scale={m.Scale} primary={m.IsPrimary}");
        Assert.NotEmpty(monitors);
        Assert.Contains(monitors, m => m.IsPrimary);
        Assert.All(monitors, m => Assert.True(m.Scale >= 1.0));
    }

    [Fact]
    public void CaptureAll_ImageSizesMatchPhysicalBounds()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var snaps = _capturer.CaptureAllMonitors();
        output.WriteLine($"CaptureAll took {sw.ElapsedMilliseconds} ms");
        Assert.Equal(_capturer.GetMonitors().Count, snaps.Count);
        foreach (var s in snaps)
        {
            using var img = s.Image;
            Assert.Equal(s.Monitor.BoundsPx.Width, img.Width);
            Assert.Equal(s.Monitor.BoundsPx.Height, img.Height);
        }
    }

    [Fact]
    public void CaptureAll_ImagesAreOpaque()
    {
        foreach (var s in _capturer.CaptureAllMonitors())
        {
            using var img = s.Image;
            Assert.Equal(SkiaSharp.SKAlphaType.Opaque, img.AlphaType);
            var stepX = Math.Max(1, img.Width / 16);
            var stepY = Math.Max(1, img.Height / 16);
            for (var y = 0; y < img.Height; y += stepY)
                for (var x = 0; x < img.Width; x += stepX)
                    Assert.Equal(255, img.GetPixel(x, y).Alpha);
        }
    }

    [Fact]
    public void CaptureMonitorAtCursor_ReturnsAMonitorFromList()
    {
        var monitors = _capturer.GetMonitors();
        var m = _capturer.CaptureMonitorAtCursor();
        using var _ = m.Image;
        Assert.Contains(monitors, x => x == m.Monitor);
        Assert.Equal(m.Monitor.BoundsPx.Width, m.Image.Width);
    }
}
