using System.Collections.Concurrent;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

/// <summary>
///     Creates and caches <see cref="GitExecutor" /> instances by repository path.
/// </summary>
internal sealed class GitExecutorProvider(IGitDirectoryResolver gitDirectoryResolver) : IGitExecutorProvider
{
    private readonly ConcurrentDictionary<string, IGitExecutor> _cache = new(StringComparer.Ordinal);

    IGitExecutor IGitExecutorProvider.GetExecutor(string repositoryPath)
    {
        return _cache.GetOrAdd(repositoryPath, path => new GitExecutor(gitDirectoryResolver, path));
    }

    IGitExecutor IGitExecutorProvider.GetNewExecutor(string repositoryPath)
    {
        _cache.TryRemove(repositoryPath, out _);
        return GetExecutor(repositoryPath);
    }
}
