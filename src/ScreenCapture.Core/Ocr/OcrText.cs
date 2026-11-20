namespace ScreenCapture.Core.Ocr;

public static class OcrText
{
    /// <summary>
    /// Trailing spaces and form feeds removed, leading/trailing blank lines dropped, runs of blank lines collapsed
    /// to one, CRLF line ends (what Notepad and most Windows apps expect when pasting).
    /// </summary>
    public static string Clean(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var lines = raw.Replace("\f", "\n").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (trimmed.Length == 0 && (result.Count == 0 || result[^1].Length == 0))
                continue;
            result.Add(trimmed);
        }

        while (result.Count > 0 && result[^1].Length == 0)
            result.RemoveAt(result.Count - 1);
        return string.Join("\r\n", result);
    }
}
