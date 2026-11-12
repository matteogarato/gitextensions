using GitCommands.Settings;
using GitUI.Properties;

namespace GitUI.Avatars;

public static class AvatarService
{
    private static readonly InitialsAvatarProvider _initialsAvatarProvider;
    private static readonly StaticImageAvatarProvider _userImageAvatarProvider;

    private static readonly HotSwapAvatarProvider _hotSwapProvider;

    static AvatarService()
    {
        _initialsAvatarProvider = new InitialsAvatarProvider();
        _userImageAvatarProvider = new StaticImageAvatarProvider(Images.User80);

        _hotSwapProvider = new HotSwapAvatarProvider();
        (DefaultProvider, CacheCleaner) = SetupCachingAndFallback();

        UpdateAvatarProvider();
    }

    public static IAvatarProvider DefaultProvider { get; }
    public static IAvatarCacheCleaner CacheCleaner { get; }

    /// <summary>
    ///     Updates the internal avatar provider chain to
    ///     reflect the current active (according to <see cref="AppSettings" /> provider.
    /// </summary>
    public static void UpdateAvatarProvider()
    {
        _hotSwapProvider.Provider = CreateAvatarProvider(AppSettings.AvatarProvider, AppSettings.AvatarFallbackType);
    }

    /// <summary>
    ///     Creates an <see cref="IAvatarProvider" /> based on the given
    ///     <see cref="AvatarProvider" /> and <see cref="AvatarFallbackType" /> enum options.
    /// </summary>
    /// <returns>Returns an avatar provider that can be used to resolve user avatars.</returns>
    public static IAvatarProvider? CreateAvatarProvider(
        AvatarProvider provider,
        AvatarFallbackType? fallbackType,
        IAvatarDownloader? downloader = null)
    {
        // initialize download only if needed (some options, like local providers, don't need a downloader)
        // and use the downloader provided as parameter if possible.
        Lazy<IAvatarDownloader> lazyDownloader = new(() => downloader ?? new AvatarDownloader());

        // build collection of (non-null) providers
        IAvatarProvider[] providers =
        [
            .. new[] { BuildMainProvider(), BuildFallbackProvider() }
                .WhereNotNull()
        ];

        // only create chained avatar overhead if really needed
        return providers.Length switch
        {
            0 => null,
            1 => providers[0],
            _ => new ChainedAvatarProvider(providers)
        };

        // Local methods to build requested main and fallback providers:

        IAvatarProvider? BuildMainProvider()
        {
            return provider switch
            {
                AvatarProvider.Default => BuildDefaultMainProvider(),
                AvatarProvider.Custom => CustomAvatarProvider.ParseTemplateString(AppSettings.CustomAvatarTemplate,
                    lazyDownloader.Value),
                _ => null
            };
        }

        IAvatarProvider BuildDefaultMainProvider()
        {
            // 1. query GitHub (with non-reply and regular email addresses)
            //    GitHub might internally fall back to Gravatar, so only need a single request in most cases.

            // 2. resolve via Gravatar (for users that don't have a GitHub account)
            //    this request also directly provides the fallback if it's Gravatar compatible.

            return new ChainedAvatarProvider(
                new GithubAvatarProvider(lazyDownloader.Value),
                new GravatarProvider(lazyDownloader.Value, fallbackType));
        }

        IAvatarProvider BuildFallbackProvider()
        {
            return fallbackType switch
            {
                // Local fallback provider
                AvatarFallbackType.AuthorInitials => _initialsAvatarProvider,

                null => _initialsAvatarProvider,

                // Use Gravatar as fallback provider
                { } type when GravatarProvider.IsFallbackSupportedByGravatar(fallbackType.Value) =>
                    new GravatarProvider(lazyDownloader.Value, type, true),

                // Use author initials for unknown types
                _ => _initialsAvatarProvider
            };
        }
    }

    private static (IAvatarProvider provider, IAvatarCacheCleaner cacheCleaner) SetupCachingAndFallback()
    {
        FileSystemAvatarCache persistentCacheProvider = new(_hotSwapProvider);
        AvatarMemoryCache memoryCachedProvider = new(persistentCacheProvider, AppSettings.AvatarCacheSize);

        MultiCacheCleaner cacheCleaner = new(memoryCachedProvider, persistentCacheProvider);

        SafetynetAvatarProvider mainProvider
            = new(
                new ChainedAvatarProvider(
                    memoryCachedProvider,
                    _userImageAvatarProvider));

        return (mainProvider, cacheCleaner);
    }

    public static void UpdateAvatarInitialFontsSettings()
    {
        _initialsAvatarProvider.UpdateFontsSettings();
    }
}
