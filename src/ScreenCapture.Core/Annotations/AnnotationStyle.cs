using SkiaSharp;

namespace ScreenCapture.Core.Annotations;

/// <summary>Stroke thickness in image-space pixels.</summary>
public enum StrokeWidth
{
    Thin = 2,
    Medium = 4,
    Thick = 8,
}

public static class AnnotationPalette
{
    public static IReadOnlyList<SKColor> Colors { get; } = new[]
    {
        SKColor.Parse("#E53935"),
        SKColor.Parse("#FB8C00"),
        SKColor.Parse("#FDD835"),
        SKColor.Parse("#43A047"),
        SKColor.Parse("#1E88E5"),
        SKColor.Parse("#8E24AA"),
        SKColor.Parse("#000000"),
        SKColor.Parse("#FFFFFF"),
    };

    public static SKColor Default { get; } = SKColor.Parse("#E53935");
}
