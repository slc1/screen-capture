using System.Globalization;
using Avalonia.Data.Converters;

namespace ScreenCapture.App.Common;

/// <summary>True when the bound value equals the converter parameter.</summary>
public sealed class EqualsConverter : IValueConverter
{
    public static EqualsConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Equals(value, parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
