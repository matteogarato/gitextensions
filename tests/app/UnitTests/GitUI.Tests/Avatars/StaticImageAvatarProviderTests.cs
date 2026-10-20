using GitUI.Avatars;

namespace GitUITests.Avatars;
public class StaticImageAvatarProviderTests : IDisposable
{
    private const int _size = 64;

    private const string _email = "a@a.a";
    private const string _name = "John Lennon";

    private readonly Image _img;

    public StaticImageAvatarProviderTests()
    {
        _img = new Bitmap(_size, _size);
    }

    public void Dispose()
    {
        _img.Dispose();
    }

    [Fact]
    public async Task Original_image_is_returned_if_size_matches()
    {
        StaticImageAvatarProvider provider = new(_img);

        Image? result = await provider.GetAvatarAsync(_email, _name, _size);

        result.Should().BeSameAs(_img);
    }

    [Fact]
    public async Task Resized_images_are_cached_and_same_instance_is_returned_on_second_call()
    {
        StaticImageAvatarProvider provider = new(_img);
        int otherSize = 32;

        Image? result1 = await provider.GetAvatarAsync(_email, _name, otherSize);
        Image? result2 = await provider.GetAvatarAsync(_email, _name, otherSize);

        result2.Should().BeSameAs(result1);
    }
}
