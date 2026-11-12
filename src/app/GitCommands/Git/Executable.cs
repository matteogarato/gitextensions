using System.Diagnostics;
using System.Text;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitExtUtils.GitUI;

namespace GitCommands.Git;

/// <summary>
///     Wraps an executable file and provides process creation with logging and environment setup.
/// </summary>
public sealed class Executable : IExecutable
{
    private readonly Func<string> _fileNameProvider;
    private readonly string _workingDir;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Executable" /> class with a fixed file name.
    /// </summary>
    /// <param name="fileName">The path to the executable file.</param>
    /// <param name="workingDir">The working directory in which the process will be started.</param>
    public Executable(string fileName, string workingDir = "")
        : this(() => fileName, workingDir)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Executable" /> class with a deferred file name resolution.
    /// </summary>
    /// <param name="fileNameProvider">A delegate that returns the path to the executable file.</param>
    /// <param name="workingDir">The working directory in which the process will be started.</param>
    /// <param name="prefixArguments">Arguments that are prepended to every invocation.</param>
    public Executable(Func<string> fileNameProvider, string workingDir = "", string prefixArguments = "")
    {
        _workingDir = workingDir;
        _fileNameProvider = fileNameProvider;
        PrefixArguments = prefixArguments;
    }

    string IExecutable.WorkingDir
    {
        get => _workingDir;
    }

    string IExecutable.Command
    {
        get => _fileNameProvider();
    }

    string IExecutable.PrefixArguments { get; }

    IProcess IExecutable.Start(ArgumentString arguments = default,
        bool createWindow = false,
        bool redirectInput = false,
        bool redirectOutput = false,
        Encoding? outputEncoding = null,
        bool useShellExecute = false,
        bool throwOnErrorExit = true,
        CancellationToken cancellationToken = default)
    {
        // TODO should we set these on the child process only?
        EnvironmentConfiguration.SetEnvironmentVariables();

        var args = (arguments.Arguments ?? "").Replace("$QUOTE$", "\\\"");

        var fileName = _fileNameProvider();

        return new ProcessWrapper(fileName, PrefixArguments, args, _workingDir, createWindow, redirectInput,
            redirectOutput, outputEncoding, useShellExecute, throwOnErrorExit, cancellationToken);
    }

    #region ProcessWrapper

    /// <summary>
    ///     Manages the lifetime of a process. The <see cref="Process" /> object has many members
    ///     that throw at different times in the lifecycle of the process, such as after it is disposed. This class
    ///     provides a simplified API that meets the need of this application via the <see cref="IProcess" /> interface.
    /// </summary>
    private sealed class ProcessWrapper : IProcess
    {
        private readonly CancellationToken _cancellationToken;
        private readonly Encoding? _errorEncoding;

        private readonly MemoryStream? _errorOutputStream;

        // TODO should this use TaskCreationOptions.RunContinuationsAsynchronously
        private readonly TaskCompletionSource<int> _exitTaskCompletionSource = new();
        private readonly Lock _lock = new();
        private readonly ProcessOperation _logOperation;
        private readonly Process _process;
        private readonly bool _redirectInput;
        private readonly bool _redirectOutput;
        private readonly bool _throwOnErrorExit;

        private bool _disposed;
        private string? _errorOutput;
        private bool _exitHandlerRemoved;

        public ProcessWrapper(string fileName,
            string prefixArguments,
            string arguments,
            string workDir,
            bool createWindow,
            bool redirectInput,
            bool redirectOutput,
            Encoding? outputEncoding,
            bool useShellExecute,
            bool throwOnErrorExit,
            CancellationToken cancellationToken)
        {
            DebugHelpers.Assert(redirectOutput == outputEncoding is not null,
                "redirectOutput == (outputEncoding is not null)");
            _redirectInput = redirectInput;
            _redirectOutput = redirectOutput;
            _throwOnErrorExit = throwOnErrorExit;
            _cancellationToken = cancellationToken;

            _errorEncoding = outputEncoding;
            if (throwOnErrorExit)
            {
                _errorEncoding ??= Encoding.Default;
            }

            if (throwOnErrorExit || redirectOutput)
            {
                _errorOutputStream = new MemoryStream();
            }

            _process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    UseShellExecute = useShellExecute,
                    Verb = useShellExecute ? "open" : string.Empty,
                    ErrorDialog = false,
                    CreateNoWindow = !createWindow,
                    RedirectStandardInput = redirectInput,
                    RedirectStandardOutput = redirectOutput,
                    RedirectStandardError = _errorOutputStream is not null,
                    StandardOutputEncoding = outputEncoding,
                    StandardErrorEncoding = _errorEncoding,
                    FileName = fileName,
                    Arguments = $"{prefixArguments}{arguments}",
                    WorkingDirectory = workDir
                }
            };

            _logOperation = CommandLog.LogProcessStart($"{fileName} {prefixArguments}".Trim(), arguments, workDir);

            _process.Exited += OnProcessExit;

            try
            {
                try
                {
                    _process.Start();
                }
                catch (Exception ex)
                {
                    _process.Exited -= OnProcessExit;
                    _exitHandlerRemoved = true;
                    _exitTaskCompletionSource.TrySetException(ex);
                    throw;
                }

                if (_errorOutputStream is not null)
                {
                    _process.StandardError.BaseStream.CopyToAsync(_errorOutputStream, cancellationToken);
                }

                try
                {
                    _logOperation.SetProcessId(_process.Id);
                }
                catch (InvalidOperationException ex) when (useShellExecute)
                {
                    // _process.Start() has succeeded, ignore the failure getting the _process.Id
                    _logOperation.LogProcessEnd(ex);
                }
            }
            catch (Exception ex)
            {
                Dispose();

                _logOperation.LogProcessEnd(ex);
                throw new ExternalOperationException($"{fileName} {prefixArguments}".Trim(), arguments, workDir,
                    innerException: ex);
            }
        }

        StreamWriter IProcess.StandardInput
        {
            get
            {
                if (!_redirectInput)
                {
                    throw new InvalidOperationException("Process was not created with redirected input.");
                }

                return _process.StandardInput;
            }
        }

        StreamReader IProcess.StandardOutput
        {
            get
            {
                if (!_redirectOutput)
                {
                    throw new InvalidOperationException("Process was not created with redirected output.");
                }

                return _process.StandardOutput;
            }
        }

        string IProcess.StandardError
        {
            get
            {
                if (_errorOutput is null)
                {
                    throw new InvalidOperationException("Process was not created with redirected output.");
                }

                return _errorOutput;
            }
        }

        void IProcess.Kill(bool entireProcessTree)
        {
            _process.Kill(entireProcessTree);
        }

        void IProcess.WaitForInputIdle()
        {
            _process.WaitForInputIdle();
        }

#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
        Task<int> IProcess.WaitForExitAsync()
        {
            return _exitTaskCompletionSource.Task;
        }
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

        Task<int> IProcess.WaitForExitAsync(CancellationToken token)
        {
            return WaitForExitAsync().WithCancellation(token);
        }

        int IProcess.WaitForExit()
        {
            return ThreadHelper.JoinableTaskFactory.Run(WaitForExitAsync);
        }

        void IDisposable.Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                if (!_exitHandlerRemoved)
                {
                    // HandleProcessExit directly, do not rely on event execution
                    _process.Exited -= OnProcessExit;
                    _exitHandlerRemoved = true;

                    try
                    {
                        if (_process.HasExited)
                        {
                            HandleProcessExit();
                        }
                        else if (_cancellationToken.IsCancellationRequested)
                        {
                            // Directly kill the process because Ctrl+C does not reach the git process how we start it
                            _process.Kill();

                            OperationCanceledException ex = new("Process killed");
                            _logOperation.LogProcessEnd(ex);
                            _exitTaskCompletionSource.TrySetException(ex);
                        }
                        else
                        {
                            _exitTaskCompletionSource.TrySetCanceled();
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex);
                    }
                }
            }

            _process.Dispose();

            _logOperation.NotifyDisposed();

            _errorOutputStream?.Dispose();
        }

        private void HandleProcessExit()
        {
            try
            {
                var exitCode = _process.ExitCode;
                var errorOutput = ReadErrorOutput();
                _logOperation.LogProcessEnd(exitCode, errorOutput);

                if (_throwOnErrorExit && exitCode != 0)
                {
                    var errorMessage = errorOutput?.Length is > 0
                        ? errorOutput
                        : "External program returned non-zero exit code.";
                    Exception ex
                        = new ExternalOperationException(_process.StartInfo.FileName,
                            _process.StartInfo.Arguments,
                            _process.StartInfo.WorkingDirectory,
                            exitCode,
                            new Exception(errorMessage));
                    if (exitCode == NativeMethods.StatusControlCExit)
                    {
                        ex = new OperationCanceledException("Ctrl+C pressed or console closed", ex);
                    }

                    _exitTaskCompletionSource.TrySetException(ex);
                }

                _exitTaskCompletionSource.TrySetResult(exitCode);
            }
            catch (Exception ex)
            {
                _logOperation.LogProcessEnd(ex);
                _exitTaskCompletionSource.TrySetException(ex);
            }

            return;

            string? ReadErrorOutput()
            {
                if (_errorOutputStream is null || _errorEncoding is null)
                {
                    return null;
                }

                try
                {
                    _errorOutput = _errorEncoding.GetString(_errorOutputStream.GetBuffer(), 0,
                        (int)_errorOutputStream.Length);

                    return _errorOutput.Trim();
                }
                catch (Exception ex)
                {
                    return $"Failed to read: {ex}";
                }
            }
        }

        private void OnProcessExit(object? sender, EventArgs eventArgs)
        {
            lock (_lock)
            {
                // The Exited event can be raised after the process is disposed, however
                // if the Process is disposed then reading ExitCode will throw.
                if (!_disposed && !_exitHandlerRemoved)
                {
                    _process.Exited -= OnProcessExit;
                    _exitHandlerRemoved = true;
                    HandleProcessExit();
                }
            }
        }
    }

    #endregion
}
