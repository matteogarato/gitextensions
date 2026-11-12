namespace GitUI.Avatars;

public sealed class StaticImageAvatarProvider : IAvatarProvider
{
    private readonly Image _image;
    private readonly Dictionary<int, Image> _sizeCache = [];
    private readonly Lock _sizeCacheLock = new();

    public StaticImageAvatarProvider(Image image)
    {
        _image = image;
        _sizeCache.Add(_image.Height, image);
    }

    bool IAvatarProvider.PerformsIo
    {
        get => false;
    }

    /// <inheritdoc />
    Task<Image?> IAvatarProvider.GetAvatarAsync(string email, string? name, int imageSize)
    {
        return Task.FromResult<Image?>(GetCachedResizedImage(imageSize));
    }

    private Image GetCachedResizedImage(int imageSize)
    {
        lock (_sizeCacheLock)
        {
            if (_sizeCache.TryGetValue(imageSize, out var image))
            {
                return image;
            }

            Bitmap resizedImage = new(_image, imageSize, imageSize);
            _sizeCache.Add(imageSize, resizedImage);

            return resizedImage;
        }
    }
}
