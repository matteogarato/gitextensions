using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///  The supported options for a branch merge.
/// </summary>
/// <param name="AllowFastForward">Whether Git may fast-forward the current branch.</param>
/// <param name="Squash">Whether to squash the source commits into the index without creating a merge commit.</param>
/// <param name="NoCommit">Whether to stop before creating a merge commit.</param>
/// <param name="AllowUnrelatedHistories">Whether to allow merging histories without a common ancestor.</param>
/// <param name="Strategy">The merge strategy name, or an empty string for Git's default.</param>
/// <param name="Message">The merge commit message, or <see langword="null"/> for the generated one.</param>
/// <param name="IncludeLog">Whether the merge commit message lists the merged commits (<c>--log</c>).</param>
public sealed record MergeOptions(bool AllowFastForward, bool Squash, bool NoCommit, bool AllowUnrelatedHistories, string Strategy = "", string? Message = null, bool IncludeLog = false)
{
    /// <summary>
    ///  Gets the default options, which allow fast-forward merges and automatically commit them.
    /// </summary>
    public static MergeOptions Default { get; } = new(AllowFastForward: true, Squash: false, NoCommit: false, AllowUnrelatedHistories: false);
}

/// <summary>
///  Validates and builds merge operations using the shared Git command builder.
/// </summary>
public static class MergeWorkflow
{
    /// <summary>
    ///  Non-current local branches, remote branches and tags can be merged from the repository tree.
    /// </summary>
    public static bool CanMerge(IGitRef? source, string currentBranch)
        => (source is { IsTag: false, IsHead: true, IsSelected: false }
                && !string.IsNullOrWhiteSpace(currentBranch)
                && !string.Equals(source.Name, currentBranch, StringComparison.Ordinal))
            || (source is { IsTag: false, IsRemote: true }
                && !string.IsNullOrWhiteSpace(currentBranch))
            || (source is { IsTag: true } && !string.IsNullOrWhiteSpace(currentBranch));

    /// <summary>
    ///  The number of merged commit summaries requested when the log option is enabled.
    /// </summary>
    public const int MergeLogEntryCount = 20;

    /// <summary>
    ///  The strategies offered by the merge dialog; the first entry stands for Git's default.
    /// </summary>
    public static IReadOnlyList<string> Strategies { get; } = ["", "resolve", "recursive", "ort", "octopus", "ours", "subtree"];

    /// <summary>
    ///  Builds the merge command for a selected local or remote branch.
    /// </summary>
    /// <param name="module">The repository to merge in.</param>
    /// <param name="source">The branch to merge.</param>
    /// <param name="options">The merge options.</param>
    /// <param name="messageFilePath">A file holding the merge commit message, or <see langword="null"/>.</param>
    public static ArgumentString BuildMerge(IGitModule module, IGitRef source, MergeOptions options, string? messageFilePath = null)
        => Commands.MergeBranch(
            source.Name,
            options.AllowFastForward && !options.Squash,
            options.Squash,
            options.NoCommit,
            options.Strategy,
            options.AllowUnrelatedHistories,
            messageFilePath,
            module.GetPathForGitExecution,
            log: options.IncludeLog ? MergeLogEntryCount : null);

    /// <summary>
    ///  Writes the merge message to a temporary file because Git reads custom messages only from a file.
    /// </summary>
    /// <returns>The file path, or <see langword="null"/> when no custom message was given.</returns>
    public static string? WriteMessageFile(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string path = Path.GetTempFileName();
        File.WriteAllText(path, message);
        return path;
    }
}
