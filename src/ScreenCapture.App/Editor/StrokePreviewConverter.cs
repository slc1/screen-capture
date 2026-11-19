using System.Globalization;
using Avalonia.Data.Converters;
using ScreenCapture.Core.Annotations;

namespace ScreenCapture.App.Editor;

/// <summary>Toolbar preview line thickness (DIP) for a stroke width; capped so 12 px still fits a 32 px button.</summary>
public sealed class StrokePreviewConverter : IValueConverter
{
    public static StrokePreviewConverter Instance { get; } = new();

    public static double Thickness(StrokeWidth width) => width switch
    {
        StrokeWidth.ExtraThin => 1,
        StrokeWidth.Thin => 1.5,
        StrokeWidth.Medium => 2.5,
        StrokeWidth.Thick => 4,
        StrokeWidth.ExtraThick => 6,
        _ => 2.5,
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is StrokeWidth w ? Thickness(w) : 2.5;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
