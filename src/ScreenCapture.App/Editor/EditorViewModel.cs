using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenCapture.Core.Annotations;
using ScreenCapture.App.Services;
using ScreenCapture.Core.Diagnostics;
using ScreenCapture.Core.Editing;
using ScreenCapture.Core.Export;
using ScreenCapture.Core.Rendering;
using SkiaSharp;

namespace ScreenCapture.App.Editor;

public interface IEditorHost
{
    void CommitPendingText();

    void CloseEditor();

    /// <summary>Returns the chosen path, or null when the picker was cancelled.</summary>
    Task<string?> PickSavePathAsync(string folder, string suggestedName, ImageFormatKind defaultFormat);

    /// <summary>Returns the chosen quality, or null when cancelled.</summary>
    Task<int?> AskJpegQualityAsync(int initial);
}

public sealed partial class ColorSwatch(SKColor value) : ObservableObject
{
    public SKColor Value { get; } = value;

    public IBrush Brush { get; } = new ImmutableSolidColorBrush(new Color(value.Alpha, value.Red, value.Green, value.Blue));

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class EditorViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly IEditorHost _host;

    public EditorViewModel(EditorSession session, AppServices services, IEditorHost host)
    {
        Session = session;
        _services = services;
        _host = host;
        Swatches = AnnotationPalette.Colors.Select(c => new ColorSwatch(c)).ToList();
        _tool = session.Tool;
        _width = session.Width;
        SelectColor(Swatches.FirstOrDefault(s => s.Value == session.Color) ?? Swatches[0]);
        session.Document.Changed += (_, _) =>
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        };
    }

    public EditorSession Session { get; }

    public IReadOnlyList<ColorSwatch> Swatches { get; }

    [ObservableProperty]
    private EditorTool _tool;

    [ObservableProperty]
    private StrokeWidth _width;

    partial void OnToolChanged(EditorTool value) => Session.Tool = value;

    partial void OnWidthChanged(StrokeWidth value) => Session.Width = value;

    [RelayCommand]
    private void SelectTool(EditorTool tool) => Tool = tool;

    [RelayCommand]
    private void SelectWidth(StrokeWidth width) => Width = width;

    [RelayCommand]
    private void SelectColor(ColorSwatch swatch)
    {
        foreach (var s in Swatches)
            s.IsSelected = ReferenceEquals(s, swatch);
        Session.Color = swatch.Value;
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Document.Undo();

    private bool CanUndo() => Session.Document.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Document.Redo();

    private bool CanRedo() => Session.Document.CanRedo;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(CopyCommand))]
    private bool _isBusy;

    private bool CanExport() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            _host.CommitPendingText();
            var settings = _services.Settings;
            var folder = SaveTarget.EnsureFolder(
                settings.SaveFolder, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            var suggested = ImageExporter.DefaultFileName(DateTime.Now, settings.DefaultFormat);

            var picked = await _host.PickSavePathAsync(folder, suggested, settings.DefaultFormat);
            if (picked is null)
                return;

            var (path, format) = SaveTarget.Resolve(picked, settings.DefaultFormat);
            var quality = settings.JpgQuality;
            if (format == ImageFormatKind.Jpg)
            {
                if (await _host.AskJpegQualityAsync(quality) is not { } chosen)
                    return;
                quality = chosen;
            }

            // Flattened on the UI thread so the background encode never touches the window's own bitmap.
            using var flat = AnnotationRenderer.Flatten(Session.Image, Session.Document.Annotations.ToList());
            await Task.Run(() => ImageExporter.Save(flat, path, format, quality));
        }
        catch (Exception ex)
        {
            FileLog.Error("Save failed", ex);
            _services.Notifier.ShowError(_services.Loc.Format("Error_SaveFailed", ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task CopyAsync()
    {
        IsBusy = true;
        try
        {
            _host.CommitPendingText();
            using var flat = AnnotationRenderer.Flatten(Session.Image, Session.Document.Annotations.ToList());
            if (_services.Clipboard.TrySetImage(flat))
                _host.CloseEditor();
            else
                _services.Notifier.ShowError(_services.Loc["Error_ClipboardFailed"]);
        }
        catch (Exception ex)
        {
            FileLog.Error("Copy failed", ex);
            _services.Notifier.ShowError(_services.Loc["Error_ClipboardFailed"]);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
