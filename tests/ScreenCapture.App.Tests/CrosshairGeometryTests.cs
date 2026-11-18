using ScreenCapture.App.Capture;

namespace ScreenCapture.App.Tests;

public sealed class CrosshairGeometryTests
{
    [Fact]
    public void Arms_start_four_pixels_from_the_pointer_pixel_and_are_twelve_long()
    {
        // 100.4 * 1.5 = 150.6 -> 150; 50.6 * 1.5 = 75.9 -> 75.
        var p = CrosshairGeometry.ArmPixels(100.4, 50.6, 1.5, 3000, 1000);

        Assert.Equal(48, p.Count);
        Assert.Equal((146, 75), p[0]);   // left arm, nearest
        Assert.Equal((135, 75), p[11]);  // left arm, farthest
        Assert.Equal((154, 75), p[12]);  // right arm, nearest
        Assert.Equal((150, 71), p[24]);  // up arm, nearest
        Assert.Equal((150, 79), p[36]);  // down arm, nearest
        Assert.DoesNotContain(p, q => Math.Abs(q.X - 150) <= 3 && Math.Abs(q.Y - 75) <= 3);
    }

    [Fact]
    public void Pixels_outside_the_overlay_are_dropped()
    {
        var p = CrosshairGeometry.ArmPixels(2, 2, 1, 100, 100);

        Assert.Equal(24, p.Count); // left and up arms are fully outside
        Assert.All(p, q => Assert.True(q.X >= 0 && q.Y >= 0 && q.X < 100 && q.Y < 100));
    }

    [Theory]
    [InlineData(255, 255, 255, false, true)]  // white, undimmed -> black
    [InlineData(0, 0, 0, false, false)]       // black -> white
    [InlineData(255, 255, 255, true, false)]  // white dimmed to 50 % grey (luminance ~0.21) -> white
    [InlineData(200, 200, 200, false, true)]  // luminance ~0.58
    [InlineData(180, 180, 180, false, false)] // luminance ~0.46
    public void Arm_colour_follows_the_displayed_luminance(byte r, byte g, byte b, bool dimmed, bool black) =>
        Assert.Equal(black, CrosshairGeometry.UseBlack(r, g, b, dimmed));
}
