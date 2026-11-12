using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Git.hub;
using JetBrains.Annotations;

namespace GitUI.Avatars;

public sealed partial class GithubAvatarProvider([NotNull] IAvatarDownloader downloader, bool onlySupplyNoReply = false)
    : IAvatarProvider
{
    /* A brief skim through the Git Extensions repo history shows GitHub emails with the following user names:
     *
     * 25421792+mserfli
     * 33052757+freza-tm
     * gpongelli
     * odie2
     * palver123
     * RaMMicHaeL
     * SamuelLongchamps
     */
    private readonly IAvatarDownloader _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));

    [GeneratedRegex(@"^(\d+\+)?(?<username>[^@]+)@users\.noreply\.github\.com$",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture)]
    private static partial Regex GitHubEmailRegex { get; }

    bool IAvatarProvider.PerformsIo
    {
        get => true;
    }

    async Task<Image?> IAvatarProvider.GetAvatarAsync(string email, string? name, int imageSize)
    {
        var uri = await BuildAvatarUriAsync(email, imageSize);

        if (uri is null)
        {
            return null;
        }

        var image = await _downloader.DownloadImageAsync(uri);

        // Sadly GitHub doesn't provide an option to return a 404 error for non-custom avatars
        // and always provides a fallback image (identicon). Using GitHubs fallback image would
        // render the user defined fallback useless so we have to filter out the identicons.

        // We do this by checking the size of the returned image, because identicons provided by
        // GitHub are never scaled and always 420 x 420 - even if a different size was requested.

        // We exploit that fact to filter out identicons.
        var isIdenticon = imageSize != 420 && image?.Size.Width is 420;

        if (isIdenticon)
        {
            return null;
        }

        return image;
    }

    private async Task<Uri?> BuildAvatarUriAsync(string email, int imageSize)
    {
        var match = GitHubEmailRegex.Match(email);

        if (match.Success)
        {
            // email is an @users.noreply.github.com address

            var username = match.Groups["username"].Value;

            // For real users we can directly access the avatar by using
            // https://avatars.githubusercontent.com/{encodedUsername}?s={imageSize}
            // But for bots this doesn't work. To get the avatar url we can make use of the
            // GitHub API to get the profile (which includes the avatar url) but for unauthenticated
            // requests the rate limits are pretty low (60 requests per hour)

            // To mitigate the issue of possibly hitting the rate limit, we directly load the avatars
            // for all "normal" users and only users that can't be resolved that way (like bots)
            // query the GitHub profile first.

            // GitHub user names can't contain square brackets but bots use them.
            var isBot = username.Contains('[');

            if (isBot)
            {
                Client client = new();
                var userProfile = await client.GetUserAsync(username);

                if (string.IsNullOrEmpty(userProfile?.AvatarUrl))
                {
                    return null;
                }

                UriBuilder builder = new(userProfile.AvatarUrl);
                StringBuilder query = new(builder.Query.TrimStart('?'));
                query.Append(query.Length == 0 ? '?' : '&');
                query.Append("s=").Append(imageSize);
                builder.Query = query.ToString();
                return builder.Uri;
            }

            var encodedUsername = HttpUtility.UrlEncode(match.Groups["username"].Value);
            return new Uri($"https://avatars.githubusercontent.com/{encodedUsername}?s={imageSize}");
        }
        // regular email address

        if (onlySupplyNoReply)
        {
            return null;
        }

        var encodedEmail = HttpUtility.UrlEncode(email);
        return new Uri($"https://avatars.githubusercontent.com/u/e?email={encodedEmail}&s={imageSize}");
    }
}
