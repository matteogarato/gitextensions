namespace GitUITests.Avatars;

public abstract class AvatarCacheTestBase : AvatarTestBase
{
    [Fact]
    public async Task GetAvatarAsync_returns_correct_image()
    {
        (await Cache.GetAvatarAsync(Email1, Name1, Size)).Should().BeSameAs(Img1);
        (await Cache.GetAvatarAsync(Email2, Name2, Size)).Should().BeSameAs(Img2);
        (await Cache.GetAvatarAsync(Email3, Name3, Size)).Should().BeSameAs(Img3);
    }
}
