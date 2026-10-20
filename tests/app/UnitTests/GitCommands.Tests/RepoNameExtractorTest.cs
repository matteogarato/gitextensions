using GitCommands.Config;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests;
public class RepoNameExtractorTest
{
    private IGitModule _module = null!;
    private IRepoNameExtractor _repoNameExtractor = null!;

    public RepoNameExtractorTest()
    {
        _module = Substitute.For<IGitModule>();
        _repoNameExtractor = new RepoNameExtractor(() => _module);
    }

    // These test cases basically verifies Path.GetFileNameWithoutExtension(remoteUrl) and Path.GetFileNameWithoutExtension()
    [Theory]
    [InlineData("origin", "https://github.com/project/repo.git", "project", "repo")]
    [InlineData("origin1", "file://github/project/repo.git", "project", "repo")]
    [InlineData("originx", "https://github.com/extra/extra/project/repo.git", "project", "repo")]
    [InlineData("", "https://github.com/project/repo.git", "project", "repo")]
    [InlineData("", null, null, null)]
    [InlineData(null, null, null, null)]
    [InlineData("remote", "https://github.com/project/", "project", "")]
    [InlineData("origin", "git@github.com/project/repo.git", "project", "repo")]
    public void RepoNameExtractorTest_ValidCurrentRemote(string? remote, string? url, string? expProject, string? expRepo)
    {
        _module.GetCurrentRemote().Returns(x => remote);
        _module.GetRemoteNames().Returns(x => new[] { remote!, "    ", "\t" });
        _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote)).Returns(x => url);

        (string project, string repo) = _repoNameExtractor.Get();

        project.Should().Be(expProject);
        repo.Should().Be(expRepo);
    }

    [Theory]
    [InlineData("origin", "https://github.com/project/repo.git", "project", "repo")]
    [InlineData("origin", "git@github.com/project/repo.git", "project", "repo")]
    public void RepoNameExtractorTest_NoValidCurrentRemote(string? remote, string? url, string? expProject, string? expRepo)
    {
        _module.GetCurrentRemote().Returns("");
        _module.GetRemoteNames().Returns(x => new[] { remote!, "    ", "\t" });
        _module.GetSetting(string.Format(SettingKeyString.RemoteUrl, remote)).Returns(x => url);

        (string project, string repo) = _repoNameExtractor.Get();

        project.Should().Be(expProject);
        repo.Should().Be(expRepo);
    }
}
