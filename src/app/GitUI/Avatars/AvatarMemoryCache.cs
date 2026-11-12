using GitExtUtils;

namespace GitUI.Avatars;

/// <summary>
///     Caches most-recently-used images.
/// </summary>
/// <remarks>
///     <para>Decorates an inner cache, delegating to it as needed.</para>
///     <para>If an image is available in memory, the inner cache can be bypassed.</para>
/// </remarks>
public sealed class AvatarMemoryCache(IAvatarProvider inner, int capacity = 30) : IAvatarProvider, IAvatarCacheCleaner
{
    private readonly MruCache<(string email, int imageSize), Image> _cache = new(capacity);
    private readonly Lock _cacheLock = new();
    private readonly IAvatarProvider _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly HashSet<(string email, int imageSize)> _requested = new(6);
    private readonly Lock _requestedLock = new();

    /// <inheritdoc />
    event EventHandler? IAvatarCacheCleaner.CacheCleared
    {
        add => CacheCleared += value;
        remove => CacheCleared -= value;
    }

    /// <inheritdoc />
    Task IAvatarCacheCleaner.ClearCacheAsync()
    {
        lock (_cacheLock)
        {
            // The cached images must not be disposed here: GetAvatarAsync hands out the cached
            // instance itself, so a consumer may still be painting it - the picture box of an
            // AvatarControl, the avatar column of the revision grid or the blame author margin.
            // They do not keep their file open either, so nothing here has to be released eagerly.
            _cache.Clear();
        }

        lock (_requestedLock)
        {
            _requested.Clear();
        }

        CacheCleared?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    bool IAvatarProvider.PerformsIo
    {
        get => false;
    }

    /// <inheritdoc />
    async Task<Image?> IAvatarProvider.GetAvatarAsync(string email, string? name, int imageSize)
    {
        var key = (email, imageSize);
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cachedImage))
            {
                return cachedImage;
            }
        }

        // The revision grid could trigger same request multiple times at the same time
        // => Wait that the first finish and result is added to the cache.
        if (IsRequestInProgress(key))
        {
            for (var i = 0; i < 10_000; i++)
            {
                lock (_cacheLock)
                {
                    if (_cache.TryGetValue(key, out var cachedImage))
                    {
                        return cachedImage;
                    }
                }

                if (!IsRequestInProgress(key))
                {
                    // Early exit when the image is not in the cache and key is no more in the requests list
                    // => the request has failed!
                    break;
                }

                await Task.Delay(5); // Approximative time to read from disk to not slow down the grid scrolling
            }
        }

        try
        {
            lock (_requestedLock)
            {
                _requested.Add(key);
            }

            var image = await _inner.GetAvatarAsync(email, name, imageSize);

            if (image is not null)
            {
                lock (_cacheLock)
                {
                    _cache.Add(key, image);
                }
            }

            return image;
        }
        finally
        {
            lock (_requestedLock)
            {
                _requested.Remove(key);
            }
        }

        bool IsRequestInProgress((string email, int imageSize) avatarKey)
        {
            lock (_requestedLock)
            {
                return _requested.Contains(avatarKey);
            }
        }
    }

    /// <inheritdoc />
    private event EventHandler? CacheCleared;
}
