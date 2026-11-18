namespace ScreenCapture.Core.Annotations;

public interface IDocumentCommand
{
    void Apply(List<Annotation> items);

    void Revert(List<Annotation> items);
}

public sealed class AddAnnotationCommand(Annotation annotation) : IDocumentCommand
{
    public void Apply(List<Annotation> items) => items.Add(annotation);

    public void Revert(List<Annotation> items) => items.Remove(annotation);
}

public sealed class RemoveAnnotationCommand(Guid id) : IDocumentCommand
{
    private Annotation? _removed;
    private int _index = -1;

    public void Apply(List<Annotation> items)
    {
        _index = items.FindIndex(a => a.Id == id);
        if (_index < 0)
        {
            return;
        }

        _removed = items[_index];
        items.RemoveAt(_index);
    }

    public void Revert(List<Annotation> items)
    {
        if (_removed is null)
        {
            return;
        }

        items.Insert(_index, _removed);
        _removed = null;
    }
}

public sealed class ReplaceAnnotationCommand(Annotation before, Annotation after) : IDocumentCommand
{
    public void Apply(List<Annotation> items) => Swap(items, before, after);

    public void Revert(List<Annotation> items) => Swap(items, after, before);

    private static void Swap(List<Annotation> items, Annotation from, Annotation to)
    {
        var i = items.FindIndex(a => a.Id == from.Id);
        if (i >= 0)
        {
            items[i] = to;
        }
    }
}
