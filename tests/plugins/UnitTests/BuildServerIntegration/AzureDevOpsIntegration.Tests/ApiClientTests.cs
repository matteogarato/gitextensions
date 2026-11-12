using System.Diagnostics;
using GitExtensions.Plugins.AzureDevOpsIntegration;

namespace AzureDevOpsIntegrationTests;

public class ApiClientTests
{
    [Fact]
    public void CreateCredentialFillStartInfo_should_not_let_the_credential_helper_prompt()
    {
        var startInfo = ApiClient.CreateCredentialFillStartInfo(null);

        startInfo.Arguments.Should().Be("-c credential.interactive=false credential fill");
        startInfo.Environment["GIT_TERMINAL_PROMPT"].Should().Be("0");
        startInfo.Environment["GCM_INTERACTIVE"].Should().Be("never");
    }

    [Fact]
    public void CreateCredentialFillStartInfo_should_leave_the_ssh_askpass_variables_alone()
    {
        // Prompting for an ssh passphrase is forced deliberately for interactive git operations
        var startInfo = ApiClient.CreateCredentialFillStartInfo(null);

        startInfo.Environment.Keys.Should().NotContain(["SSH_ASKPASS", "SSH_ASKPASS_REQUIRE", "DISPLAY"]);
    }

    [Theory]
    [InlineData(null, "git")]
    [InlineData(@"C:\Program Files\Git\cmd\git.exe", @"C:\Program Files\Git\cmd\git.exe")]
    public void CreateCredentialFillStartInfo_should_use_the_configured_git_executable(string? gitExecutable,
        string expected)
    {
        ApiClient.CreateCredentialFillStartInfo(gitExecutable).FileName.Should().Be(expected);
    }

    [Fact]
    public void CreateCredentialFillStartInfo_should_not_show_a_window()
    {
        var startInfo = ApiClient.CreateCredentialFillStartInfo(null);

        startInfo.CreateNoWindow.Should().BeTrue();
        startInfo.UseShellExecute.Should().BeFalse();
        startInfo.RedirectStandardInput.Should().BeTrue();
        startInfo.RedirectStandardOutput.Should().BeTrue();
    }
}
