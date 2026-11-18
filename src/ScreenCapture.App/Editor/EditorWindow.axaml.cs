using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform.Storage;
using ScreenCapture.App.Common;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Annotations;
using ScreenCapture.Core.Editing;
using ScreenCapture.Core.Export;
using SkiaSharp;

namespace ScreenCapture.App.Editor;

public partial class EditorWindow : Window, IEditorHost
{
    public const double ToolbarHeight = 44;

    private SKBitmap? _image;
    private EditorSession? _session;
    private TextEdit? _textEdit;

    public EditorWindow() => InitializeComponent();

    /// <summary>Takes ownership of <paramref name="image"/> and disposes it when the window closes.</summary>
    public EditorWindow(SKBitmap image, AppServices services) : this()
    {
        _image = image;
        _session = new EditorSession(image);
        DataContext = new EditorViewModel(_session, services, this);
        Surface.Session = _session;
        Surface.TextRequested += BeginText;
        PlaceOnScreen(image.Width, image.Height);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _session is null || _textEdit is not null)
            return;

        if (e.KeyModifiers == KeyModifiers.Control && e.Key is Key.C or Key.S && DataContext is EditorViewModel vm)
        {
            var command = e.Key == Key.C ? vm.CopyCommand : vm.SaveCommand;
            if (command.CanExecute(null))
                command.Execute(null);
            e.Handled = true;
            return;
        }

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (ctrl && e.Key == Key.Z && !shift)
            _session.Document.Undo();
        else if (ctrl && (e.Key == Key.Y || (e.Key == Key.Z && shift)))
            _session.Document.Redo();
        else if (e.Key == Key.Delete && e.KeyModifiers == KeyModifiers.None)
            _session.DeleteSelected();
        else if (e.Key == Key.Escape)
            _session.Cancel();
        else
            return;

        e.Handled = true;
    }

    void IEditorHost.CommitPendingText() => CommitText();

    void IEditorHost.CloseEditor() => Close();

    async Task<string?> IEditorHost.PickSavePathAsync(string folder, string suggestedName, ImageFormatKind defaultFormat)
    {
        var png = new FilePickerFileType("PNG") { Patterns = ["*.png"] };
        var jpg = new FilePickerFileType("JPG") { Patterns = ["*.jpg", "*.jpeg"] };
        var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(folder),
            DefaultExtension = defaultFormat == ImageFormatKind.Jpg ? "jpg" : "png",
            FileTypeChoices = defaultFormat == ImageFormatKind.Jpg ? [jpg, png] : [png, jpg],
        });
        return result?.TryGetLocalPath();
    }

    Task<int?> IEditorHost.AskJpegQualityAsync(int initial) => JpegQualityDialog.ShowAsync(this, initial);

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ReleaseImages();
    }

    public void ReleaseImages()
    {
        CloseTextBox();
        Surface.Dispose();
        DataContext = null;
        _session = null;
        _image?.Dispose();
        _image = null;
    }

    /// <summary>Sizes the client area to the image (1 image px = 1 device px) within 90 % of the work area, centered.</summary>
    private void PlaceOnScreen(int imageWidth, int imageHeight)
    {
        var screen = (NativeCursor.Position() is { } cursor ? Screens.ScreenFromPoint(cursor) : null) ?? Screens.Primary;
        if (screen is null)
        {
            Width = MinWidth;
            Height = MinHeight;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }

        var scaling = screen.Scaling;
        var work = screen.WorkingArea;
        var maxWidth = Math.Max(MinWidth, work.Width * 0.9 / scaling);
        var maxHeight = Math.Max(MinHeight, work.Height * 0.9 / scaling);
        Width = Math.Clamp(imageWidth / scaling, MinWidth, maxWidth);
        Height = Math.Clamp(imageHeight / scaling + ToolbarHeight, MinHeight, maxHeight);
        Position = new PixelPoint(
            work.X + (int)((work.Width - Width * scaling) / 2),
            work.Y + (int)((work.Height - Height * scaling) / 2));
    }

    private void BeginText(SKPoint imagePoint, TextAnnotation? existing)
    {
        if (_session is null)
            return;

        CommitText();
        var color = existing?.Color ?? _session.Color;
        var fontSize = existing?.FontSize ?? _session.TextFontSize;
        var box = new TextBox
        {
            Text = existing?.Text ?? string.Empty,
            AcceptsReturn = false,
            FontSize = Math.Max(8, fontSize * Surface.ImageToDipScale),
            Foreground = new ImmutableSolidColorBrush(new Color(color.Alpha, color.Red, color.Green, color.Blue)),
            MinWidth = 80,
            MinHeight = 0,
            Padding = new Thickness(2, 0),
        };
        var at = Surface.ImageToDip(imagePoint);
        Canvas.SetLeft(box, at.X);
        Canvas.SetTop(box, at.Y);
        box.AddHandler(KeyDownEvent, OnTextBoxKeyDown, RoutingStrategies.Tunnel);
        box.LostFocus += (_, _) => CommitText();

        _textEdit = new TextEdit(box, imagePoint, existing);
        TextLayer.Children.Add(box);
        box.Focus();
        box.CaretIndex = box.Text.Length;
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseTextBox();
            e.Handled = true;
        }
    }

    private void CommitText()
    {
        var edit = CloseTextBox();
        if (edit is null || _session is null)
            return;

        var text = edit.Box.Text ?? string.Empty;
        if (edit.Existing is { } existing)
            _session.ReplaceText(existing.Id, text);
        else
            _session.AddText(edit.Position, text);
    }

    private TextEdit? CloseTextBox()
    {
        var edit = _textEdit;
        if (edit is null)
            return null;

        // Cleared first: removing the box raises LostFocus, which calls back into CommitText.
        _textEdit = null;
        TextLayer.Children.Remove(edit.Box);
        Surface.Focus();
        return edit;
    }

    private sealed record TextEdit(TextBox Box, SKPoint Position, TextAnnotation? Existing);
}
