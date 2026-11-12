using GitExtensions.Extensibility.Git;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls;

public sealed class FileStatusItem(
    GitRevision? firstRev,
    GitRevision secondRev,
    GitItemStatus item,
    ObjectId baseA = default,
    ObjectId baseB = default)
{
    /// <summary>
    ///     First (Parent or A in diff)
    ///     Can be null for the initial commit.
    /// </summary>
    public GitRevision? FirstRevision { get; } = firstRev;

    /// <summary>
    ///     Selected (current or B in diff)
    ///     The revision selected, the primary for which info exists.
    /// </summary>
    public GitRevision SecondRevision { get; } = secondRev ?? throw new ArgumentNullException(nameof(secondRev));

    /// <summary>
    ///     If ranges are selected, the first commit (base) for <see cref="FirstRevision" /> (head).
    /// </summary>
    public ObjectId BaseA { get; } = baseA;

    /// <summary>
    ///     If ranges are selected, the first commit (base) for <see cref="SecondRevision" /> (head).
    /// </summary>
    public ObjectId BaseB { get; } = baseB;

    /// <summary>
    ///     The status item in the list.
    /// </summary>
    public GitItemStatus Item { get; } = item ?? throw new ArgumentNullException(nameof(item));

    public override string ToString()
    {
        return Item.ToString();
    }
}
