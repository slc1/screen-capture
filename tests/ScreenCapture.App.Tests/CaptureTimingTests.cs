using ScreenCapture.App.Capture;

namespace ScreenCapture.App.Tests;

public sealed class CaptureTimingTests
{
    [Fact]
    public void Summary_lists_first_occurrence_of_each_stage_in_order()
    {
        var now = TimeSpan.Zero;
        var timing = new CaptureTiming("area, hotkey", () => now);

        now = TimeSpan.FromMilliseconds(12.4);
        timing.Mark("captured");
        now = TimeSpan.FromMilliseconds(80);
        timing.Mark("overlay opened");
        now = TimeSpan.FromMilliseconds(95);
        timing.Mark("overlay opened");
        now = TimeSpan.FromMilliseconds(120.6);
        timing.Complete("overlay first frame");
        now = TimeSpan.FromMilliseconds(500);
        timing.Mark("late");
        timing.Complete("again");

        Assert.Equal("Capture timing (area, hotkey): captured 12 ms, overlay opened 80 ms, overlay first frame 121 ms", timing.Summary);
    }
}
