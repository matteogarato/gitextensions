using GitCommands.Git;
using GitUI;

namespace GitUITests;

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Microsoft.WindowsDesktop.Other 1.2.3 ")]
    [InlineData("Microsoft.WindowsDesktop.App 1.2.3")] // not followed by whitespace
    [InlineData("Microsoft.WindowsDesktop.App \n")]
    [InlineData("Microsoft.WindowsDesktop.App x.y.z ")]
    [InlineData("Microsoft.WindowsDesktop.App 1.2.z ")]
    public void GetDotnetDesktopRuntimeVersions_shall_return_empty(string? versions)
    {
        UserEnvironmentInformation.GetDotnetDesktopRuntimeVersions(versions!)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4")]
    public void GetDotnetDesktopRuntimeVersions_shall_parse_version(string version)
    {
        UserEnvironmentInformation.GetDotnetDesktopRuntimeVersions($"Microsoft.WindowsDesktop.App {version} whatever")
            .Select(v => v.ToString()).Should().BeEquivalentTo(version);
    }

    [Theory]
    [InlineData("1.2-rc3.42.17", "1.2")]
    [InlineData("1.2.3-", "1.2.3")]
    [InlineData("1.2.3.4-suffix", "1.2.3.4")]
    public void GetDotnetDesktopRuntimeVersions_shall_ignore_suffix(string version, string expected)
    {
        UserEnvironmentInformation.GetDotnetDesktopRuntimeVersions($"Microsoft.WindowsDesktop.App {version} whatever")
            .Select(v => v.ToString()).Should().BeEquivalentTo(expected);
    }
}
