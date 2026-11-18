namespace ScreenCapture.Core.Export;

public static class SaveTarget
{
    /// <summary>Format follows the picked extension; a name without one gets the default format's extension appended.</summary>
    public static (string Path, ImageFormatKind Format) Resolve(string pickedPath, ImageFormatKind defaultFormat)
    {
        var ext = Path.GetExtension(pickedPath);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            return (pickedPath, ImageExporter.FormatFromPath(pickedPath));

        var suffix = defaultFormat == ImageFormatKind.Jpg ? ".jpg" : ".png";
        return (pickedPath + suffix, defaultFormat);
    }

    /// <summary>Creates <paramref name="configured"/> if needed; returns <paramref name="fallback"/> when that is impossible.</summary>
    public static string EnsureFolder(string configured, string fallback)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(configured))
            {
                Directory.CreateDirectory(configured);
                return configured;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }

        return fallback;
    }
}
