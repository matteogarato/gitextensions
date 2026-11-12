using ConEmuWinForms;

namespace GitUI.Shells;

public class ShellProvider : IShellProvider
{
    private static readonly IShellDescriptor _defaultShell = new BashShell();

    private static readonly IShellDescriptor[] _shells =
        [_defaultShell, new CmdShell(), new PwshShell(), new PowerShellShell()];

    IReadOnlyList<IShellDescriptor> IShellProvider.GetShells()
    {
        return _shells;
    }

    IShellDescriptor IShellProvider.GetShell(string? name)
    {
        return _shells.FirstOrDefault(s => s.Name == name) ?? _defaultShell;
    }

    string IShellProvider.GetShellCommandLine(string? shellType)
    {
        var shell = GetShell(shellType);

        if (!shell.HasExecutable || shell.ExecutableCommandLine is null)
        {
            // Fallback to default if ExecutableCommandLine is not set
            return ConEmuConstants.DefaultConsoleCommandLine;
        }

        return shell.ExecutableCommandLine;
    }
}
