using GitExtensions.Extensibility;

namespace GitUI.ScriptsEngine;

internal static class PowerShellHelper
{
    internal static void RunPowerShell(string command, string? argument, string workingDir, bool runInBackground)
    {
        var filename = GetPowerShellExecutable();
        var arguments = (runInBackground ? "" : "-NoExit") + " -ExecutionPolicy Unrestricted -Command \"" + command +
                        " " + argument + "\"";
        EnvironmentConfiguration.SetEnvironmentVariables();

        IExecutable executable = new Executable(filename, workingDir);
        executable.Start(arguments, !runInBackground);
    }

    internal static string GetPowerShellExecutable()
    {
        return OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
    }
}
