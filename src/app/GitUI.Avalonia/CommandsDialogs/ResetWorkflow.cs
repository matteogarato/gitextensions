using System.Text;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Discards working tree changes through the shared <see cref="IGitModule" /> reset operations,
///     as the WinForms reset-changes flow does.
/// </summary>
public static class ResetWorkflow
{
    /// <summary>
    ///     Only real commits can be reset to, not the artificial working tree and index revisions.
    /// </summary>
    public static bool CanResetTo(GitRevision? revision)
    {
        return revision is not null && !revision.IsArtificial;
    }

    /// <summary>
    ///     Builds a soft reset to the parent of HEAD, preserving the undone commit's changes in the index.
    /// </summary>
    /// <returns>A command that moves HEAD back one commit and keeps the changes staged.</returns>
    public static ArgumentString BuildUndoLastCommit()
    {
        return Commands.Reset(ResetMode.Soft, "HEAD~1");
    }

    /// <summary>
    ///     Builds a reset command that moves the current branch to the selected revision.
    /// </summary>
    /// <param name="revision">The commit to reset to.</param>
    /// <param name="mode">The reset mode.</param>
    /// <returns>A command that changes repository state.</returns>
    public static ArgumentString BuildResetTo(GitRevision revision, ResetMode mode)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.IsArtificial)
        {
            throw new ArgumentException("A real commit revision is required.", nameof(revision));
        }

        return Commands.Reset(mode, revision.Guid, quiet: false);
    }

    /// <summary>
    ///     Returns the confirmation text for discarding the given files, or <see langword="null" /> if there is nothing to
    ///     discard.
    /// </summary>
    public static string? BuildDiscardConfirmation(IReadOnlyList<GitItemStatus> items)
    {
        return items.Count == 0
            ? null
            : string.Format(
                items.Any(item => item.IsNew)
                    ? TranslatedStrings.ConfirmDiscardChangesAndDelete
                    : TranslatedStrings.ConfirmDiscardChanges,
                items.Count);
    }

    /// <summary>
    ///     Discards the selected unstaged changes; new files are deleted.
    /// </summary>
    public static bool Discard(IGitModule module, IReadOnlyList<GitItemStatus> items,
        IFullPathResolver fullPathResolver, out string output)
    {
        output = string.Empty;
        if (items.Count == 0)
        {
            return false;
        }

        var resetAndDelete = items.Any(item => item.IsNew);
        var succeeded =
            module.ResetChanges(default, items, resetAndDelete, fullPathResolver, out var log, null);
        output = log.ToString();
        return succeeded && output.Length == 0;
    }

    /// <summary>
    ///     Resets tracked files to HEAD, leaving untracked files in place.
    /// </summary>
    public static bool ResetAll(IGitModule module)
    {
        return module.ResetAllChanges(false);
    }
}
