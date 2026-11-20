using System.Text;

namespace ScreenCapture.Core.Ocr;

/// <summary>The temporary .txt files recognized text is opened from.</summary>
public static class OcrTextFiles
{
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(1);

    private static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string FileName(DateTime now, int attempt = 1) =>
        attempt <= 1 ? $"Text {now:yyyy-MM-dd HH-mm-ss}.txt" : $"Text {now:yyyy-MM-dd HH-mm-ss} ({attempt}).txt";

    /// <summary>Writes UTF-8 with BOM (so older editors detect Cyrillic correctly); never overwrites a file.</summary>
    public static string Write(string folder, DateTime now, string text)
    {
        Directory.CreateDirectory(folder);
        for (var attempt = 1; ; attempt++)
        {
            var path = Path.Combine(folder, FileName(now, attempt));
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream, Utf8WithBom);
                writer.Write(text);
                return path;
            }
            catch (IOException) when (File.Exists(path) && attempt < 100)
            {
            }
        }
    }

    /// <summary>Best effort: a file that is open elsewhere or already gone is skipped.</summary>
    public static void DeleteOlderThan(string folder, DateTime now, TimeSpan age)
    {
        if (!Directory.Exists(folder))
            return;
        foreach (var file in Directory.EnumerateFiles(folder, "Text *.txt"))
        {
            try
            {
                if (now - File.GetLastWriteTime(file) > age)
                    File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
