using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace GitCommands.Remotes;

public partial class GitHostingRemoteParser : RemoteParser
{
    private static readonly Regex[] _gitHostingRegexes = [GitHostingHttpsUrlRegex, GitHostingSshUrlRegex];

    [GeneratedRegex(
        @"^(ssh://)?git(?:@|://)(?<hosting>([^.]+\.)+[^.]+)[:/](?<owner>[^/]+)/(?<repo>[\w_\.\-]+)(?:.git)?",
        RegexOptions.ExplicitCapture)]
    private static partial Regex GitHostingSshUrlRegex { get; }

    [GeneratedRegex(
        @"^https?://(?:[^@:]*?)?(?::[^/@:]*?)?@?(?<hosting>([^./]+\.)+[^./]+)/(?<owner>[^/]+)/(?<repo>[\w_\.\-]+)(?:.git)?$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex GitHostingHttpsUrlRegex { get; }

    /// <summary>
    ///     Gets if an url is the one of a git hosted repository.
    /// </summary>
    /// <param name="remoteUrl">the url of a git repository.</param>
    /// <returns>
    ///     true, if the url has been succefully parsed.
    ///     false otherwise.
    /// </returns>
    public bool IsValidRemoteUrl(string remoteUrl)
    {
        return TryExtractGitHostingDataFromRemoteUrl(remoteUrl, out _, out _, out _);
    }

    public bool TryExtractGitHostingDataFromRemoteUrl(string remoteUrl, [NotNullWhen(true)] out string? gitHosting,
        [NotNullWhen(true)] out string? owner, [NotNullWhen(true)] out string? repository)
    {
        owner = null;
        repository = null;
        gitHosting = null;

        var m = MatchRegExes(remoteUrl, _gitHostingRegexes);

        if (m is null or { Success: false })
        {
            return false;
        }

        gitHosting = m.Groups["hosting"].Value;
        owner = m.Groups["owner"].Value;
        repository = m.Groups["repo"].Value.Replace(".git", "");
        return true;
    }
}
