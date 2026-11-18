using Avalonia.Controls;
using Avalonia.Media.Imaging;
using ScreenCapture.App.Common;
using ScreenCapture.App.Services;
using SkiaSharp;

namespace ScreenCapture.App.Editor;

public partial class EditorWindow : Window
{
    private SKBitmap? _image;
    private Bitmap? _preview;

    public EditorWindow() => InitializeComponent();

    /// <summary>Takes ownership of <paramref name="image"/> and disposes it when the window closes.</summary>
    public EditorWindow(SKBitmap image, AppServices services) : this()
    {
        _image = image;
        _preview = SkiaBitmapConversion.ToAvalonia(image);
        Preview.Source = _preview;
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ReleaseImages();
    }

    public void ReleaseImages()
    {
        Preview.Source = null;
        _preview?.Dispose();
        _preview = null;
        _image?.Dispose();
        _image = null;
    }
}
