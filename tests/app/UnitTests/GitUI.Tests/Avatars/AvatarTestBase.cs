using System.Drawing.Imaging;
using GitUI.Avatars;
using NSubstitute;

namespace GitUITests.Avatars;

public abstract class AvatarTestBase : IDisposable
{
    protected const int Size = 16;

    protected const string Email1 = "a@a.a";
    protected const string Email2 = "b@b.b";
    protected const string Email3 = "c@c.c";
    protected const string Email4 = "d@d.d";
    protected const string EmailMissing = "missing@avatar.com";

    protected const string Name1 = "John Lennon";
    protected const string Name2 = "Paul McCartney";
    protected const string Name3 = "George Harrison";
    protected const string Name4 = "Ringo Starr";
    protected const string NameMissing = "Fifth Beatle";
    protected IAvatarProvider Cache = null!;

    protected Image Img1 = null!;
    protected Image Img2 = null!;
    protected Image Img3 = null!;
    protected Image Img4 = null!;
    protected Image ImgGenerated = null!;

    protected IAvatarProvider Inner = null!;

    public AvatarTestBase()
    {
        Img1 = new Bitmap(Size, Size);
        Img2 = new Bitmap(Size, Size);
        Img3 = new Bitmap(Size, Size);
        Img4 = new Bitmap(Size, Size);
        ImgGenerated = new Bitmap(Size, Size);
        InitializeAvatarProvider();
    }

    protected IAvatarCacheCleaner CacheCleaner
    {
        get => (Cache as IAvatarCacheCleaner)!;
    }

    void IDisposable.Dispose()
    {
        Img1.Dispose();
        Img2.Dispose();
        Img3.Dispose();
        Img4.Dispose();
        ImgGenerated.Dispose();
    }

    private void InitializeAvatarProvider()
    {
        Inner = Substitute.For<IAvatarProvider>();

        Inner.PerformsIo.Returns(true);
        Inner.GetAvatarAsync(Email1, Name1, Size).Returns(Task.FromResult<Image?>(Img1));
        Inner.GetAvatarAsync(Email2, Name2, Size).Returns(Task.FromResult<Image?>(Img2));
        Inner.GetAvatarAsync(Email3, Name3, Size).Returns(Task.FromResult<Image?>(Img3));
        Inner.GetAvatarAsync(Email4, Name4, Size).Returns(Task.FromResult<Image?>(Img4));
        Inner.GetAvatarAsync(EmailMissing, NameMissing, Size).Returns(Task.FromResult((Image?)null));
    }

    protected async Task MissAsync(string email, string name, Image expected = null!)
    {
        Inner.ClearReceivedCalls();

        var actual = await Cache.GetAvatarAsync(email, name, Size);

        _ = Inner.Received(1).GetAvatarAsync(email, name, Size);

        if (expected is not null)
        {
            actual.Should().BeSameAs(expected);
        }
    }

    protected async Task HitAsync(string email, string name, Image expected = null!)
    {
        Inner.ClearReceivedCalls();

        var actual = await Cache.GetAvatarAsync(email, name, Size);

        _ = Inner.Received(0).GetAvatarAsync(email, name, Size);

        if (expected is not null)
        {
            actual.Should().BeSameAs(expected);
        }
    }

    protected Stream GetPngStream()
    {
        MemoryStream stream = new();
        Img1.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        return stream;
    }
}
