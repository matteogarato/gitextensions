using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     The supported options for rebasing the current branch.
/// </summary>
/// <param name="Onto">The branch or revision to rebase onto.</param>
/// <param name="AutoStash">Whether Git should stash and restore local changes.</param>
/// <param name="Interactive">Whether Git should open the configured sequence editor.</param>
/// <param name="AutoSquash">Whether interactive rebase should autosquash fixup commits.</param>
/// <param name="Upstream">The exclusive lower bound for a range rebase, or <see langword="null" />.</param>
public sealed record RebaseConfiguration(
    string Onto,
    bool AutoStash,
    bool Interactive,
    bool AutoSquash,
    string? Upstream);

/// <summary>
///     Validates and builds standard, interactive and range rebases using shared Git commands.
/// </summary>
public static class RebaseWorkflow
{
    /// <summary>
    ///     Returns an error when the repository cannot start a rebase onto the selected branch.
    /// </summary>
    public static string? Validate(IGitModule module, string? onto)
    {
        var currentBranch = module.GetSelectedBranch(true);
        if (string.IsNullOrWhiteSpace(currentBranch))
        {
            return TranslatedStrings.RebaseDetachedHeadError;
        }

        if (string.IsNullOrWhiteSpace(onto))
        {
            return TranslatedStrings.RebaseTargetRequired;
        }

        if (string.Equals(currentBranch, onto, StringComparison.Ordinal))
        {
            return TranslatedStrings.RebaseSameBranchError;
        }

        if (module.InTheMiddleOfRebase() || module.InTheMiddleOfMerge() || module.InTheMiddleOfConflictedMerge())
        {
            return TranslatedStrings.RebaseOperationInProgressError;
        }

        return null;
    }

    /// <summary>
    ///     Builds a rebase using the selected branch, range and interactive options.
    /// </summary>
    public static ArgumentString BuildRebase(RebaseConfiguration configuration)
    {
        return Commands.Rebase(new Commands.RebaseOptions
        {
            OnTo = configuration.Upstream is null ? null : configuration.Onto.Trim(),
            From = configuration.Upstream?.Trim(),
            Interactive = configuration.Interactive,
            AutoSquash = configuration.AutoSquash,
            AutoStash = configuration.AutoStash,
            BranchName = configuration.Upstream is null ? configuration.Onto.Trim() : "HEAD"
        });
    }

    /// <summary>
    ///     Builds a standard non-interactive rebase onto the selected branch.
    /// </summary>
    public static ArgumentString BuildRebase(string onto, bool autoStash)
    {
        return BuildRebase(new RebaseConfiguration(onto, autoStash, false, false, null));
    }

    /// <summary>
    ///     Gets the shared command used to continue after conflicts are resolved.
    /// </summary>
    public static ArgumentString BuildContinue()
    {
        return Commands.ContinueRebase();
    }

    /// <summary>
    ///     Gets the shared command used to skip the current commit.
    /// </summary>
    public static ArgumentString BuildSkip()
    {
        return Commands.SkipRebase();
    }

    /// <summary>
    ///     Gets the shared command used to abort and restore the pre-rebase state.
    /// </summary>
    public static ArgumentString BuildAbort()
    {
        return Commands.AbortRebase();
    }

    /// <summary>
    ///     Gets the shared command used to edit the active rebase todo list.
    /// </summary>
    public static ArgumentString BuildEditTodo()
    {
        return Commands.EditTodoRebase();
    }
}
