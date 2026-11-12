using GitUI.Avatars;
using NSubstitute;

namespace GitUITests.Avatars;

public class ChainedAvatarProviderTests : IDisposable
{
    private const int _size = 16;

    private const string _email1 = "a@a.a";
    private const string _email2 = "b@b.b";
    private const string _email3 = "c@c.c";
    private const string _email4 = "d@d.d";

    private const string _name1 = "John Lennon";
    private const string _name2 = "Paul McCartney";
    private const string _name3 = "George Harrison";
    private const string _name4 = "Ringo Starr";

    private readonly Image _img1 = new Bitmap(_size, _size);
    private readonly Image _img2 = new Bitmap(_size, _size);
    private readonly Image _img3 = new Bitmap(_size, _size);
    private readonly Image _img4 = new Bitmap(_size, _size);
    private readonly Image _img5 = new Bitmap(_size, _size);
    private readonly Image _img6 = new Bitmap(_size, _size);

    void IDisposable.Dispose()
    {
        _img1.Dispose();
        _img2.Dispose();
        _img3.Dispose();
        _img4.Dispose();
        _img5.Dispose();
        _img6.Dispose();
    }

    [Fact]
    public async Task Construction_without_parameter_is_allowed_and_returns_null()
    {
        ChainedAvatarProvider provider = new();

        var image = await provider.GetAvatarAsync(_email1, _name1, _size);
        image.Should().BeNull();
    }

    [Fact]
    public void Construction_with_null_parameters_is_permitted()
    {
        ((Action)(() => new ChainedAvatarProvider(null!))).Should().Throw<ArgumentNullException>();

        ((Action)(() => new ChainedAvatarProvider(null!, null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Return_first_non_null_result()
    {
        var provider1 = Substitute.For<IAvatarProvider>();
        var provider2 = Substitute.For<IAvatarProvider>();
        var provider3 = Substitute.For<IAvatarProvider>();

        // Register different images (for the same parameters)
        // for each provider. This allows us to detect incorrect results.

        // Case 1: First provider hit
        provider1.GetAvatarAsync(_email1, _name1, _size).Returns(_img1);
        provider2.GetAvatarAsync(_email1, _name1, _size).Returns(_img2);
        provider3.GetAvatarAsync(_email1, _name1, _size).Returns(_img3);

        // Case 2: Second provider hit
        provider1.GetAvatarAsync(_email2, _name2, _size).Returns((Image?)null);
        provider2.GetAvatarAsync(_email2, _name2, _size).Returns(_img4);
        provider3.GetAvatarAsync(_email2, _name2, _size).Returns(_img5);

        // Case 3: Third provider hit
        provider1.GetAvatarAsync(_email3, _name3, _size).Returns((Image?)null);
        provider2.GetAvatarAsync(_email3, _name3, _size).Returns((Image?)null);
        provider3.GetAvatarAsync(_email3, _name3, _size).Returns(_img6);

        // Case 4: No provider hit
        provider1.GetAvatarAsync(_email4, _name4, _size).Returns((Image?)null);
        provider2.GetAvatarAsync(_email4, _name4, _size).Returns((Image?)null);
        provider3.GetAvatarAsync(_email4, _name4, _size).Returns((Image?)null);

        ChainedAvatarProvider chainedProvider = new(provider1, provider2, provider3);

        var res1 = await chainedProvider.GetAvatarAsync(_email1, _name1, _size);
        var res2 = await chainedProvider.GetAvatarAsync(_email2, _name2, _size);
        var res3 = await chainedProvider.GetAvatarAsync(_email3, _name3, _size);
        var res4 = await chainedProvider.GetAvatarAsync(_email4, _name4, _size);

        res1.Should().BeSameAs(_img1);
        res2.Should().BeSameAs(_img4);
        res3.Should().BeSameAs(_img6);
        res4.Should().BeSameAs(null);
    }
}
