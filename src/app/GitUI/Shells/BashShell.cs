using GitCommands;
using GitUI.Properties;

namespace GitUI.Shells;

public class BashShell : ShellDescriptor
{
    private const string _gitBashExe = "git-bash.exe"; // Bash with git in the path, should generally be in the git dir
    private const string _bashExe = "bash.exe"; // Fallback to generic bash, should generally be in the git bin dir
    private const string _shExe = "sh.exe"; // Fallback to SH
    public const string ShellName = "bash";

    public BashShell()
    {
        Name = ShellName;
        Icon = Images.GitForWindows;

        if (PathUtil.TryFindShellPath(_gitBashExe, out var exePath))
        {
            ExecutableName = _gitBashExe;
            ExecutablePath = exePath;

            // Try to find bash or sh below to set ExecutableCommandLine, as git-bash.exe cannot be connected to the built-in console.
        }

        foreach (var shellExecutableName in new[] { _bashExe, _shExe })
        {
            if (PathUtil.TryFindShellPath(shellExecutableName, out exePath))
            {
                if (ExecutablePath is null)
                {
                    ExecutableName = shellExecutableName;
                    ExecutablePath = exePath;
                }

                ExecutableCommandLine = $"{exePath.Quote()} --login -i";

                break;
            }
        }
    }

    public override string GetChangeDirCommand(string path)
    {
        try
        {
            DirectoryInfo directoryInfo = new(path);
            if (directoryInfo.Exists)
            {
                var posixPath = "/" + directoryInfo.FullName.ToPosixPath().Remove(1, 1);
                return $"cd {posixPath.QuoteNe()}";
            }
        }
        catch
        {
            // no-op
        }

        return string.Empty;
    }
}
