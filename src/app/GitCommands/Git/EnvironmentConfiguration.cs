using GitCommands.Settings;

namespace GitCommands.Git;

public static class EnvironmentConfiguration
{
    private static readonly IEnvironmentAbstraction _env = new EnvironmentAbstraction();

    /// <summary>
    ///     The <c>USER</c> environment variable's value for the user/machine.
    /// </summary>
    private static readonly string? _userHomeDir
        = _env.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.User)
          ?? _env.GetEnvironmentVariable("HOME", EnvironmentVariableTarget.Machine);

    public static string? GetEnvironmentVariable(string name)
    {
        return _env.GetEnvironmentVariable(name);
    }

    /// <summary>
    ///     Sets <c>PATH</c>, <c>HOME</c>, <c>TERM</c> and <c>SSH_ASKPASS</c> environment variables
    ///     for the current process.
    /// </summary>
    public static void SetEnvironmentVariables()
    {
        // PATH variable

        if (!string.IsNullOrEmpty(AppSettings.LinuxToolsDir))
        {
            // Ensure the GNU/Linux tools dir is on the path
            var path = _env.GetEnvironmentVariable("PATH");

            if (path is null)
            {
                _env.SetEnvironmentVariable("PATH", AppSettings.LinuxToolsDir);
            }
            else if (!path.Contains(AppSettings.LinuxToolsDir))
            {
                _env.SetEnvironmentVariable("PATH", $"{path}{Path.PathSeparator}{AppSettings.LinuxToolsDir}");
            }
        }

        // HOME variable
        _env.SetEnvironmentVariable("HOME", ComputeHomeLocation());

        // TERM variable

        // to prevent from leaking processes see issue #1092 for details
        _env.SetEnvironmentVariable("TERM", "msys");

        // Force a non-empty DISPLAY so ssh uses SSH_ASKPASS if it has no terminal
        if (string.IsNullOrEmpty(_env.GetEnvironmentVariable("DISPLAY")))
        {
            _env.SetEnvironmentVariable("DISPLAY", ":");
        }

        // SSH_ASKPASS variable

        if (OperatingSystem.IsWindows())
        {
            var sshAskPass = Path.Join(AppSettings.GetInstallDir()!, "GitExtSshAskPass.exe");

            if (File.Exists(sshAskPass))
            {
                _env.SetEnvironmentVariable("SSH_ASKPASS", sshAskPass);
            }
        }
        else if (string.IsNullOrEmpty(_env.GetEnvironmentVariable("SSH_ASKPASS")))
        {
            _env.SetEnvironmentVariable("SSH_ASKPASS", "ssh-askpass");
        }

        if (!string.IsNullOrEmpty(_env.GetEnvironmentVariable("SSH_ASKPASS")))
        {
            _env.SetEnvironmentVariable("SSH_ASKPASS_REQUIRE", "force");
        }

        return;

        static string? ComputeHomeLocation()
        {
            if (!string.IsNullOrEmpty(AppSettings.CustomHomeDir))
            {
                return AppSettings.CustomHomeDir;
            }

            if (AppSettings.UserProfileHomeDir)
            {
                return _env.GetEnvironmentVariable("USERPROFILE");
            }

            return GetDefaultHomeDir();
        }
    }

    /// <summary>
    ///     Gets the value of the current process's <c>HOME</c> environment variable.
    /// </summary>
    /// <returns>The variable's value, or an empty string if it is not present.</returns>
    public static string GetHomeDir()
    {
        return _env.GetEnvironmentVariable("HOME") ?? "";
    }

    public static string? GetDefaultHomeDir()
    {
        // Use the HOME property from the user or machine, as captured at startup
        if (!string.IsNullOrEmpty(_userHomeDir))
        {
            return _userHomeDir;
        }

        if (OperatingSystem.IsWindows())
        {
            // Use the Windows default home directory
            var homeDrive = _env.GetEnvironmentVariable("HOMEDRIVE");

            if (!string.IsNullOrEmpty(homeDrive))
            {
                return homeDrive + _env.GetEnvironmentVariable("HOMEPATH");
            }

            return _env.GetEnvironmentVariable("USERPROFILE");
        }

        return _env.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
