using Avalonia;
using ScreenCapture.App.Common;

namespace ScreenCapture.App.Tests;

public sealed class MessageWindowPlacementTests
{
    private static readonly PixelRect Screen = new(0, 0, 1920, 1040);

    [Fact]
    public void Centres_in_the_anchor() =>
        Assert.Equal(new PixelPoint(300, 250), MessageWindow.CenterIn(new PixelRect(100, 100, 1000, 500), new PixelSize(600, 200), Screen));

    [Fact]
    public void Stays_on_screen_when_the_anchor_is_near_an_edge() =>
        Assert.Equal(new PixelPoint(1320, 0), MessageWindow.CenterIn(new PixelRect(1800, -100, 200, 100), new PixelSize(600, 200), Screen));

    [Fact]
    public void Centres_on_a_secondary_screen_with_negative_coordinates()
    {
        var left = new PixelRect(-1280, 0, 1280, 1024);
        Assert.Equal(new PixelPoint(-940, 412), MessageWindow.CenterIn(left, new PixelSize(600, 200), left));
    }
}
