using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Selects which side of a conflicted file is kept.
/// </summary>
public enum ConflictSide
{
    Ours,
    Theirs
}

/// <summary>
///     Resolves conflicted files by taking one whole side, which also covers binary files.
/// </summary>
public static class ConflictWorkflow
{
    /// <summary>
    ///     Builds the command that writes the chosen side of a conflicted file into the worktree.
    /// </summary>
    public static ArgumentString BuildCheckoutSide(ConflictSide side, string filename)
    {
        return new GitArgumentBuilder("checkout")
        {
            { side == ConflictSide.Ours, "--ours", "--theirs" }, "--", filename.ToPosixPath().Quote()
        };
    }

    /// <summary>
    ///     Builds the command that finishes the interrupted operation once no conflicts remain.
    /// </summary>
    /// <returns>The command, or <see langword="null" /> when no operation that supports continuing is in progress.</returns>
    public static ArgumentString? BuildContinue(IGitModule module)
    {
        // A stopped rebase may also leave MERGE_HEAD, so classify it before merge.
        if (module.InTheMiddleOfRebase())
        {
            return Commands.ContinueRebase();
        }

        if (module.InTheMiddleOfMerge())
        {
            return Commands.ContinueMerge();
        }

        if (File.Exists(module.ResolveGitInternalPath("CHERRY_PICK_HEAD")))
        {
            return Commands.ContinueCherryPick();
        }

        return File.Exists(module.ResolveGitInternalPath("REVERT_HEAD"))
            ? Commands.ContinueRevert()
            : (ArgumentString?)null;
    }

    /// <summary>
    ///     Takes the chosen side of <paramref name="filename" /> and stages the result.
    /// </summary>
    /// <returns>An error message, or <see langword="null" /> when the conflict was resolved.</returns>
    public static string? ResolveWithSide(IGitModule module, string filename, ConflictSide side)
    {
        var quoted = filename.ToPosixPath().Quote();
        var checkout =
            module.GitExecutable.Execute(BuildCheckoutSide(side, filename), throwOnErrorExit: false);
        if (checkout.ExitedSuccessfully)
        {
            var add = module.GitExecutable.Execute(new GitArgumentBuilder("add") { "--", quoted },
                throwOnErrorExit: false);
            return add.ExitedSuccessfully ? null : add.AllOutput;
        }

        // The chosen side deleted the file, so there is nothing to check out and the resolution is the removal.
        var remove = module.GitExecutable.Execute(new GitArgumentBuilder("rm") { "--quiet", "--", quoted },
            throwOnErrorExit: false);
        return remove.ExitedSuccessfully ? null : $"{checkout.AllOutput}{Environment.NewLine}{remove.AllOutput}".Trim();
    }
}
