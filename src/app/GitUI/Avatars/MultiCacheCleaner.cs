namespace GitUI.Avatars;

/// <summary>
///     Wraps multiple caches and clears all of them with a single call.
/// </summary>
public sealed class MultiCacheCleaner : IAvatarCacheCleaner
{
    private readonly IAvatarCacheCleaner[] _inner;

    public MultiCacheCleaner(params IAvatarCacheCleaner[] inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));

        if (_inner.Any(p => p == null))
        {
            throw new ArgumentNullException(nameof(inner));
        }
    }

    /// <inheritdoc />
    event EventHandler? IAvatarCacheCleaner.CacheCleared
    {
        add => CacheCleared += value;
        remove => CacheCleared -= value;
    }

    /// <inheritdoc />
    async Task IAvatarCacheCleaner.ClearCacheAsync()
    {
        foreach (var cacheCleaner in _inner)
        {
            await cacheCleaner.ClearCacheAsync();
        }

        CacheCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    private event EventHandler? CacheCleared;
}
