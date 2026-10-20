using GitUI.Avatars;
using NSubstitute;

namespace GitUITests.Avatars;
public class HotSwapProviderTests : IDisposable
{
    private const int _size = 16;
    private const string _email = "a@a.a";
    private const string _name = "John Lennon";

    private readonly Image _img;

    public HotSwapProviderTests()
    {
        _img = new Bitmap(_size, _size);
    }

    public void Dispose()
    {
        _img.Dispose();
    }

    [Fact]
    public async Task Returns_null_if_no_provider_is_set()
    {
        HotSwapAvatarProvider provider = new();
        Image? image = await provider.GetAvatarAsync(_email, _name, 16);
        image.Should().BeNull();
    }

    [Fact]
    public async Task Returns_the_same_image_as_the_wrapped_provider()
    {
        HotSwapAvatarProvider provider = new();
        IAvatarProvider inner = Substitute.For<IAvatarProvider>();
        provider.Provider = inner;

        inner.GetAvatarAsync(_email, _name, _size).Returns(_img);

        Image? result = await provider.GetAvatarAsync(_email, _name, _size);

        result.Should().BeSameAs(_img);
    }
}
