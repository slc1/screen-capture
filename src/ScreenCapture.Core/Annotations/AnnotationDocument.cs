using SkiaSharp;

namespace ScreenCapture.Core.Annotations;

public sealed class AnnotationDocument
{
    private readonly List<Annotation> _items = [];
    private readonly Stack<IDocumentCommand> _undo = new();
    private readonly Stack<IDocumentCommand> _redo = new();

    public IReadOnlyList<Annotation> Annotations => _items;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? Changed;

    public void Execute(IDocumentCommand command)
    {
        command.Apply(_items);
        _undo.Push(command);
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!_undo.TryPop(out var command))
        {
            return;
        }

        command.Revert(_items);
        _redo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!_redo.TryPop(out var command))
        {
            return;
        }

        command.Apply(_items);
        _undo.Push(command);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Annotation? HitTest(SKPoint p, float tolerance)
    {
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].HitTest(p, tolerance))
            {
                return _items[i];
            }
        }

        return null;
    }
}
