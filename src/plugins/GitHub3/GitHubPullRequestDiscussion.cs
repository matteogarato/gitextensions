using Git.hub;
using GitExtensions.Extensibility.Plugins;

namespace GitExtensions.Plugins.GitHub3;

internal sealed class GitHubPullRequestDiscussion : IPullRequestDiscussion
{
    private readonly PullRequest _pullRequest;

    public GitHubPullRequestDiscussion(PullRequest pullRequest)
    {
        _pullRequest = pullRequest;
        Entries = [];
        ForceReload();
    }

    private List<IDiscussionEntry> Entries { get; set; }

    List<IDiscussionEntry> IPullRequestDiscussion.Entries
    {
        get => Entries;
    }

    void IPullRequestDiscussion.Post(string data)
    {
        _pullRequest.ToIssue().CreateComment(data);
    }

    void IPullRequestDiscussion.ForceReload()
    {
        Entries.Clear();

        Entries.Add(new GitHubDiscussionComment
        {
            Author = _pullRequest.User.Login, Created = _pullRequest.CreatedAt, Body = _pullRequest.Body
        });

        foreach (var commit in _pullRequest.GetCommits())
        {
            Entries.Add(new GitHubDiscussionCommit
            {
                Sha = commit.Sha,
                Author = commit.AuthorName.Replace("<", "&lt;").Replace(">", "&gt;"),
                Created = commit.Commit.Author.Date,
                Body = commit.Commit.Message
            });
        }

        foreach (var comment in _pullRequest.GetIssueComments())
        {
            Entries.Add(new GitHubDiscussionComment
            {
                Author = comment.User.Login, Created = comment.CreatedAt, Body = comment.Body
            });
        }

        Entries = [.. Entries.OrderBy(entry => entry.Created)];
    }
}

internal class GitHubDiscussionComment : IDiscussionEntry
{
    internal string? Author { get; set; }

    internal DateTime Created { get; set; }

    internal string? Body { get; set; }

    string? IDiscussionEntry.Author
    {
        get => Author;
    }

    DateTime IDiscussionEntry.Created
    {
        get => Created;
    }

    string? IDiscussionEntry.Body
    {
        get => Body;
    }
}

internal sealed class GitHubDiscussionCommit : GitHubDiscussionComment, ICommitDiscussionEntry
{
    internal string? Sha { get; set; }

    string? ICommitDiscussionEntry.Sha
    {
        get => Sha;
    }
}
