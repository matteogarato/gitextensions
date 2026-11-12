namespace GitExtensions.Extensibility.Plugins;

public interface IHostedRemote
{
    /// <summary>
    ///     Gets the name of the remote in the local git repository. May be null.
    /// </summary>
    string? Name { get; }

    string Data { get; }

    string DisplayData { get; }

    bool IsOwnedByMe { get; }

    string Owner { get; }

    string RemoteRepositoryName { get; }

    string RemoteUrl { get; }
    GitProtocol CloneProtocol { get; }
    IHostedRepository GetHostedRepository();

    string GetBlameUrl(string commitHash, string fileName, int lineIndex);
}
