using System.Globalization;
using System.Text.RegularExpressions;
using ConEmuWinForms;
using GitCommands.Git;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitExtUtils.GitUI;

namespace GitUI.ConsoleEmulation.ConEmu;

/// <summary>
///     Embeds a ConEmu terminal in the output panel so command dialogs can host an interactive console.
/// </summary>
internal sealed class ConEmuConsoleCommandRunner : ContainerControl, IConsoleCommandRunner
{
    private readonly ConsoleEmulatorSettings _settings;
    private int _nLastExitCode;

    private Panel _panel;
    private ConEmuControl? _terminal;

    public ConEmuConsoleCommandRunner(ConsoleEmulatorSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        Validates.NotNull(_panel);
    }

    Control IConsoleCommandRunner.Control
    {
        get => this;
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
        Validates.NotNull(_terminal);
        this.InvokeAndForget(() => _terminal.RunningSession?.WriteInputTextAsync(text)!);
    }

    void IConsoleCommandRunner.KillCommandProcess()
    {
        Validates.NotNull(_terminal);
        KillProcess(_terminal);
    }

    void IConsoleCommandRunner.ResetConsole()
    {
        var oldTerminal = _terminal;

        _terminal = new ConEmuControl { Dock = DockStyle.Fill, IsStatusbarVisible = false };

        if (oldTerminal is not null)
        {
            KillProcess(oldTerminal);
            _panel.Controls.Remove(oldTerminal);
            oldTerminal.Dispose();
        }

        _panel.Controls.Add(_terminal);
    }

    void IConsoleCommandRunner.StartCommand(string command, string arguments, string workDir,
        Dictionary<string, string> envVariables)
    {
        var operation = CommandLog.LogProcessStart(command, arguments, workDir);

        WriteConsoleOutput($"{command.Quote()} {arguments}{Environment.NewLine}");

        try
        {
            var commandLine = new ArgumentBuilder { command.Quote(), arguments }.ToString();
            ConsoleCommandLineOutputProcessor outputProcessor =
                new(commandLine.Length, args => CommandOutputReceived?.Invoke(this, args));

            ConEmuStartInfo startInfo = new()
            {
                ConsoleProcessCommandLine = commandLine,
                IsEchoingConsoleCommandLine = true,
                WhenConsoleProcessExits = WhenConsoleProcessExits.KeepConsoleEmulatorAndShowMessage,
                AnsiStreamChunkReceivedEventSink = outputProcessor.AnsiStreamChunkReceived,
                StartupDirectory = workDir
            };

            foreach ((var name, var value) in envVariables)
            {
                startInfo.SetEnv(name, value);
            }

            startInfo.ConsoleProcessExitedEventSink = (_, args) =>
            {
                _nLastExitCode = args.ExitCode;
                operation.LogProcessEnd(_nLastExitCode);
                outputProcessor.Flush();
                WriteConsoleOutput("Done");
                CommandProcessExited?.Invoke(this, new ConsoleProcessExitEventArgs(args.ExitCode));
            };

            startInfo.ConsoleEmulatorClosedEventSink = (sender, _) =>
            {
                Validates.NotNull(_terminal);
                if (sender == _terminal.RunningSession)
                {
                    ConsoleHostTerminated?.Invoke(this, EventArgs.Empty);
                }
            };

            Validates.NotNull(_terminal);
            Validates.NotNull(_settings.Font);
            _terminal.Start(startInfo, ThreadHelper.JoinableTaskFactory, _settings.Theme, _settings.Font.Name,
                _settings.Font.Size.ToString("F0", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            operation.LogProcessEnd(ex);
            throw;
        }
    }

    private void InitializeComponent()
    {
        Controls.Add(_panel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None });
    }

    private event EventHandler<ConsoleOutputEventArgs>? CommandOutputReceived;

    private event EventHandler<ConsoleProcessExitEventArgs>? CommandProcessExited;

    private event EventHandler? ConsoleHostTerminated;

    private void WriteConsoleOutput(string text)
    {
        Validates.NotNull(_terminal);
        _terminal.RunningSession?.WriteOutputTextAsync(text);
    }

    private static void KillProcess(ConEmuControl terminal)
    {
        terminal.RunningSession?.SendControlCAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _terminal?.Dispose();
        }

        base.Dispose(disposing);
    }
}

public partial class ConsoleCommandLineOutputProcessor
{
    private readonly Action<ConsoleOutputEventArgs> _fireDataReceived;
    private int _commandLineCharsInOutput;
    private string? _lineChunk;

    public ConsoleCommandLineOutputProcessor(int commandLineCharsInOutput,
        Action<ConsoleOutputEventArgs> fireDataReceived)
    {
        _fireDataReceived = fireDataReceived;
        _commandLineCharsInOutput = commandLineCharsInOutput;
        _commandLineCharsInOutput += Environment.NewLine.Length; // for \n after the command line
    }

    [GeneratedRegex(@"(?<=[\n\r])", RegexOptions.ExplicitCapture)]
    private static partial Regex NewLineRegex { get; }

    private string? FilterOutConsoleCommandLine(string outputChunk)
    {
        if (_commandLineCharsInOutput > 0)
        {
            if (_commandLineCharsInOutput >= outputChunk.Length)
            {
                _commandLineCharsInOutput -= outputChunk.Length;
                return null;
            }

            var rest = outputChunk[_commandLineCharsInOutput..];
            _commandLineCharsInOutput = 0;
            return rest;
        }

        return outputChunk;
    }

    public void AnsiStreamChunkReceived(object? sender, AnsiStreamChunkEventArgs args)
    {
        var text = args.GetText(GitModule.SystemEncoding);
        var filtered = FilterOutConsoleCommandLine(text);
        if (filtered is not null)
        {
            SendAsLines(filtered);
        }
    }

    private void SendAsLines(string output)
    {
        if (_lineChunk is not null)
        {
            output = _lineChunk + output;
            _lineChunk = null;
        }

        var outputLines = NewLineRegex.Split(output);
        var lineCount = outputLines.Length;
        if (string.IsNullOrEmpty(outputLines[^1]))
        {
            lineCount--;
        }

        for (var i = 0; i < lineCount; i++)
        {
            var outputLine = outputLines[i];
            var isTheLastLine = i == lineCount - 1;
            if (isTheLastLine)
            {
                var isLineCompleted = outputLine.Length > 0 &&
                                      (outputLine[^1] == '\n' ||
                                       outputLine[^1] == '\r');
                if (!isLineCompleted)
                {
                    _lineChunk = outputLine;
                    break;
                }
            }

            _fireDataReceived(new ConsoleOutputEventArgs(outputLine));
        }
    }

    public void Flush()
    {
        if (_lineChunk is not null)
        {
            _fireDataReceived(new ConsoleOutputEventArgs(_lineChunk));
            _lineChunk = null;
        }
    }
}
