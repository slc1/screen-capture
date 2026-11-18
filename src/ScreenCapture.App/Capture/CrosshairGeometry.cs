namespace ScreenCapture.App.Capture;

/// <summary>
/// Pixels of the small selection crosshair, in device pixels, and the colour each one is drawn in.
/// </summary>
public static class CrosshairGeometry
{
    /// <summary>Uncovered device pixels between the target pixel and the start of each arm.</summary>
    public const int Gap = 3;

    public const int ArmLength = 12;

    /// <summary>
    /// The pointer pixel is floor(DIP * scale). Each of the four arms covers ArmLength pixels starting at
    /// offset Gap + 1 from it (so Gap pixels stay free). Pixels outside [0, width) x [0, height) are omitted.
    /// Order: left, right, up, down; each arm from the nearest pixel outward.
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> ArmPixels(double xDip, double yDip, double scale, int widthPx, int heightPx)
    {
        var px = (int)Math.Floor(xDip * scale);
        var py = (int)Math.Floor(yDip * scale);
        var pixels = new List<(int, int)>(4 * ArmLength);
        for (var d = Gap + 1; d <= Gap + ArmLength; d++) Add(px - d, py);
        for (var d = Gap + 1; d <= Gap + ArmLength; d++) Add(px + d, py);
        for (var d = Gap + 1; d <= Gap + ArmLength; d++) Add(px, py - d);
        for (var d = Gap + 1; d <= Gap + ArmLength; d++) Add(px, py + d);
        return pixels;

        void Add(int x, int y)
        {
            if (x >= 0 && y >= 0 && x < widthPx && y < heightPx) pixels.Add((x, y));
        }
    }

    /// <summary>
    /// True when the arm pixel must be black: the colour displayed there (the frozen pixel, darkened by the
    /// overlay's 50 % black dim when <paramref name="dimmed"/>) has WCAG relative luminance above 0.5.
    /// </summary>
    public static bool UseBlack(byte r, byte g, byte b, bool dimmed)
    {
        const double keep = 1 - 128 / 255.0;
        var k = dimmed ? keep : 1;
        var luminance = 0.2126 * Linear(r * k / 255) + 0.7152 * Linear(g * k / 255) + 0.0722 * Linear(b * k / 255);
        return luminance > 0.5;
    }

    private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
