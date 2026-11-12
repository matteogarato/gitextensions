using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Builds cherry-pick and revert commands for a revision using the shared <see cref="Commands" />.
/// </summary>
public static class CommitPickWorkflow
{
    private const int _mainlineParent = 1;

    /// <summary>
    ///     Only real commits can be picked or reverted, not the artificial working tree and index revisions.
    /// </summary>
    public static bool CanApply(GitRevision? revision)
    {
        return revision is not null && !revision.IsArtificial;
    }

    /// <summary>
    ///     Builds a cherry-pick command, using the selected mainline for merge commits.
    /// </summary>
    /// <param name="module">The repository module.</param>
    /// <param name="revision">The commit to cherry-pick.</param>
    /// <param name="mainlineParent">The one-based parent number for a merge commit.</param>
    public static ArgumentString BuildCherryPick(IGitModule module, GitRevision revision,
        int mainlineParent = _mainlineParent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mainlineParent, 1);

        return Commands.CherryPick(revision.ObjectId, true,
            module.IsMerge(revision.ObjectId) ? $"-m {mainlineParent}" : string.Empty);
    }

    /// <summary>
    ///     Builds a revert command, using the selected mainline for merge commits.
    /// </summary>
    /// <param name="module">The repository module.</param>
    /// <param name="revision">The commit to revert.</param>
    /// <param name="mainlineParent">The one-based parent number for a merge commit.</param>
    public static ArgumentString BuildRevert(IGitModule module, GitRevision revision,
        int mainlineParent = _mainlineParent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mainlineParent, 1);

        return Commands.Revert(revision.ObjectId, true, module.IsMerge(revision.ObjectId) ? mainlineParent : 0);
    }

    /// <summary>
    ///     Builds a cherry-pick command that records the source commit in the message (<c>-x</c>).
    /// </summary>
    /// <param name="module">The repository module.</param>
    /// <param name="revision">The commit to cherry-pick.</param>
    /// <param name="mainlineParent">The one-based parent number for a merge commit.</param>
    public static ArgumentString BuildCherryPickRecordingOrigin(IGitModule module, GitRevision revision,
        int mainlineParent = _mainlineParent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mainlineParent, 1);

        return new GitArgumentBuilder("cherry-pick")
        {
            "-x", { module.IsMerge(revision.ObjectId), $"-m {mainlineParent}" }, revision.ObjectId.ToString()
        };
    }

    /// <summary>
    ///     Builds a revert command that stops before committing so the message can be edited in the commit dialog.
    /// </summary>
    /// <param name="module">The repository module.</param>
    /// <param name="revision">The commit to revert.</param>
    /// <param name="mainlineParent">The one-based parent number for a merge commit.</param>
    public static ArgumentString BuildRevertWithoutCommit(IGitModule module, GitRevision revision,
        int mainlineParent = _mainlineParent)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(mainlineParent, 1);

        return Commands.Revert(revision.ObjectId, false, module.IsMerge(revision.ObjectId) ? mainlineParent : 0);
    }

    /// <summary>
    ///     Only non-merge, real commits can be applied in one sequence because a sequence cannot ask for a mainline per
    ///     commit.
    /// </summary>
    public static bool CanApplySequence(IGitModule module, IReadOnlyList<GitRevision> revisions)
    {
        return revisions.Count > 1 &&
               revisions.All(revision => CanApply(revision) && !module.IsMerge(revision.ObjectId));
    }

    /// <summary>
    ///     Builds one cherry-pick for several commits, applied oldest first so Git's sequencer can continue after a conflict.
    /// </summary>
    /// <param name="revisions">The selected commits in revision-grid order (newest first).</param>
    /// <param name="recordOrigin">Whether to add the <c>-x</c> source reference to each message.</param>
    public static ArgumentString BuildCherryPickSequence(IReadOnlyList<GitRevision> revisions, bool recordOrigin)
    {
        return BuildSequence("cherry-pick", [.. revisions.Reverse()], recordOrigin ? "-x" : null);
    }

    /// <summary>
    ///     Builds one revert for several commits, reverting the newest first.
    /// </summary>
    /// <param name="revisions">The selected commits in revision-grid order (newest first).</param>
    /// <param name="autoCommit">Whether each revert is committed automatically.</param>
    public static ArgumentString BuildRevertSequence(IReadOnlyList<GitRevision> revisions, bool autoCommit)
    {
        return BuildSequence("revert", revisions, autoCommit ? "--no-edit" : "--no-commit");
    }

    private static ArgumentString BuildSequence(string verb, IReadOnlyList<GitRevision> revisions, string? option)
    {
        GitArgumentBuilder builder = new(verb) { option };
        foreach (var revision in revisions)
        {
            builder.Add(revision.ObjectId.ToString());
        }

        return builder;
    }
}
