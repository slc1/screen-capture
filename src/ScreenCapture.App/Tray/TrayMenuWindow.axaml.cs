using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ScreenCapture.App.Common;

namespace ScreenCapture.App.Tray;

public partial class TrayMenuWindow : Window
{
    private readonly Action _captureArea;
    private readonly Action _captureFullScreen;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private readonly PixelPoint _anchor;

    public TrayMenuWindow()
    {
        _captureArea = _captureFullScreen = _openSettings = _exit = () => { };
        InitializeComponent();
        Opacity = 0;
    }

    public TrayMenuWindow(
        string? areaHotkey,
        string? fullScreenHotkey,
        Action captureArea,
        Action captureFullScreen,
        Action openSettings,
        Action exit) : this()
    {
        AreaHotkey.Text = areaHotkey;
        FullHotkey.Text = fullScreenHotkey;
        _captureArea = captureArea;
        _captureFullScreen = captureFullScreen;
        _openSettings = openSettings;
        _exit = exit;
        _anchor = NativeCursor.Position() ?? new PixelPoint(0, 0);
        Opened += (_, _) => PlaceNearAnchor();
        Deactivated += (_, _) => Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape) Close();
    }

    private void OnArea(object? sender, RoutedEventArgs e) => Run(_captureArea);
    private void OnFullScreen(object? sender, RoutedEventArgs e) => Run(_captureFullScreen);
    private void OnSettings(object? sender, RoutedEventArgs e) => Run(_openSettings);
    private void OnExit(object? sender, RoutedEventArgs e) => Run(_exit);

    private void Run(Action action)
    {
        Close();
        action();
    }

    private void PlaceNearAnchor()
    {
        var screen = Screens.ScreenFromPoint(_anchor) ?? Screens.Primary;
        if (screen is null)
        {
            Opacity = 1;
            return;
        }

        var area = screen.WorkingArea;
        var w = (int)Math.Ceiling(Bounds.Width * screen.Scaling);
        var h = (int)Math.Ceiling(Bounds.Height * screen.Scaling);

        // The tray sits at the bottom-right, so prefer opening up and to the left of the cursor.
        var x = _anchor.X + w <= area.Right ? _anchor.X : _anchor.X - w;
        var y = _anchor.Y - h >= area.Y ? _anchor.Y - h : _anchor.Y;
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - w));
        y = Math.Clamp(y, area.Y, Math.Max(area.Y, area.Bottom - h));
        Position = new PixelPoint(x, y);
        Opacity = 1;
        NativeForeground.Bring(this);
    }
}
