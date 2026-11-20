using SkiaSharp;

namespace ScreenCapture.Platform;

public interface IOcrService
{
    /// <summary>
    /// Recognizes the text in <paramref name="image"/> (any screenshot; the service does its own preprocessing).
    /// Returns cleaned text with CRLF line ends, or an empty string when there is none. Blocking; call it off the
    /// UI thread. Throws <see cref="OcrUnavailableException"/> when the engine cannot be loaded.
    /// </summary>
    string Recognize(SKBitmap image);
}

public enum OcrUnavailableReason
{
    /// <summary>The Microsoft Visual C++ runtime the native engine needs is not installed.</summary>
    RuntimeMissing,

    /// <summary>The engine files could not be prepared or loaded; see the inner exception.</summary>
    LoadFailed,
}

public sealed class OcrUnavailableException(OcrUnavailableReason reason, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public OcrUnavailableReason Reason { get; } = reason;
}
