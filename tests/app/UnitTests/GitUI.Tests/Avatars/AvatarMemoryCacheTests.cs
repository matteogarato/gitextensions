using GitUI.Avatars;
using NSubstitute;

namespace GitUITests.Avatars;

public sealed class AvatarMemoryCacheTests : AvatarCacheTestBase
{
    public AvatarMemoryCacheTests()
    {
        Cache = new AvatarMemoryCache(Inner, 3);
    }

    [Fact]
    public async Task GetAvatarAsync_calls_inner_the_first_time()
    {
        await MissAsync(Email1, Name1, Img1);
        await HitAsync(Email1, Name1, Img1);
    }

    [Fact]
    public async Task GetAvatarAsync_calls_inner_when_not_present_in_cache()
    {
        await MissAsync(Email1, Name1, Img1);
        await MissAsync(Email2, Name2, Img2);
        await MissAsync(Email3, Name3, Img3);
        await MissAsync(Email4, Name4, Img4);
        await MissAsync(Email1, Name1, Img1);
        await MissAsync(Email2, Name2, Img2);
        await MissAsync(Email3, Name3, Img3);
        await MissAsync(Email4, Name4, Img4);
        await HitAsync(Email2, Name2, Img2);
        await HitAsync(Email3, Name3, Img3);
        await HitAsync(Email4, Name4, Img4);
    }

    [Fact]
    public async Task GetAvatarAsync_cleans_oldest_images()
    {
        // Populate the cache with three images, so that it is full
        await MissAsync(Email1, Name1, Img1);
        await MissAsync(Email2, Name2, Img2);
        await MissAsync(Email3, Name3, Img3);

        // Try getting the first image again
        await HitAsync(Email1, Name1, Img1);

        // At this point image 2 is the oldest

        // Try getting image 4
        await MissAsync(Email4, Name4, Img4);

        // That should have pushed images 2 and 3 out of the cache
        await MissAsync(Email2, Name2, Img2);
        await MissAsync(Email3, Name3, Img3);
    }

    [Fact]
    public async Task ClearCacheAsync_must_not_dispose_an_image_it_handed_out()
    {
        // Own the image instead of reusing one of the shared ones, which the cache would dispose
        // before the sibling tests run
        const string email = "cleared@avatar.com";
        const string name = "Cleared Cache";
        using Bitmap image = new(Size, Size);
        Inner.GetAvatarAsync(email, name, Size).Returns(Task.FromResult<Image?>(image));

        var handedOut = await Cache.GetAvatarAsync(email, name, Size);

        await CacheCleaner.ClearCacheAsync();

        handedOut.Should().BeSameAs(image);
        ((Action)(() => _ = handedOut!.Size)).Should()
            .NotThrow<ArgumentException>("a consumer may still be painting the image the cache handed out");
    }
}
