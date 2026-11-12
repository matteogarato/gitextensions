using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Net;
using GitUI.Interops;
using Microsoft.VisualStudio.Threading;

namespace GitUI.Avatars;

/// <summary>
///     Helps downloading avatar images and implements concurrent requests and automatic retries.
/// </summary>
public sealed class AvatarDownloader : IAvatarDownloader
{
    private const int _maxConcurrentDownloads = 10;
    private const int _requestCacheDurationInSeconds = 30;

    private static readonly TimeSpan _requestCacheTimeSpan = TimeSpan.FromSeconds(_requestCacheDurationInSeconds);

    private static readonly SemaphoreSlim _downloadSemaphore = new(_maxConcurrentDownloads);
    private static readonly ConcurrentDictionary<Uri, (DateTime, Task<Image?>)> _downloads = new();

    private static readonly HttpClient _client = new(new HttpClientHandler
    {
        UseProxy = true, DefaultProxyCredentials = CredentialCache.DefaultCredentials
    });

    async Task<Image?> IAvatarDownloader.DownloadImageAsync(Uri? imageUrl)
    {
        if (imageUrl is null)
        {
            return null;
        }

        // check network connectivity
        if (!NativeMethods.InternetGetConnectedState(out _, 0))
        {
            return null;
        }

        ClearOldCacheEntries();

        var errorCount = 0;

        while (true)
        {
            (_, var task) = _downloads.GetOrAdd(imageUrl, _ => (DateTime.UtcNow, DownloadAsync(imageUrl)));

            // If we discover a faulted task, remove it and try again
            if (task.IsFaulted || task.IsCanceled)
            {
                _downloads.TryRemove(imageUrl, out _);

                if (++errorCount > 3)
                {
                    await task;
                }

                continue;
            }

            var image = await task;
            if (image?.PixelFormat == PixelFormat.DontCare)
            {
                // Image from cached download has been disposed (in all probability during a cache cleanup)
                _downloads.TryRemove(imageUrl, out _);
                continue;
            }

            return image;
        }
    }

    private static async Task<Image?> DownloadAsync(Uri imageUrl)
    {
        // Get onto background thread
        await TaskScheduler.Default;

        // Limit the number of concurrent download requests
        await _downloadSemaphore.WaitAsync();

        try
        {
            using var response = await _client.GetAsync(imageUrl);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var imageStream = await response.Content.ReadAsStreamAsync();
            return Image.FromStream(imageStream);
        }
        catch (Exception ex)
        {
            // catch IO errors
            Trace.WriteLine(ex.Message);
        }
        finally
        {
            _downloadSemaphore.Release();
        }

        return null;
    }

    private void ClearOldCacheEntries()
    {
        var now = DateTime.UtcNow;

        foreach ((var key, (var time, var _)) in _downloads)
        {
            if (now - time > _requestCacheTimeSpan)
            {
                _downloads.TryRemove(key, out _);
            }
        }
    }
}
