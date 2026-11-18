using ScreenCapture.Core.Annotations;
using SkiaSharp;

namespace ScreenCapture.Core.Tests;

public class AnnotationDocumentTests
{
    private static LineAnnotation Line(float x = 0) =>
        new(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new(x, 0), new(x + 10, 0));

    [Fact]
    public void Execute_Add_ThenUndo_RemovesIt()
    {
        var doc = new AnnotationDocument();
        doc.Execute(new AddAnnotationCommand(Line()));
        Assert.Single(doc.Annotations);
        Assert.True(doc.CanUndo);
        doc.Undo();
        Assert.Empty(doc.Annotations);
        Assert.False(doc.CanUndo);
    }

    [Fact]
    public void Undo_ThenRedo_RestoresIt()
    {
        var doc = new AnnotationDocument();
        var a = Line();
        doc.Execute(new AddAnnotationCommand(a));
        doc.Undo();
        Assert.True(doc.CanRedo);
        doc.Redo();
        Assert.Equal(a, Assert.Single(doc.Annotations));
    }

    [Fact]
    public void Execute_AfterUndo_ClearsRedo()
    {
        var doc = new AnnotationDocument();
        doc.Execute(new AddAnnotationCommand(Line()));
        doc.Undo();
        doc.Execute(new AddAnnotationCommand(Line(5)));
        Assert.False(doc.CanRedo);
    }

    [Fact]
    public void Remove_Undo_RestoresAtOriginalIndex()
    {
        var doc = new AnnotationDocument();
        var first = Line(0);
        var middle = Line(20);
        var last = Line(40);
        doc.Execute(new AddAnnotationCommand(first));
        doc.Execute(new AddAnnotationCommand(middle));
        doc.Execute(new AddAnnotationCommand(last));
        doc.Execute(new RemoveAnnotationCommand(middle.Id));
        Assert.Equal(2, doc.Annotations.Count);
        doc.Undo();
        Assert.Equal(3, doc.Annotations.Count);
        Assert.Equal(middle.Id, doc.Annotations[1].Id);
    }

    [Fact]
    public void Replace_Undo_RestoresBefore()
    {
        var doc = new AnnotationDocument();
        var before = Line();
        var after = (Annotation)before.MoveBy(5, 5);
        doc.Execute(new AddAnnotationCommand(before));
        doc.Execute(new ReplaceAnnotationCommand(before, after));
        Assert.Equal(after, doc.Annotations[0]);
        doc.Undo();
        Assert.Equal(before, doc.Annotations[0]);
    }

    [Fact]
    public void HitTest_ReturnsTopmost()
    {
        var doc = new AnnotationDocument();
        var r1 = RectangleAnnotation.FromCorners(Guid.NewGuid(), SKColors.Red, StrokeWidth.Medium, new(0, 0), new(100, 100));
        var r2 = RectangleAnnotation.FromCorners(Guid.NewGuid(), SKColors.Blue, StrokeWidth.Medium, new(0, 0), new(100, 100));
        doc.Execute(new AddAnnotationCommand(r1));
        doc.Execute(new AddAnnotationCommand(r2));
        Assert.Equal(r2.Id, doc.HitTest(new(0, 50), 2)?.Id);
    }

    [Fact]
    public void Changed_FiresOnExecuteUndoRedo()
    {
        var doc = new AnnotationDocument();
        var count = 0;
        doc.Changed += (_, _) => count++;
        doc.Execute(new AddAnnotationCommand(Line()));
        doc.Undo();
        doc.Redo();
        Assert.Equal(3, count);
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNothing()
    {
        var doc = new AnnotationDocument();
        var count = 0;
        doc.Changed += (_, _) => count++;
        doc.Undo();
        doc.Redo();
        Assert.Empty(doc.Annotations);
        Assert.Equal(0, count);
    }
}
