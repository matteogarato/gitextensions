using GitCommands;

namespace GitUI.ConsoleEmulation.Mintty;

internal sealed class MinttyShellRunner : IConsoleShellRunner
{
    private readonly string _bashPath;
    private readonly MinttyControl _control;
    private readonly string _minttyPath;
    private readonly ConsoleEmulatorSettings _settings;

    internal MinttyShellRunner(string minttyPath, string bashPath, ConsoleEmulatorSettings settings)
    {
        _minttyPath = minttyPath;
        _bashPath = bashPath;
        _settings = settings;
        _control = new MinttyControl { Dock = DockStyle.Fill };
    }

    bool IConsoleShellRunner.IsShellRunning
    {
        get => _control.IsShellRunning;
    }

    Control IConsoleShellRunner.Control
    {
        get => _control;
    }

    void IConsoleShellRunner.StartShell(string workDir)
    {
        _control.StartInteractiveShell(_minttyPath, _bashPath, workDir, _settings);
    }

    void IConsoleShellRunner.ChangeWorkingDirectory(string path)
    {
        var changeDirCommand = $"cd \"{path.ToMountPath("/")}\"";
        _control.SendConsoleInput($"\x1\xb{changeDirCommand}\n");
    }

    void IConsoleShellRunner.FocusTerminal()
    {
        _control.Focus();
    }
}
