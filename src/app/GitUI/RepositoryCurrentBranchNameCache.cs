using System.Collections.Concurrent;
using GitCommands.Git;

namespace GitUI;

/// <summary>
///     Wraps <see cref="IRepositoryCurrentBranchNameProvider" /> with a shared in-memory cache so that
///     multiple UI surfaces (menu bar, Dashboard list) avoid redundant git reads for the same path.
/// </summary>
public interface IRepositoryCurrentBranchNameCache : IRepositoryCurrentBranchNameProvider
{
    /// <summary>
    ///     Returns <see langword="true" /> when no branch names have been cached yet.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    ///     Returns the cached branch name for <paramref name="repositoryPath" />, or <see langword="null" /> if not yet
    ///     cached.
    /// </summary>
    string? GetCachedBranchName(string repositoryPath);

    /// <summary>
    ///     Resolves the branch name and forwards it to <see cref="UpdateCache" />.
    /// </summary>
    /// <returns>The branch name for <paramref name="repositoryPath" />.</returns>
    string GetUpdatedBranchName(string repositoryPath);

    /// <summary>
    ///     Writes the resolved branch name into the cache, or removes the entry when the name is blank or error occurred.
    /// </summary>
    void UpdateCache(string repositoryPath, string branchName);

    /// <summary>
    ///     Clears all cached branch names, forcing fresh reads on the next access.
    /// </summary>
    void InvalidateAll();
}

internal sealed class RepositoryCurrentBranchNameCache(IRepositoryCurrentBranchNameProvider inner)
    : IRepositoryCurrentBranchNameCache
{
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.InvariantCulture);

    string? IRepositoryCurrentBranchNameCache.GetCachedBranchName(string repositoryPath)
    {
        return _cache.TryGetValue(repositoryPath, out var cached) ? cached : null;
    }

    /// <summary>
    ///     Gets the current branch name, reading from the cache when available.
    /// </summary>
    string IRepositoryCurrentBranchNameProvider.GetCurrentBranchName(string repositoryPath)
    {
        return _cache.TryGetValue(repositoryPath, out var cached)
            ? cached
            : GetUpdatedBranchName(repositoryPath);
    }

    string IRepositoryCurrentBranchNameCache.GetUpdatedBranchName(string repositoryPath)
    {
        var branchName = inner.GetCurrentBranchName(repositoryPath);
        UpdateCache(repositoryPath, branchName);
        return branchName;
    }

    void IRepositoryCurrentBranchNameCache.UpdateCache(string repositoryPath, string branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName) || branchName == DetachedHeadParser.UnknownBranchName)
        {
            _cache.TryRemove(repositoryPath, out _);
        }
        else
        {
            _cache[repositoryPath] = branchName;
        }
    }

    void IRepositoryCurrentBranchNameCache.InvalidateAll()
    {
        _cache.Clear();
    }

    bool IRepositoryCurrentBranchNameCache.IsEmpty
    {
        get => _cache.IsEmpty;
    }
}
