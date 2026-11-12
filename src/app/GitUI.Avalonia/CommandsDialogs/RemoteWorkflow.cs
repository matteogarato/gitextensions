using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitUI.CommandsDialogs;

/// <summary>
///     Resolves the remote and branch for pull and push of the current branch and maps them onto the shared
///     git argument builders, so the Avalonia UI uses the same git semantics as the WinForms dialogs.
/// </summary>
public static class RemoteWorkflow
{
    private const string _defaultRemoteName = "origin";

    /// <summary>
    ///     Returns the remote that the current branch tracks or, failing that, a sensible default.
    /// </summary>
    public static string? ResolveRemote(IGitModule module)
    {
        var current = module.GetCurrentRemote();
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        var remotes = module.GetRemoteNames();
        if (remotes.Contains(_defaultRemoteName, StringComparer.Ordinal))
        {
            return _defaultRemoteName;
        }

        return remotes.Count == 1 ? remotes[0] : null;
    }

    /// <summary>
    ///     Returns a user-facing error if pull or push cannot start, otherwise <see langword="null" />.
    /// </summary>
    public static string? Validate(IGitModule module, bool requiresBranch)
    {
        if (ResolveRemote(module) is null)
        {
            return TranslatedStrings.NoRemoteConfiguredError;
        }

        if (requiresBranch && string.IsNullOrEmpty(module.GetSelectedBranch(true)))
        {
            return TranslatedStrings.DetachedHeadPushError;
        }

        return null;
    }

    public static ArgumentString BuildPull(IGitModule module, string remote, bool rebase)
    {
        return module.PullCmd(remote, null, rebase);
    }

    /// <summary>
    ///     Pushes the current branch; sets the upstream when the branch does not track a remote yet.
    /// </summary>
    public static ArgumentString BuildPush(IGitModule module, string remote, ForcePushOptions force)
    {
        var branch = module.GetSelectedBranch(true);
        var track = string.IsNullOrWhiteSpace(module.GetCurrentRemote());
        return Commands.Push(remote, branch, null, force, track, 0);
    }

    /// <summary>
    ///     Builds a pull or fetch from the options chosen in the pull dialog.
    /// </summary>
    public static ArgumentString BuildPull(IGitModule module, PullOptions options)
    {
        var remoteBranch = string.IsNullOrWhiteSpace(options.RemoteBranch) ? null : options.RemoteBranch.Trim();
        bool? fetchTags = options.Tags switch
        {
            PullTagsMode.All => true,
            PullTagsMode.None => false,
            _ => null
        };

        if (options.Mode == PullMode.FetchOnly)
        {
            return module.FetchCmd(options.Remote, remoteBranch, null, fetchTags, options.Unshallow, options.Prune);
        }

        var pull = module.PullCmd(options.Remote, remoteBranch, options.Mode == PullMode.Rebase, fetchTags,
            options.Unshallow);
        if (!options.AutoStash)
        {
            return pull;
        }

        // The shared builder cannot add this flag, and git only accepts it after the verb; the leading -c options never contain that token.
        const string pullVerb = "pull ";
        var arguments = pull.ToString();
        var index = arguments.IndexOf(pullVerb, StringComparison.Ordinal);
        return index < 0 ? pull : arguments.Insert(index + pullVerb.Length, "--autostash ");
    }

    /// <summary>
    ///     Builds a push from the options chosen in the push dialog.
    /// </summary>
    public static ArgumentString BuildPush(IGitModule module, PushOptions options)
    {
        switch (options.Scope)
        {
            case PushScope.AllBranches:
                return Commands.PushAll(options.Remote, options.Force, options.TrackUpstream,
                    options.RecurseSubmodules);
            case PushScope.Tags:
                return Commands.PushTag(options.Remote, "", true, options.Force);
            default:
                var branch = module.GetSelectedBranch(true);
                var remoteBranch = string.IsNullOrWhiteSpace(options.RemoteBranch)
                    ? null
                    : options.RemoteBranch.Trim();
                return Commands.Push(options.Remote, branch, remoteBranch, options.Force, options.TrackUpstream,
                    options.RecurseSubmodules);
        }
    }
}
