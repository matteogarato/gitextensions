using GitCommands.Remotes;

namespace GitCommandsTests.Remote;
public class GitHostingRemoteParserTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo.git", "github.com")]
    [InlineData("http://github.com/owner/repo.git", "github.com")]
    [InlineData("https://github.com/owner/repo", "github.com")]
    [InlineData("ssh://git@github.com/owner/repo.git", "github.com")]
    [InlineData("git@github.com/owner/repo.git", "github.com")]
    [InlineData("git@framagit.org:owner/repo.git", "framagit.org")]
    [InlineData("https://framagit.org/owner/repo.git", "framagit.org")]
    [InlineData("git@github.com:owner/repo.git", "github.com")]
    [InlineData("https://git.sr.ht/owner/repo", "git.sr.ht")]
    [InlineData("git@git.sr.ht:owner/repo", "git.sr.ht")]
    [InlineData("https://gitlab.freedesktop.org/owner/repo.git", "gitlab.freedesktop.org")]
    [InlineData("git@gitlab.freedesktop.org:owner/repo.git", "gitlab.freedesktop.org")]
    public void TryExtractGitHubDataFromRemoteUrl(string url, string expectedGitHosting)
    {
        new GitHostingRemoteParser().TryExtractGitHostingDataFromRemoteUrl(url, out string? gitHosting, out string? owner, out string? repository).Should().BeTrue();
        gitHosting.Should().Be(expectedGitHosting);
        owner.Should().Be("owner");
        repository.Should().Be("repo");
    }

    [Fact]
    public void Should_fail_in_parsing_invalid_url()
    {
        GitHostingRemoteParser gitHubRemoteParser = new();
        string url = "https://owner@dev.bad.com/owner/project/_git/repo";
        gitHubRemoteParser.TryExtractGitHostingDataFromRemoteUrl(url, out string? gitHosting, out string? owner, out string? repository).Should().BeFalse();
        gitHosting.Should().BeNull();
        owner.Should().BeNull();
        repository.Should().BeNull();

        gitHubRemoteParser.IsValidRemoteUrl(url).Should().BeFalse();
    }
}
