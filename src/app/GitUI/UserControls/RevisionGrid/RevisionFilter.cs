namespace GitUI.UserControls.RevisionGrid;

/// <summary>
///     Represents a simple data structure that defines a revision filter.
/// </summary>
public class RevisionFilter(string text, bool byCommit, bool byCommitter, bool byAuthor, bool byDiffContent)
{
    // TODO: consider using a bit vector or an enum

    public string Text { get; } = text ?? string.Empty;
    public bool FilterByCommit { get; } = byCommit;
    public bool FilterByCommitter { get; } = byCommitter;
    public bool FilterByAuthor { get; } = byAuthor;
    public bool FilterByDiffContent { get; } = byDiffContent;
}
