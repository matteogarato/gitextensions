using System.Diagnostics;
using GitExtensions.Extensibility.Plugins;
using GitUIPluginInterfaces.RepositoryHosts;

namespace GitExtensions.Plugins.GitHub3;

[DebuggerDisplay("{Data}")]
internal sealed class GitHubHostedRemote(string name, string owner, string remoteRepositoryName, string url)
    : IHostedRemote
{
    private GitHubRepo? _repo;

    IHostedRepository IHostedRemote.GetHostedRepository()
    {
        return _repo ??= new GitHubRepo(GitHub3Plugin.GitHub.GetRepository(Owner, RemoteRepositoryName))
        {
            CloneProtocol = CloneProtocol
        };
    }

    /// <summary>
    ///     Local name of the remote, 'origin'
    /// </summary>
    string IHostedRemote.Name { get; } = name;

    /// <summary>
    ///     Owner of the remote repository, in
    ///     git@github.com:mabako/Git.hub.git this is 'mabako'
    /// </summary>
    string IHostedRemote.Owner { get; } = owner;

    /// <summary>
    ///     Name of the remote repository, in
    ///     git@github.com:mabako/Git.hub.git this is 'Git.hub'
    /// </summary>
    string IHostedRemote.RemoteRepositoryName { get; } = remoteRepositoryName;

    string IHostedRemote.RemoteUrl { get; } = url;

    GitProtocol IHostedRemote.CloneProtocol { get; } = url.IsUrlUsingHttp() ? GitProtocol.Https : GitProtocol.Ssh;

    string IHostedRemote.Data
    {
        get => Owner + "/" + RemoteRepositoryName;
    }

    string IHostedRemote.DisplayData
    {
        get => Data;
    }

    bool IHostedRemote.IsOwnedByMe
    {
        get => GitHubLoginInfo.Username == Owner;
    }

    string IHostedRemote.GetBlameUrl(string commitHash, string fileName, int lineIndex)
    {
        return $"{GitHub3Plugin.Instance.GitHubEndpoint}/{Data}/blame/{commitHash}/{fileName}#L{lineIndex}";
    }
}
