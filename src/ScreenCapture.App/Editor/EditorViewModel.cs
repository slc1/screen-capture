using Avalonia.Media;
using Avalonia.Media.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenCapture.Core.Annotations;
using ScreenCapture.Core.Editing;
using SkiaSharp;

namespace ScreenCapture.App.Editor;

public sealed partial class ColorSwatch(SKColor value) : ObservableObject
{
    public SKColor Value { get; } = value;

    public IBrush Brush { get; } = new ImmutableSolidColorBrush(new Color(value.Alpha, value.Red, value.Green, value.Blue));

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class EditorViewModel : ObservableObject
{
    public EditorViewModel(EditorSession session)
    {
        Session = session;
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

    // Save and Copy are wired in a later task; until then the buttons stay disabled.
    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Save()
    {
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void Copy()
    {
    }

    private static bool CanExport() => false;
}
