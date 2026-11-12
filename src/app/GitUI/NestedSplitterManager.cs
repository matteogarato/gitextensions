namespace GitUI;

public sealed class NestedSplitterManager(SplitterManager parent, string name)
{
    private readonly SplitterManager _parent = parent;
    private readonly string _prefix = $"{name}.";

    public NestedSplitterManager(NestedSplitterManager parent, string name)
        : this(parent._parent, parent._prefix + name)
    {
    }

    public void AddSplitter(SplitContainer splitter, int? defaultDistance = null)
    {
        _parent.AddSplitter(splitter, $"{_prefix}{splitter.Name}", defaultDistance);
    }
}
