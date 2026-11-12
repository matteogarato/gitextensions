using GitExtensions.Extensibility.Extensions;

namespace GitExtensions.Extensibility.Git;

public sealed class CommitData(
    ObjectId objectId,
    IReadOnlyList<ObjectId>? parentIds,
    string? author,
    DateTime authorDate,
    string? committer,
    DateTime commitDate,
    string body)
{
    public ObjectId ObjectId { get; } = objectId;
    public IReadOnlyList<ObjectId>? ParentIds { get; } = parentIds;
    public string? Author { get; } = author;
    public DateTimeOffset AuthorDate { get; } = authorDate.ToDateTimeOffset();
    public string? Committer { get; } = committer;
    public DateTimeOffset CommitDate { get; } = commitDate.ToDateTimeOffset();

    // TODO mutable properties need review

    public IReadOnlyList<ObjectId>? ChildIds { get; set; }

    /// <summary>
    ///     Gets and sets the commit message.
    /// </summary>
    public string Body { get; set; } = body;

    /// <summary>
    ///     Git <see href="https://git-scm.com/docs/git-notes">Notes</see>
    /// </summary>
    public string? Notes { get; set; }
}
