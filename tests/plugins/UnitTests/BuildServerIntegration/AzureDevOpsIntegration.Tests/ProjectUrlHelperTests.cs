using GitExtensions.Plugins.AzureDevOpsIntegration.Settings;

namespace AzureDevOpsIntegrationTests;

public class ProjectUrlHelperTests
{
    [Theory]
    [InlineData("https://user.visualstudio.com/DefaultCollection/MyProject/_git/MyProject",
        "https://user.visualstudio.com/MyProject")]
    [InlineData("http://user.visualstudio.com/DefaultCollection/MyProject/_git/MyProject",
        "http://user.visualstudio.com/MyProject")]
    [InlineData("https://user.visualstudio.com/MyProject/_git/MyProject", "https://user.visualstudio.com/MyProject")]
    [InlineData("http://user.visualstudio.com/MyProject/_git/MyProject", "http://user.visualstudio.com/MyProject")]
    [InlineData("user@vs-ssh.visualstudio.com:v3/user/MyProject/MyProject", "https://user.visualstudio.com/MyProject")]
    [InlineData("https://user@dev.azure.com/user/MyProject/_git/MyProject", "https://dev.azure.com/user/MyProject")]
    [InlineData("http://user@dev.azure.com/user/MyProject/_git/MyProject", "http://dev.azure.com/user/MyProject")]
    [InlineData("git@ssh.dev.azure.com:v3/user/MyProject/MyProject", "https://dev.azure.com/user/MyProject")]
    [InlineData("https://somehost:8080/tfs/DefaultCollection/_git/MyProject",
        "https://somehost:8080/tfs/DefaultCollection/MyProject")]
    [InlineData("http://somehost:8080/tfs/DefaultCollection/_git/MyProject",
        "http://somehost:8080/tfs/DefaultCollection/MyProject")]
    [InlineData("https://somehost:8080/tfs/DefaultCollection/MyProject/_git/SecondaryRepo",
        "https://somehost:8080/tfs/DefaultCollection/MyProject")]
    [InlineData("http://somehost:8080/tfs/DefaultCollection/MyProject/_git/SecondaryRepo",
        "http://somehost:8080/tfs/DefaultCollection/MyProject")]
    public void TryDetectProjectFromRemoteUrl_should_succeed_with_expected_url_from_valid_remote(string? remoteUrl,
        string expectedProjectUrl)
    {
        var (success, projectUrl) = ProjectUrlHelper.TryDetectProjectFromRemoteUrl(remoteUrl);
        success.Should().Be(true);
        projectUrl.Should().BeEquivalentTo(expectedProjectUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notAurl")]
    [InlineData("https://www.google.de")]
    [InlineData("https://github.com/example/project.git")]
    [InlineData("git@github.com:example/project.git")]
    [InlineData("https://user@bitbucket.org/example/project.git")]
    [InlineData("git@bitbucket.org:example/project.git")]
    [InlineData(
        @"[TestCase(""http://somehost:8080/tfs/DefaultCollection/MyProject/_git/SecondaryRepo"", ""http://somehost:8080/tfs/DefaultCollection/MyProject"")]")]
    public void TryDetectProjectFromRemoteUrl_should_fail_with_invalid_remote(string? remoteUrl)
    {
        var (success, projectUrl) = ProjectUrlHelper.TryDetectProjectFromRemoteUrl(remoteUrl);
        success.Should().Be(false);
        projectUrl.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData("https://user.visualstudio.com/MyProject", "https://user.visualstudio.com/_details/security/tokens")]
    [InlineData("http://user.visualstudio.com/MyProject", "http://user.visualstudio.com/_details/security/tokens")]
    [InlineData("https://dev.azure.com/user/MyProject", "https://dev.azure.com/user/_details/security/tokens")]
    [InlineData("http://dev.azure.com/user/MyProject", "http://dev.azure.com/user/_details/security/tokens")]
    [InlineData("https://somehost:8080/tfs/DefaultCollection/MyProject",
        "https://somehost:8080/tfs/DefaultCollection/_details/security/tokens")]
    [InlineData("http://somehost:8080/tfs/DefaultCollection/MyProject",
        "http://somehost:8080/tfs/DefaultCollection/_details/security/tokens")]
    public void TryGetTokenManagementUrlFromProject_should_succeed_with_expected_url_from_valid_projecturl(
        string? projectUrl, string expectedTokenManagementUrl)
    {
        var (success, tokenManagementUrl) = ProjectUrlHelper.TryGetTokenManagementUrlFromProject(projectUrl);
        success.Should().Be(true);
        tokenManagementUrl.Should().BeEquivalentTo(expectedTokenManagementUrl);
    }

    /// <remarks>
    ///     TryGetTokenManagementUrlFromProject will happily convert anything that somewhat looks like a project url
    ///     in favor of better availability for on premise installations of TFS
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notAurl")]
    [InlineData("https://www.google.de")]
    [InlineData(
        @"[TestCase(""http://somehost:8080/tfs/DefaultCollection/MyProject"", ""http://somehost:8080/tfs/DefaultCollection/_details/security/tokens"")]")]
    public void TryGetTokenManagementUrlFromProject_should_fail_with_url_that_does_not_look_like_a_project_url(
        string? projectUrl)
    {
        var (success, tokenManagementUrl) = ProjectUrlHelper.TryGetTokenManagementUrlFromProject(projectUrl);
        success.Should().Be(false);
        tokenManagementUrl.Should().BeNullOrEmpty();
    }

    [Theory]
    [InlineData("https://user.visualstudio.com/MyProject/_build/results?buildId=42&view=results",
        "https://user.visualstudio.com/MyProject", 42)]
    [InlineData("http://user.visualstudio.com/MyProject/_build/results?buildId=42&view=results",
        "http://user.visualstudio.com/MyProject", 42)]
    [InlineData("https://dev.azure.com/user/MyProject/_build/results?buildId=42&view=results",
        "https://dev.azure.com/user/MyProject", 42)]
    [InlineData("http://dev.azure.com/user/MyProject/_build/results?buildId=42&view=results",
        "http://dev.azure.com/user/MyProject", 42)]
    [InlineData("https://somehost:8080/tfs/DefaultCollection/MyProject/_build/index?buildId=1&view=summary",
        "https://somehost:8080/tfs/DefaultCollection/MyProject", 1)]
    [InlineData("http://somehost:8080/tfs/DefaultCollection/MyProject/_build/index?buildId=987&view=summary",
        "http://somehost:8080/tfs/DefaultCollection/MyProject", 987)]
    public void TryParseBuildUrl_should_succeed_with_expected_build_info_from_valid_buildurl(string? buildUrl,
        string expectedProjectUrl, int expectedBuildId)
    {
        var (success, projectUrl, buildId) = ProjectUrlHelper.TryParseBuildUrl(buildUrl);
        success.Should().Be(true);
        projectUrl.Should().BeEquivalentTo(expectedProjectUrl);
        buildId.Should().Be(expectedBuildId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("notAurl")]
    [InlineData("https://www.google.de")]
    [InlineData(
        @"[TestCase(""https://somehost:8080/tfs/DefaultCollection/MyProject/_build/index?buildId=42&view=summary"", ""https://somehost:8080/tfs/DefaultCollection/MyProject"", 42)]")]
    public void TryParseBuildUrl_should_fail_with_invalid_buildurl(string? buildUrl)
    {
        var (success, projectUrl, buildId) = ProjectUrlHelper.TryParseBuildUrl(buildUrl);
        success.Should().Be(false);
        projectUrl.Should().BeNullOrEmpty();
        buildId.Should().BeLessThan(0);
    }
}
