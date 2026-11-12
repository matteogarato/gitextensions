using System.Diagnostics.CodeAnalysis;

namespace GitCommands.Remotes;

public sealed class GitHubRemoteParser : GitHostingRemoteParser
{
    public new bool IsValidRemoteUrl(string remoteUrl)
    {
        return TryExtractGitHubDataFromRemoteUrl(remoteUrl, out _, out _);
    }

    public bool TryExtractGitHubDataFromRemoteUrl(string remoteUrl, [NotNullWhen(true)] out string? owner,
        [NotNullWhen(true)] out string? repository)
    {
        return TryExtractGitHostingDataFromRemoteUrl(remoteUrl, out var gitHosting, out owner, out repository) &&
               gitHosting == "github.com";
    }
}
