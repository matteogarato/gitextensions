using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using GitCommands.Git.Extensions;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitExtUtils;
using GitUI.Compat;

namespace GitUI.ConsoleEmulation.PlainText;

// Twin of GitUI/ConsoleEmulation/PlainText/PlainTextConsoleCommandRunner.cs: the RichTextBox
// becomes a read-only monospace TextBox (clickable links arrive later), the WinForms timer a
// DispatcherTimer. The process handling is identical.

/// <summary>
///     Displays redirected process output in an edit box when no embedded terminal is being used.
/// </summary>
public sealed class PlainTextConsoleCommandRunner : UserControl, IPlainTextConsoleCommandRunner, IDisposable
{
    private readonly TextBox _editbox;

    private StreamWriter? _input;

    private bool _isDisposed;

    private Action? _logProcessKilled;

    private ProcessOutputThrottle? _outputThrottle;

    private Process? _process;

    public PlainTextConsoleCommandRunner()
    {
        _editbox = new TextBox
        {
            FontFamily = new FontFamily("monospace"),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap
        };
        Content = _editbox;

        _outputThrottle = new ProcessOutputThrottle(AppendMessage);

        void AppendMessage(string text)
        {
            DebugHelpers.Assert(text is not null, "text is not null");
            if (_isDisposed)
            {
                return;
            }

            DebugHelpers.Assert(Dispatcher.UIThread.CheckAccess(), "!InvokeRequired");

            _editbox.IsVisible = true;
            _editbox.Text += text;
            _editbox.CaretIndex = _editbox.Text!.Length;
        }
    }

    void IDisposable.Dispose()
    {
        KillCommandProcess();
        _isDisposed = true;
        _outputThrottle?.Dispose();
        _outputThrottle = null;
        _process?.Dispose();
        _process = null;
        _input?.Dispose();
        _input = null;
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

    void IPlainTextConsoleCommandRunner.WriteOutputText(string text)
    {
        _outputThrottle?.Append(text);
    }

    void IConsoleCommandRunner.WriteCommandProcessInput(string text)
    {
        Validates.NotNull(_input);
        _input.Write(text);
    }

    void IConsoleCommandRunner.KillCommandProcess()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw new InvalidOperationException("This operation is to be executed on the home thread.");
        }

        if (_process is null)
        {
            return;
        }

        _logProcessKilled?.Invoke();

        try
        {
            _process.TerminateTree();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex);
        }

        _process.Dispose();
        _process = null;
        _input?.Dispose();
        _input = null;
        CommandProcessExited?.Invoke(this, new ConsoleProcessExitEventArgs(-1));
    }

    void IConsoleCommandRunner.ResetConsole()
    {
        KillCommandProcess();
        _outputThrottle?.Clear();
        _editbox.Text = "";
        _editbox.IsVisible = false;
    }

    void IConsoleCommandRunner.StartCommand(string command, string arguments, string workDir,
        Dictionary<string, string> envVariables)
    {
        var operation = CommandLog.LogProcessStart(command, arguments, workDir);

        WriteOutputText($"{command.Quote()} {arguments}{Environment.NewLine}");

        try
        {
            EnvironmentConfiguration.SetEnvironmentVariables();

            KillCommandProcess();

            _logProcessKilled = () => operation.LogProcessEnd(new Exception("Process killed"));

            // process used to execute external commands
            var outputEncoding = GitModule.SystemEncoding;
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = false,
                ErrorDialog = false,
                CreateNoWindow = !AppSettings.ShowGitCommandLine,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = outputEncoding,
                StandardErrorEncoding = outputEncoding,
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workDir
            };

            foreach ((var name, var value) in envVariables)
            {
                startInfo.EnvironmentVariables.Add(name, value);
            }

            _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            AsyncStreamReader? outputReader = null;
            AsyncStreamReader? errorReader = null;

            _process.Exited += delegate
            {
                ThreadHelper.FileAndForget(async () =>
                {
                    if (_process is null)
                    {
                        await this.SwitchToMainThreadAsync();
                        operation.LogProcessEnd(new Exception("Process instance is null in Exited event"));
                        return;
                    }

                    // The process is exited already, but this command waits also until all output is received.
                    try
                    {
                        // WaitForExit[Async] blocks here for unknown reason if the process has already exited
                        if (!_process.HasExited)
                        {
                            await _process.WaitForExitAsync();
                        }

                        _logProcessKilled = null;

                        if (_process is null)
                        {
                            // The process has been killed meanwhile.
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        await this.SwitchToMainThreadAsync();
                        operation.LogProcessEnd(ex);
                    }

                    var exitCode = _process.ExitCode;

                    using CancellationTokenSource eofTimeoutTokenSource = new(5000);

                    if (outputReader is not null)
                    {
                        await outputReader.WaitUntilEofAsync(eofTimeoutTokenSource.Token);
                        outputReader.Dispose();
                    }

                    if (errorReader is not null)
                    {
                        await errorReader.WaitUntilEofAsync(eofTimeoutTokenSource.Token);
                        errorReader.Dispose();
                    }

                    await this.SwitchToMainThreadAsync();
                    operation.LogProcessEnd(exitCode);
                    WriteOutputText("Done");
                    _process.Dispose();
                    _process = null;
                    await _input!.DisposeAsync();
                    _input = null;
                    _outputThrottle?.Stop(true);
                    CommandProcessExited?.Invoke(this, new ConsoleProcessExitEventArgs(exitCode));
                });
            };

            _process.Start();
            operation.SetProcessId(_process.Id);
            _input = _process.StandardInput;
            outputReader = new AsyncStreamReader(_process.StandardOutput, ForwardOutput);
            errorReader = new AsyncStreamReader(_process.StandardError, ForwardOutput);
        }
        catch (Exception ex)
        {
            operation.LogProcessEnd(ex);
            ex.Data.Add("command", command);
            ex.Data.Add("arguments", arguments);
            throw;
        }

        return;

        void ForwardOutput(string output)
        {
            output = output.Replace("\r\n", "\n");

            for (var startIndex = 0; startIndex < output.Length;)
            {
                var nextLineStart = output.IndexOfAny(Delimiters.LineFeedAndCarriageReturnSearchValues, startIndex) + 1;
                if (nextLineStart == 0)
                {
                    nextLineStart = output.Length;
                }

                var outputLine = output[startIndex..nextLineStart];
                CommandOutputReceived?.Invoke(this, new ConsoleOutputEventArgs(outputLine));

                startIndex = nextLineStart;
            }
        }
    }

    private event EventHandler<ConsoleOutputEventArgs>? CommandOutputReceived;

    private event EventHandler<ConsoleProcessExitEventArgs>? CommandProcessExited;

    #region ProcessOutputThrottle

    private sealed class ProcessOutputThrottle : IDisposable
    {
        private readonly Action<string> _doOutput;
        private readonly StringBuilder _textToAdd = new();
        private readonly Lock _textToAddLock = new();
        private readonly DispatcherTimer _timer;

        /// <param name="doOutput">Will be called on the UI thread.</param>
        public ProcessOutputThrottle(Action<string> doOutput)
        {
            _doOutput = doOutput;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1) };
            _timer.Tick += delegate { FlushOutput(); };
            _timer.Start();
        }

        void IDisposable.Dispose()
        {
            Stop(false);

            // clear will lock, to prevent outputting to disposed object
            Clear();
        }

        public void Stop(bool flush)
        {
            if (flush)
            {
                FlushOutput();
            }

            _timer.Stop();
        }

        /// <remarks>Can be called on any thread.</remarks>
        public void Append(string text)
        {
            lock (_textToAddLock)
            {
                _textToAdd.Append(text);
            }
        }

        public void FlushOutput()
        {
            _timer.Stop();
            _timer.Interval = TimeSpan.FromMilliseconds(100);
            _timer.Start();

            var textToAdd = "";
            lock (_textToAddLock)
            {
                if (_textToAdd.Length > 0)
                {
                    textToAdd = _textToAdd.ToString();
                    _textToAdd.Clear();
                }
            }

            if (textToAdd.Length > 0)
            {
                _doOutput?.Invoke(textToAdd);
            }
        }

        public void Clear()
        {
            lock (_textToAddLock)
            {
                _textToAdd.Clear();
            }
        }
    }

    #endregion

    // Editbox-based output never terminates independently; event is required by the interface.
#pragma warning disable CS0067
    private event EventHandler? ConsoleHostTerminated;
    event EventHandler? IConsoleCommandRunner.ConsoleHostTerminated
    {
        add => ConsoleHostTerminated += value;
        remove => ConsoleHostTerminated -= value;
    }
#pragma warning restore CS0067
}
