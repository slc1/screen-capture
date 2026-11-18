using SkiaSharp;

namespace ScreenCapture.Platform;

public interface IClipboardImageService
{
    bool TrySetImage(SKBitmap image);
}
