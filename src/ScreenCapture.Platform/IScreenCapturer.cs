using SkiaSharp;

namespace ScreenCapture.Platform;

public sealed record MonitorInfo(string DeviceName, SKRectI BoundsPx, double Scale, bool IsPrimary);

/// <summary>Image size equals BoundsPx size; pixels are opaque.</summary>
public sealed record ScreenSnapshot(MonitorInfo Monitor, SKBitmap Image);

public interface IScreenCapturer
{
    IReadOnlyList<MonitorInfo> GetMonitors();
    IReadOnlyList<ScreenSnapshot> CaptureAllMonitors();
    ScreenSnapshot CaptureMonitorAtCursor();
}
