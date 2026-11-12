using System.Net;
using System.Text;
using Git.hub;
using GitExtensions.Extensibility.Plugins;

namespace GitExtensions.Plugins.GitHub3;

internal sealed class GitHubPullRequest(PullRequest pullRequest) : IPullRequestInformation
{
    private IHostedRepository? _baseRepo;

    private string? _diffData;

    private IPullRequestDiscussion? _discussion;

    private IHostedRepository? _headRepo;

    string IPullRequestInformation.Title
    {
        get => pullRequest.Title;
    }

    string IPullRequestInformation.Body
    {
        get => pullRequest.Body;
    }

    string IPullRequestInformation.Owner
    {
        get => pullRequest.User.Login;
    }

    DateTime IPullRequestInformation.Created
    {
        get => pullRequest.CreatedAt;
    }

    async Task<string> IPullRequestInformation.GetDiffDataAsync()
    {
        if (_diffData is null)
        {
#pragma warning disable SYSLIB0014 // 'WebRequest.Create(string)' is obsolete
            var request = (HttpWebRequest)WebRequest.Create(pullRequest.DiffUrl);
#pragma warning restore SYSLIB0014 // 'WebRequest.Create(string)' is obsolete
            using var response = await request.GetResponseAsync();
            using StreamReader reader = new(response.GetResponseStream(), Encoding.UTF8);
            _diffData = await reader.ReadToEndAsync();
        }

        return _diffData;
    }

    IHostedRepository IPullRequestInformation.BaseRepo
    {
        get => _baseRepo ??= new GitHubRepo(pullRequest.Base.Repo);
    }

    IHostedRepository IPullRequestInformation.HeadRepo
    {
        get => _headRepo ??= new GitHubRepo(pullRequest.Head.Repo);
    }

    string IPullRequestInformation.BaseSha
    {
        get => pullRequest.Base.Sha;
    }

    string IPullRequestInformation.HeadSha
    {
        get => pullRequest.Head.Sha;
    }

    string IPullRequestInformation.BaseRef
    {
        get => pullRequest.Base.Ref;
    }

    string IPullRequestInformation.HeadRef
    {
        get => pullRequest.Head.Ref;
    }

    string IPullRequestInformation.Id
    {
        get => pullRequest.Number.ToString();
    }

    string IPullRequestInformation.DetailedInfo
    {
        get => string.Format("Base repo owner: {0}\nHead repo owner: {1}", BaseRepo.Owner, HeadRepo.Owner);
    }

    string IPullRequestInformation.FetchBranch
    {
        get => string.Format("pr/n{0}_{1}", Id, HeadRef);
    }

    void IPullRequestInformation.Close()
    {
        pullRequest.Close();
    }

    IPullRequestDiscussion IPullRequestInformation.GetDiscussion()
    {
        return _discussion ??= new GitHubPullRequestDiscussion(pullRequest);
    }
}
