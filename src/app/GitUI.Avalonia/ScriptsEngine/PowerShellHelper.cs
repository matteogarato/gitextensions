using GitCommands;
using GitExtensions.Extensibility;

namespace GitUI.ScriptsEngine;

internal static class PowerShellHelper
{
    internal static void RunPowerShell(string command, string? argument, string workingDir, bool runInBackground)
    {
        string filename = GetPowerShellExecutable();
        string arguments = (runInBackground ? "" : "-NoExit") + " -ExecutionPolicy Unrestricted -Command \"" + command + " " + argument + "\"";
        EnvironmentConfiguration.SetEnvironmentVariables();

        IExecutable executable = new Executable(filename, workingDir);
        executable.Start(arguments, createWindow: !runInBackground);
    }

    internal static string GetPowerShellExecutable()
        => OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
}
