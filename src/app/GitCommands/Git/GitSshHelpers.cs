using GitCommands.Settings;

namespace GitCommands.Git;

public static class GitSshHelpers
{
    // Note that variants like TortoisePlink.exe are supported too
    public static bool IsPlink
    {
        get => AppSettings.SshPath.EndsWith("plink.exe", StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>Sets the git SSH command path.</summary>
    public static void SetGitSshEnvironmentVariable(string path)
    {
        // Git will use the embedded OpenSSH ssh.exe if empty/unset
        Environment.SetEnvironmentVariable("GIT_SSH", path?.Length is > 0 ? path : null,
            EnvironmentVariableTarget.Process);
    }
}
