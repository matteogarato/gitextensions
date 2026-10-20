using GitCommands.Remotes;

namespace GitCommandsTests.Remote;
public class GitHubRemoteParserTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("http://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("ssh://git@github.com/owner/repo.git")]
    [InlineData("git@github.com/owner/repo.git")]
    public void TryExtractGitHubDataFromRemoteUrl(string url)
    {
        new GitHubRemoteParser().TryExtractGitHubDataFromRemoteUrl(url, out string? owner, out string? repository).Should().BeTrue();
        owner.Should().Be("owner");
        repository.Should().Be("repo");
    }

    [Fact]
    public void Should_fail_in_parsing_invalid_url()
    {
        GitHubRemoteParser gitHubRemoteParser = new();
        string url = "https://owner@dev.bad.com/owner/project/_git/repo";
        gitHubRemoteParser.TryExtractGitHubDataFromRemoteUrl(url, out string? owner, out string? repository).Should().BeFalse();
        owner.Should().BeNull();
        repository.Should().BeNull();

        gitHubRemoteParser.IsValidRemoteUrl(url).Should().BeFalse();
    }
}
