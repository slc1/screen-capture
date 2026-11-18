using ScreenCapture.Core.Annotations;
using ScreenCapture.Core.Editing;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public sealed class EditorSessionTests : IDisposable
{
    private readonly SKBitmap _image = new(200, 100);

    public void Dispose() => _image.Dispose();

    private EditorSession NewSession(EditorTool tool = EditorTool.Arrow) => new(_image) { Tool = tool };

    private static void Drag(EditorSession s, SKPoint from, SKPoint to)
    {
        s.PointerDown(from);
        s.PointerMove(new SKPoint((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        s.PointerMove(to);
        s.PointerUp(to);
    }

    [Fact]
    public void Defaults_MatchSpec()
    {
        var s = new EditorSession(_image);
        Assert.Equal(EditorTool.Arrow, s.Tool);
        Assert.Equal(AnnotationPalette.Default, s.Color);
        Assert.Equal(StrokeWidth.Medium, s.Width);
        Assert.Equal(4f, s.HitTolerance);
        Assert.Null(s.Draft);
        Assert.Null(s.SelectedId);
    }

    [Fact]
    public void Drag_WithRectangleTool_AddsOneRectangle()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));

        var rect = Assert.IsType<RectangleAnnotation>(Assert.Single(s.Document.Annotations));
        Assert.Equal(new SKRect(10, 10, 50, 40), rect.Rect);
        Assert.Equal(s.Color, rect.Color);
        Assert.Equal(s.Width, rect.Width);
        Assert.Null(s.Draft);
        s.Document.Undo();
        Assert.Empty(s.Document.Annotations);
        Assert.False(s.Document.CanUndo);
    }

    [Theory]
    [InlineData(EditorTool.Arrow, typeof(ArrowAnnotation))]
    [InlineData(EditorTool.Line, typeof(LineAnnotation))]
    [InlineData(EditorTool.Marker, typeof(MarkerAnnotation))]
    [InlineData(EditorTool.Rectangle, typeof(RectangleAnnotation))]
    [InlineData(EditorTool.Blur, typeof(BlurAnnotation))]
    public void Drag_WithDrawingTool_AddsMatchingAnnotation(EditorTool tool, Type expected)
    {
        var s = NewSession(tool);
        Drag(s, new(10, 10), new(60, 50));
        Assert.IsType(expected, Assert.Single(s.Document.Annotations));
    }

    [Theory]
    [InlineData(EditorTool.Arrow, 2f, 2f)]
    [InlineData(EditorTool.Line, 2.9f, 0f)]
    [InlineData(EditorTool.Rectangle, 40f, 2f)]
    [InlineData(EditorTool.Blur, 2f, 40f)]
    public void TinyDrag_AddsNothing(EditorTool tool, float dx, float dy)
    {
        var s = NewSession(tool);
        Drag(s, new(10, 10), new(10 + dx, 10 + dy));
        Assert.Empty(s.Document.Annotations);
        Assert.False(s.Document.CanUndo);
        Assert.Null(s.Draft);
    }

    [Theory]
    [InlineData(EditorTool.Line, 3f, 0f)]
    [InlineData(EditorTool.Rectangle, 3f, 3f)]
    public void DragAtMinimumSize_AddsAnnotation(EditorTool tool, float dx, float dy)
    {
        var s = NewSession(tool);
        Drag(s, new(10, 10), new(10 + dx, 10 + dy));
        Assert.Single(s.Document.Annotations);
    }

    [Fact]
    public void Draft_VisibleInRenderListDuringDrag()
    {
        var s = NewSession(EditorTool.Line);
        s.PointerDown(new(10, 10));
        s.PointerMove(new(80, 30));

        var draft = Assert.IsType<LineAnnotation>(s.Draft);
        Assert.Equal(new SKPoint(80, 30), draft.End);
        Assert.Same(draft, Assert.Single(s.RenderList));
        Assert.Empty(s.Document.Annotations);
    }

    [Fact]
    public void SelectAndDrag_MovesWithSingleUndoStep()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        var original = (RectangleAnnotation)s.Document.Annotations[0];

        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        Assert.Equal(original.Id, s.SelectedId);
        s.PointerMove(new(15, 25));
        s.PointerMove(new(30, 40));

        var preview = Assert.IsType<RectangleAnnotation>(Assert.Single(s.RenderList));
        Assert.Equal(new SKRect(30, 30, 70, 60), preview.Rect);
        Assert.Equal(original, s.Document.Annotations[0]);

        s.PointerUp(new(30, 40));
        var moved = Assert.IsType<RectangleAnnotation>(Assert.Single(s.Document.Annotations));
        Assert.Equal(new SKRect(30, 30, 70, 60), moved.Rect);
        Assert.Equal(original.Id, moved.Id);

        s.Document.Undo();
        Assert.Equal(original, Assert.Single(s.Document.Annotations));
        s.Document.Undo();
        Assert.Empty(s.Document.Annotations);
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void SelectClick_WithoutMoving_CommitsNothing()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        s.Document.Undo();
        s.Document.Redo();

        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        s.PointerUp(new(10, 20));

        Assert.NotNull(s.SelectedId);
        Assert.False(s.Document.CanRedo);
        s.Document.Undo();
        Assert.Empty(s.Document.Annotations);
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void Click_OnEmpty_ClearsSelection()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        s.PointerUp(new(10, 20));
        Assert.NotNull(s.SelectedId);

        s.PointerDown(new(150, 90));
        s.PointerUp(new(150, 90));
        Assert.Null(s.SelectedId);
        Assert.Single(s.Document.Annotations);
    }

    [Fact]
    public void HitTolerance_IsUsedForSelection()
    {
        var s = NewSession(EditorTool.Line);
        Drag(s, new(10, 10), new(100, 10));
        s.Tool = EditorTool.Select;

        s.PointerDown(new(50, 22));
        s.PointerUp(new(50, 22));
        Assert.Null(s.SelectedId);

        s.HitTolerance = 12f;
        s.PointerDown(new(50, 22));
        s.PointerUp(new(50, 22));
        Assert.NotNull(s.SelectedId);
    }

    [Fact]
    public void SwitchingTool_ClearsSelection()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        s.PointerUp(new(10, 20));

        s.Tool = EditorTool.Arrow;
        Assert.Null(s.SelectedId);
    }

    [Fact]
    public void DeleteSelected_ThenUndo_Restores()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        var original = s.Document.Annotations[0];
        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        s.PointerUp(new(10, 20));

        s.DeleteSelected();
        Assert.Empty(s.Document.Annotations);
        Assert.Null(s.SelectedId);

        s.Document.Undo();
        Assert.Equal(original, Assert.Single(s.Document.Annotations));
    }

    [Fact]
    public void DeleteSelected_WithoutSelection_AddsNoUndoStep()
    {
        var s = NewSession();
        s.DeleteSelected();
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void Undo_RemovingSelectedAnnotation_ClearsSelection()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        s.PointerUp(new(10, 20));

        s.Document.Undo();
        Assert.Null(s.SelectedId);
        Assert.Null(s.Selected);
    }

    [Fact]
    public void Cancel_DuringMove_RestoresAndCommitsNothing()
    {
        var s = NewSession(EditorTool.Rectangle);
        Drag(s, new(10, 10), new(50, 40));
        var original = s.Document.Annotations[0];
        s.Tool = EditorTool.Select;
        s.PointerDown(new(10, 20));
        s.PointerMove(new(40, 50));

        s.Cancel();
        s.PointerUp(new(40, 50));

        Assert.Null(s.SelectedId);
        Assert.Equal(original, Assert.Single(s.RenderList));
        s.Document.Undo();
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void Cancel_DuringDraw_DiscardsDraft()
    {
        var s = NewSession(EditorTool.Line);
        s.PointerDown(new(10, 10));
        s.PointerMove(new(80, 30));
        s.Cancel();
        s.PointerUp(new(80, 30));

        Assert.Null(s.Draft);
        Assert.Empty(s.Document.Annotations);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void AddText_Empty_AddsNothing(string text)
    {
        var s = NewSession(EditorTool.Text);
        s.AddText(new(5, 5), text);
        Assert.Empty(s.Document.Annotations);
        Assert.False(s.Document.CanUndo);
    }

    [Theory]
    [InlineData(StrokeWidth.Thin, 16f)]
    [InlineData(StrokeWidth.Medium, 20f)]
    [InlineData(StrokeWidth.Thick, 24f)]
    public void AddText_UsesColorAndFontSizeFromWidth(StrokeWidth width, float fontSize)
    {
        var s = NewSession(EditorTool.Text);
        s.Color = AnnotationPalette.Colors[4];
        s.Width = width;
        s.AddText(new(5, 6), "hello");

        var t = Assert.IsType<TextAnnotation>(Assert.Single(s.Document.Annotations));
        Assert.Equal("hello", t.Text);
        Assert.Equal(new SKPoint(5, 6), t.Position);
        Assert.Equal(AnnotationPalette.Colors[4], t.Color);
        Assert.Equal(fontSize, t.FontSize);
    }

    [Fact]
    public void TextTool_PointerDrag_AddsNothing()
    {
        var s = NewSession(EditorTool.Text);
        Drag(s, new(10, 10), new(60, 50));
        Assert.Empty(s.Document.Annotations);
        Assert.Null(s.Draft);
    }

    [Fact]
    public void ReplaceText_IsUndoable()
    {
        var s = NewSession(EditorTool.Text);
        s.AddText(new(5, 5), "before");
        var original = (TextAnnotation)s.Document.Annotations[0];

        s.ReplaceText(original.Id, "after");
        var edited = Assert.IsType<TextAnnotation>(Assert.Single(s.Document.Annotations));
        Assert.Equal("after", edited.Text);
        Assert.Equal(original.Id, edited.Id);
        Assert.Equal(original.Position, edited.Position);

        s.Document.Undo();
        Assert.Equal(original, Assert.Single(s.Document.Annotations));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("")]
    [InlineData("  ")]
    public void ReplaceText_UnchangedOrEmpty_AddsNoUndoStep(string text)
    {
        var s = NewSession(EditorTool.Text);
        s.AddText(new(5, 5), "same");
        var id = s.Document.Annotations[0].Id;

        s.ReplaceText(id, text);
        s.Document.Undo();
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void ReplaceText_UnknownId_AddsNoUndoStep()
    {
        var s = NewSession(EditorTool.Text);
        s.ReplaceText(Guid.NewGuid(), "x");
        Assert.False(s.Document.CanUndo);
    }

    [Fact]
    public void Changed_IsRaisedForDraftUpdates()
    {
        var s = NewSession(EditorTool.Line);
        var count = 0;
        s.Changed += (_, _) => count++;
        s.PointerDown(new(10, 10));
        s.PointerMove(new(50, 10));
        Assert.True(count >= 2);
    }
}
