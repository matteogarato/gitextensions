namespace GitUI.CommandsDialogs;

/// <summary>
///     The textual representations of a revision that can be copied to the clipboard.
/// </summary>
public enum RevisionCopyKind
{
    FullHash,
    ShortHash,
    Subject,
    HashAndSubject,
    Author
}

/// <summary>
///     Formats the revision data copied by the revision context menu.
/// </summary>
public static class RevisionCopyWorkflow
{
    /// <summary>
    ///     Returns one line per revision, in the given order, for the requested <paramref name="kind" />.
    /// </summary>
    public static string Format(IEnumerable<GitRevision> revisions, RevisionCopyKind kind)
    {
        return string.Join(Environment.NewLine, revisions.Select(revision => kind switch
        {
            RevisionCopyKind.FullHash => revision.ObjectId.ToString(),
            RevisionCopyKind.ShortHash => revision.ObjectId.ToShortString(),
            RevisionCopyKind.Subject => revision.Subject,
            RevisionCopyKind.HashAndSubject => $"{revision.ObjectId.ToShortString()} {revision.Subject}",
            RevisionCopyKind.Author => string.IsNullOrEmpty(revision.AuthorEmail)
                ? revision.Author ?? string.Empty
                : $"{revision.Author} <{revision.AuthorEmail}>",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        }));
    }
}
