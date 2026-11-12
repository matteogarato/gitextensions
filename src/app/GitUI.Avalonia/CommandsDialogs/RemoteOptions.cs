using GitCommands.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Selects how a pull integrates the fetched changes.
/// </summary>
public enum PullMode
{
    Merge,
    Rebase,
    FetchOnly
}

/// <summary>
///     Selects how tags are handled while fetching.
/// </summary>
public enum PullTagsMode
{
    Default,
    All,
    None
}

/// <summary>
///     Describes a pull or fetch requested through the options dialog.
/// </summary>
/// <param name="Remote">The remote to fetch from.</param>
/// <param name="RemoteBranch">The remote branch to fetch, or <see langword="null" /> for the tracked/default branch.</param>
/// <param name="Mode">How fetched changes are integrated.</param>
/// <param name="Tags">Tag handling.</param>
/// <param name="Unshallow">Whether to convert a shallow repository to a complete one.</param>
/// <param name="Prune">Whether to prune remote-tracking branches; only applies to <see cref="PullMode.FetchOnly" />.</param>
/// <param name="AutoStash">Whether local changes are stashed and restored around the pull.</param>
public sealed record PullOptions(
    string Remote,
    string? RemoteBranch,
    PullMode Mode,
    PullTagsMode Tags,
    bool Unshallow,
    bool Prune,
    bool AutoStash);

/// <summary>
///     Selects what a push sends to the remote.
/// </summary>
public enum PushScope
{
    CurrentBranch,
    AllBranches,
    Tags
}

/// <summary>
///     Describes a push requested through the options dialog.
/// </summary>
/// <param name="Remote">The destination remote.</param>
/// <param name="RemoteBranch">The remote branch to update, or <see langword="null" /> to use the local branch name.</param>
/// <param name="Force">The force mode.</param>
/// <param name="TrackUpstream">Whether to record the pushed branch as upstream.</param>
/// <param name="RecurseSubmodules">0 for none, 1 to check and 2 to push on demand.</param>
/// <param name="Scope">What to push.</param>
public sealed record PushOptions(
    string Remote,
    string? RemoteBranch,
    ForcePushOptions Force,
    bool TrackUpstream,
    int RecurseSubmodules,
    PushScope Scope);
