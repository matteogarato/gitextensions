using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Resolves the remote and branch for pull and push of the current branch and maps them onto the shared
///  git argument builders, so the Avalonia UI uses the same git semantics as the WinForms dialogs.
/// </summary>
public static class RemoteWorkflow
{
    private const string DefaultRemoteName = "origin";

    /// <summary>
    ///  Returns the remote that the current branch tracks or, failing that, a sensible default.
    /// </summary>
    public static string? ResolveRemote(IGitModule module)
    {
        string current = module.GetCurrentRemote();
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        IReadOnlyList<string> remotes = module.GetRemoteNames();
        if (remotes.Contains(DefaultRemoteName, StringComparer.Ordinal))
        {
            return DefaultRemoteName;
        }

        return remotes.Count == 1 ? remotes[0] : null;
    }

    /// <summary>
    ///  Returns a user-facing error if pull or push cannot start, otherwise <see langword="null"/>.
    /// </summary>
    public static string? Validate(IGitModule module, bool requiresBranch)
    {
        if (ResolveRemote(module) is null)
        {
            return TranslatedStrings.NoRemoteConfiguredError;
        }

        if (requiresBranch && string.IsNullOrEmpty(module.GetSelectedBranch(emptyIfDetached: true)))
        {
            return TranslatedStrings.DetachedHeadPushError;
        }

        return null;
    }

    public static ArgumentString BuildPull(IGitModule module, string remote, bool rebase)
        => module.PullCmd(remote, remoteBranch: null, rebase);

    /// <summary>
    ///  Pushes the current branch; sets the upstream when the branch does not track a remote yet.
    /// </summary>
    public static ArgumentString BuildPush(IGitModule module, string remote, ForcePushOptions force)
    {
        string branch = module.GetSelectedBranch(emptyIfDetached: true);
        bool track = string.IsNullOrWhiteSpace(module.GetCurrentRemote());
        return Commands.Push(remote, branch, toBranch: null, force, track, recursiveSubmodules: 0);
    }

    /// <summary>
    ///  Builds a pull or fetch from the options chosen in the pull dialog.
    /// </summary>
    public static ArgumentString BuildPull(IGitModule module, PullOptions options)
    {
        string? remoteBranch = string.IsNullOrWhiteSpace(options.RemoteBranch) ? null : options.RemoteBranch.Trim();
        bool? fetchTags = options.Tags switch
        {
            PullTagsMode.All => true,
            PullTagsMode.None => false,
            _ => null,
        };

        if (options.Mode == PullMode.FetchOnly)
        {
            return module.FetchCmd(options.Remote, remoteBranch, localBranch: null, fetchTags, options.Unshallow, pruneRemoteBranches: options.Prune);
        }

        ArgumentString pull = module.PullCmd(options.Remote, remoteBranch, options.Mode == PullMode.Rebase, fetchTags, options.Unshallow);
        if (!options.AutoStash)
        {
            return pull;
        }

        // The shared builder cannot add this flag, and git only accepts it after the verb; the leading -c options never contain that token.
        const string PullVerb = "pull ";
        string arguments = pull.ToString();
        int index = arguments.IndexOf(PullVerb, StringComparison.Ordinal);
        return index < 0 ? pull : arguments.Insert(index + PullVerb.Length, "--autostash ");
    }

    /// <summary>
    ///  Builds a push from the options chosen in the push dialog.
    /// </summary>
    public static ArgumentString BuildPush(IGitModule module, PushOptions options)
    {
        switch (options.Scope)
        {
            case PushScope.AllBranches:
                return Commands.PushAll(options.Remote, options.Force, options.TrackUpstream, options.RecurseSubmodules);
            case PushScope.Tags:
                return Commands.PushTag(options.Remote, tag: "", all: true, options.Force);
            default:
                string branch = module.GetSelectedBranch(emptyIfDetached: true);
                string? remoteBranch = string.IsNullOrWhiteSpace(options.RemoteBranch) ? null : options.RemoteBranch.Trim();
                return Commands.Push(options.Remote, branch, remoteBranch, options.Force, options.TrackUpstream, options.RecurseSubmodules);
        }
    }
}
