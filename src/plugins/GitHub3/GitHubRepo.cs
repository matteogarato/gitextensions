using Git.hub;
using GitExtensions.Extensibility.Plugins;

namespace GitExtensions.Plugins.GitHub3;

public class GitHubRepo(Repository repo) : IHostedRepository
{
    private Repository _repo = repo;

    string? IHostedRepository.Owner
    {
        get => _repo.Owner?.Login;
    }

    string IHostedRepository.Name
    {
        get => _repo.Name;
    }

    string IHostedRepository.Description
    {
        get => _repo.Description;
    }

    bool IHostedRepository.IsAFork
    {
        get => _repo.Fork;
    }

    bool IHostedRepository.IsMine
    {
        get => Owner == GitHubLoginInfo.Username;
    }

    bool IHostedRepository.IsPrivate
    {
        get => _repo.Private;
    }

    int IHostedRepository.Forks
    {
        get => _repo.Forks;
    }

    string IHostedRepository.Homepage
    {
        get => _repo.Homepage;
    }

    string? IHostedRepository.ParentUrl
    {
        get
        {
            if (!_repo.Fork)
            {
                return null;
            }

            if (!_repo.Detailed)
            {
                if (_repo.Organization is not null)
                {
                    return null;
                }

                _repo = GitHub3Plugin.GitHub.GetRepository(Owner, Name);
            }

            return CloneProtocol == GitProtocol.Ssh ? _repo.Parent?.SshUrl : _repo.Parent?.CloneUrl;
        }
    }

    string? IHostedRepository.ParentOwner
    {
        get
        {
            if (!_repo.Fork)
            {
                return null;
            }

            if (!_repo.Detailed)
            {
                if (_repo.Organization is not null)
                {
                    return null;
                }

                _repo = GitHub3Plugin.GitHub.GetRepository(Owner, Name);
            }

            return _repo.Parent?.Owner.Login;
        }
    }

    string IHostedRepository.CloneUrl
    {
        get => CloneProtocol == GitProtocol.Ssh ? _repo.SshUrl : _repo.CloneUrl;
    }

    IReadOnlyList<IHostedBranch> IHostedRepository.GetBranches()
    {
        return _repo.GetBranches()
            .Select(branch => new GitHubBranch(branch))
            .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    string IHostedRepository.GetDefaultBranch()
    {
        return _repo.GetDefaultBranch();
    }

    IHostedRepository IHostedRepository.Fork()
    {
        return new GitHubRepo(_repo.CreateFork());
    }

    IReadOnlyList<IPullRequestInformation> IHostedRepository.GetPullRequests()
    {
        var pullRequests = _repo?.GetPullRequests();

        if (pullRequests is not null)
        {
            return pullRequests
                .Select(pr => new GitHubPullRequest(pr))
                .ToList();
        }

        return [];
    }

    int IHostedRepository.CreatePullRequest(string myBranch, string remoteBranch, string title, string body)
    {
        var pullRequest =
            _repo.CreatePullRequest(GitHubLoginInfo.Username + ":" + myBranch, remoteBranch, title, body);

        if (pullRequest?.Number is not > 0)
        {
            throw new Exception("Failed to create pull request.");
        }

        return pullRequest.Number;
    }

    GitProtocol IHostedRepository.CloneProtocol { get; set; } = GitProtocol.Ssh;

    IReadOnlyList<GitProtocol> IHostedRepository.SupportedCloneProtocols { get; set; } =
        new[] { GitProtocol.Ssh, GitProtocol.Https };

    public override string ToString()
    {
        return $"{Owner}/{Name}";
    }
}
