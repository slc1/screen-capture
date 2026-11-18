using SkiaSharp;

namespace ScreenCapture.Platform;

public interface IClipboardImageService
{
    bool TrySetImage(SKBitmap image);

    /// <summary>Why the last <see cref="TrySetImage"/> returned false; null after a success.</summary>
    string? LastError { get; }
}
