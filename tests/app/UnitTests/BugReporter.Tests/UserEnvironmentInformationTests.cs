using BugReporter;
using GitCommands.Git;

namespace BugReporterTests;

public sealed class UserEnvironmentInformationTests
{
    [Fact]
    public void GitVersion_is_good()
    {
        var gitString = UserEnvironmentInformation.GetGitVersionInfo("2.21.0.windows.1", new GitVersion("2.18.0"),
            new GitVersion("2.21.0"));
        gitString.Should().Be("2.21.0.windows.1");
    }

    [Fact]
    public void GitVersion_is_old_but_supported()
    {
        var gitString = UserEnvironmentInformation.GetGitVersionInfo("2.20.1.windows.1", new GitVersion("2.18.0"),
            new GitVersion("2.21.0"));
        gitString.Should().Be("2.20.1.windows.1 (recommended: 2.21.0 or later)");
    }

    [Fact]
    public void GitVersion_is_not_supported()
    {
        var gitString = UserEnvironmentInformation.GetGitVersionInfo("1.6.5.windows.1", new GitVersion("2.18.0"),
            new GitVersion("2.21.0"));
        gitString.Should().Be("1.6.5.windows.1 (minimum: 2.18.0, please update!)");
    }

    [Fact]
    public void GitVersion_is_unknown_then_return_all_data()
    {
        var gitString = UserEnvironmentInformation.GetGitVersionInfo(null, new GitVersion("2.18.0"),
            new GitVersion("2.21.0"));
        gitString.Should().Be("- (minimum: 2.18.0, recommended: 2.21.0)");
    }
}
