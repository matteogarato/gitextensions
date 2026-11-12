using System.Collections;
using GitExtensions.Extensibility;

namespace GitUI.UserControls.RevisionGrid;

internal readonly struct VisibleRowRange : IEnumerable<int>, IEquatable<VisibleRowRange>
{
    public int FromIndex { get; }
    public int Count { get; }

    public VisibleRowRange(int fromIndex, int count)
    {
        DebugHelpers.Assert(fromIndex >= 0, "fromIndex >= 0");
        DebugHelpers.Assert(count >= 0, "count >= 0");

        FromIndex = fromIndex;
        Count = count;
    }

    public bool Contains(int index)
    {
        return index >= FromIndex && index < FromIndex + Count;
    }

    IEnumerator<int> IEnumerable<int>.GetEnumerator()
    {
        return Enumerable.Range(FromIndex, Count).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    bool IEquatable<VisibleRowRange>.Equals(VisibleRowRange other)
    {
        return FromIndex == other.FromIndex && Count == other.Count;
    }

    public override bool Equals(object? obj)
    {
        return obj is VisibleRowRange range && Equals(range);
    }

    public override int GetHashCode()
    {
        return unchecked((FromIndex * 397) ^ Count);
    }

    public override string ToString()
    {
        return $"[{FromIndex}, {FromIndex + Count - 1}] {Count} row{(Count == 1 ? "" : "s")}";
    }

    public static bool operator ==(VisibleRowRange left, VisibleRowRange right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(VisibleRowRange left, VisibleRowRange right)
    {
        return !left.Equals(right);
    }
}
