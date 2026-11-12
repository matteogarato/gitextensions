using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using GitCommands.Remotes;

namespace GitExtensions.Plugins.GitlabIntegration.Settings;

public partial class GitlabRemoteParser : RemoteParser
{
    private static readonly Regex[] _gitLabRegexes = [GitlabHttpsUrlRegex, GitlabSshUrlRegex];

    [GeneratedRegex(@"git(?:@|://)(?<host>[^/]+)[:/](?<owner>[^/]+)/(?<repo>[\w_\.\-]+)\.git",
        RegexOptions.ExplicitCapture)]
    private static partial Regex GitlabSshUrlRegex { get; }

    [GeneratedRegex(@"https?://(?<host>[^/@]+)/(?<owner>.+)/(?<repo>[\w_\.\-]+)(?:\.git)?",
        RegexOptions.ExplicitCapture)]
    private static partial Regex GitlabHttpsUrlRegex { get; }

    public bool IsValidRemoteUrl(string remoteUrl)
    {
        return TryExtractGitlabDataFromRemoteUrl(remoteUrl, out _, out _, out _);
    }

    public bool TryExtractGitlabDataFromRemoteUrl(
        string remoteUrl,
        [NotNullWhen(true)] out string? host,
        [NotNullWhen(true)] out string? owner,
        [NotNullWhen(true)] out string? repository)
    {
        host = null;
        owner = null;
        repository = null;

        var m = MatchRegExes(remoteUrl, _gitLabRegexes);

        if (m is null || !m.Success)
        {
            return false;
        }

        host = m.Groups["host"].Value;
        owner = m.Groups["owner"].Value;
        repository = m.Groups["repo"].Value.Replace(".git", "");
        return true;
    }
}
