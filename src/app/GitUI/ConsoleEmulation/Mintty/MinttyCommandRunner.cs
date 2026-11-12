using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitExtUtils.GitUI;

namespace GitUI.ConsoleEmulation.Mintty;

internal sealed partial class MinttyCommandRunner : IConsoleCommandRunner
{
    private readonly string _bashPath;
    private readonly string _minttyPath;
    private readonly Panel _panel;
    private readonly ConsoleEmulatorSettings _settings;

    private MinttyControl? _terminal;

    internal MinttyCommandRunner(string minttyPath, string bashPath, ConsoleEmulatorSettings settings)
    {
        _minttyPath = minttyPath;
        _bashPath = bashPath;
        _settings = settings;

        _panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
    }

    Control IConsoleCommandRunner.Control
    {
        get => _panel;
    }

    event EventHandler<ConsoleOutputEventArgs>? IConsoleCommandRunner.CommandOutputReceived
    {
        add => CommandOutputReceived += value;
        remove => CommandOutputReceived -= value;
    }

    event EventHandler<ConsoleProcessExitEventArgs>? IConsoleCommandRunner.CommandProcessExited
    {
        add => CommandProcessExited += value;
        remove => CommandProcessExited -= value;
    }

    event EventHandler? IConsoleCommandRunner.ConsoleHostTerminated
    {
        add => ConsoleHostTerminated += value;
        remove => ConsoleHostTerminated -= value;
    }

    void IConsoleCommandRunner.WriteCommandProcessInput(string text)
    {
        _terminal?.SendConsoleInput(text);
    }

    void IConsoleCommandRunner.KillCommandProcess()
    {
        _terminal?.RunningSession?.Kill();
    }

    [MemberNotNull(nameof(_terminal))]
    void IConsoleCommandRunner.ResetConsole()
    {
        var oldTerminal = _terminal;

        _terminal = new MinttyControl { Dock = DockStyle.Fill };

        if (oldTerminal is not null)
        {
            _panel.Controls.Remove(oldTerminal);
            oldTerminal.Dispose();
        }

        _panel.Controls.Add(_terminal);
    }

    void IConsoleCommandRunner.StartCommand(string command, string arguments, string workDir,
        Dictionary<string, string> envVariables)
    {
        if (_terminal is null)
        {
            ResetConsole();
        }

        var operation = CommandLog.LogProcessStart(command, arguments, workDir);

        try
        {
            var commandLine = new ArgumentBuilder { command.Quote(), arguments }.ToString();

            MinttyStartInfo startInfo = new()
            {
                ProcessOperation = operation,
                ConsoleProcessCommandLine = commandLine,
                StartupDirectory = workDir,
                ProcessExitedCallback = exitCode =>
                {
                    operation.LogProcessEnd(exitCode);
                    Control.InvokeAndForget(() =>
                        CommandProcessExited?.Invoke(this, new ConsoleProcessExitEventArgs(exitCode)));
                },
                ConsoleClosedCallback = () =>
                {
                    Control.InvokeAndForget(() => ConsoleHostTerminated?.Invoke(this, EventArgs.Empty));
                },
                AnsiOutputLineCallback = line =>
                {
                    line = StripAnsiCodesRegex().Replace(line, string.Empty);
                    Control.InvokeAndForget(() =>
                        CommandOutputReceived?.Invoke(this, new ConsoleOutputEventArgs(line)));
                }
            };

            foreach ((var name, var value) in envVariables)
            {
                startInfo.EnvironmentVariables[name] = value;
            }

            _terminal.StartCommand(startInfo, _minttyPath, _bashPath, _settings);
        }
        catch (Exception ex)
        {
            operation.LogProcessEnd(ex);
            throw;
        }
    }

    private event EventHandler<ConsoleOutputEventArgs>? CommandOutputReceived;

    private event EventHandler<ConsoleProcessExitEventArgs>? CommandProcessExited;

    private event EventHandler? ConsoleHostTerminated;

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])")]
    private static partial Regex StripAnsiCodesRegex();
}
