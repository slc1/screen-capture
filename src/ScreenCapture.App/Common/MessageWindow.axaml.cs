using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ScreenCapture.App.Common;

public partial class MessageWindow : Window
{
    private readonly Window? _anchor;

    public MessageWindow() => InitializeComponent();

    /// <summary>Opens centred on <paramref name="anchor"/> when it is visible, else on the primary screen.</summary>
    public MessageWindow(string message, Window? anchor = null) : this()
    {
        MessageText.Text = message;
        _anchor = anchor;
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Hidden until placed, because the final height is only known once the content is measured.
        Opacity = 0;
        Opened += (_, _) => PlaceAndReveal();
    }

    /// <summary>Top-left that centres <paramref name="size"/> in <paramref name="area"/>, kept inside <paramref name="screen"/>.</summary>
    public static PixelPoint CenterIn(PixelRect area, PixelSize size, PixelRect screen)
    {
        var x = area.X + (area.Width - size.Width) / 2;
        var y = area.Y + (area.Height - size.Height) / 2;
        x = Math.Clamp(x, screen.X, Math.Max(screen.X, screen.Right - size.Width));
        y = Math.Clamp(y, screen.Y, Math.Max(screen.Y, screen.Bottom - size.Height));
        return new PixelPoint(x, y);
    }

    private void PlaceAndReveal()
    {
        try
        {
            var area = AnchorRect();
            var screen = area is { } a ? Screens.ScreenFromPoint(a.Center) : null;
            screen ??= Screens.Primary;
            if (screen is not null)
            {
                var scale = screen.Scaling;
                var frame = FrameSize ?? Bounds.Size;
                var size = new PixelSize((int)Math.Ceiling(frame.Width * scale), (int)Math.Ceiling(frame.Height * scale));
                Position = CenterIn(area ?? screen.WorkingArea, size, screen.WorkingArea);
            }
        }
        finally
        {
            Opacity = 1;
        }
    }

    private PixelRect? AnchorRect()
    {
        if (_anchor is not { IsVisible: true } anchor) return null;
        var frame = anchor.FrameSize ?? anchor.Bounds.Size;
        var scale = anchor.DesktopScaling;
        return new PixelRect(anchor.Position,
            new PixelSize((int)Math.Round(frame.Width * scale), (int)Math.Round(frame.Height * scale)));
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
