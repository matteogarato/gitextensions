using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO.Abstractions;
using GitCommands.Settings;
using GitExtUtils.GitUI;

namespace GitUI.Avatars;

/// <summary>
///     Decorates an avatar provider, adding persistent caching to the file system.
/// </summary>
public sealed class FileSystemAvatarCache : IAvatarProvider, IAvatarCacheCleaner
{
    private readonly int _cacheDays;
    private readonly string _cacheDir;
    private readonly IFileSystem _fileSystem;
    private readonly IAvatarProvider _inner;

    public FileSystemAvatarCache(IAvatarProvider inner, IFileSystem? fileSystem = null)
    {
        _inner = inner;
        _fileSystem = fileSystem ?? new FileSystem();

        _cacheDays = AppSettings.AvatarImageCacheDays;
        if (_cacheDays < 1)
        {
            const int defaultCacheDays = 30;
            _cacheDays = defaultCacheDays;
        }

        _cacheDir = AppSettings.AvatarImageCachePath;
        if (!_fileSystem.Directory.Exists(_cacheDir))
        {
            _fileSystem.Directory.CreateDirectory(_cacheDir);
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
        var cachePath = AppSettings.AvatarImageCachePath;

        if (_fileSystem.Directory.Exists(cachePath))
        {
            try
            {
                foreach (var file in _fileSystem.Directory.GetFiles(cachePath))
                {
                    try
                    {
                        _fileSystem.File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        // do nothing
                        Trace.WriteLine($"Failed to delete file '{file}'. Error: {ex}");
                    }
                }
            }
            catch (Exception ex)
            {
                // do nothing
                Trace.WriteLine($"Failed to enumerate files. Error: {ex}");
            }
        }

        if (CacheCleared is not null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            CacheCleared.Invoke(this, EventArgs.Empty);
        }
    }

    bool IAvatarProvider.PerformsIo
    {
        get => true;
    }

    /// <inheritdoc />
    async Task<Image?> IAvatarProvider.GetAvatarAsync(string email, string? name, int imageSize)
    {
        if (!_inner.PerformsIo)
        {
            return await _inner.GetAvatarAsync(email, name, imageSize);
        }

        var path = Path.Join(_cacheDir, $"{email}.{imageSize}px.png");

        var image = ReadImage();

        if (image is not null)
        {
            return image;
        }

        image = await _inner.GetAvatarAsync(email, name, imageSize);

        if (image is not null)
        {
            WriteImage();
        }

        return image;

        void WriteImage()
        {
            try
            {
                // Workaround to avoid the "A generic error occurred in GDI+." exception when saving
                // where copying the image in a new one allows the save on disk to be successful...
                using Bitmap newImage = new(image);
                using Stream output = _fileSystem.File.OpenWrite(path);
                newImage.Save(output, ImageFormat.Png);
            }
            catch
            {
            }
        }

        Image? ReadImage()
        {
            if (!HasExpired())
            {
                try
                {
                    using Stream stream = _fileSystem.File.OpenRead(path);
                    return Image.FromStream(stream);
                }
                catch
                {
                    // ignore
                }
            }

            return null;

            bool HasExpired()
            {
                var info = _fileSystem.FileInfo.New(path);

                if (!info.Exists)
                {
                    return true;
                }

                if (AppSettings.AvatarProvider == AvatarProvider.None)
                {
                    // No need to refresh because the image returned is always the same.
                    return false;
                }

                if (info.LastWriteTime < DateTime.Now.AddDays(-_cacheDays))
                {
                    TryDelete();
                    return true;
                }

                return false;
            }

            void TryDelete()
            {
                try
                {
                    _fileSystem.File.Delete(path);
                }
                catch
                {
                    // ignore
                }
            }
        }
    }

    /// <inheritdoc />
    private event EventHandler? CacheCleared;
}
