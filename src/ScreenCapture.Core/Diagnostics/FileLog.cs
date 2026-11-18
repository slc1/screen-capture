namespace ScreenCapture.Core.Diagnostics;

public static class FileLog
{
    private static readonly object Gate = new();
    private static string? _path;

    public static void Init(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _path = path;
        }
        catch
        {
            // Logging must never break the app.
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            var path = _path;
            if (path is null) return;
            lock (Gate)
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never break the app.
        }
    }
}
