using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using GitExtensions.Extensibility;
using GitExtUtils.GitUI;
using Xunit.Sdk;

namespace CommonTestUtils;

public sealed class MockExecutable : IExecutable
{
    private readonly ConcurrentDictionary<string, int> _commandArgumentsSet = new();

    private readonly ConcurrentDictionary<string, ConcurrentStack<(string output, int? exitCode, string? error)>>
        _outputStackByArguments = new();

    private readonly List<MockProcess> _processes = [];
    private int _nextCommandId;

    string IExecutable.Command
    {
        get => "mock-git.exe";
    }

    string IExecutable.WorkingDir
    {
        get => ".";
    }

    string IExecutable.PrefixArguments
    {
        get => "";
    }

    IProcess IExecutable.Start(
        ArgumentString arguments,
        bool createWindow,
        bool redirectInput,
        bool redirectOutput,
        Encoding? outputEncoding,
        bool useShellExecute = false,
        bool throwOnErrorExit = true,
        CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"mock-git {arguments}");

        if (_outputStackByArguments.TryRemove(arguments,
                out var queue) &&
            queue.TryPop(out var item))
        {
            if (queue.IsEmpty)
            {
                _outputStackByArguments.TryRemove(arguments, out _);
            }

            MockProcess process = new(item.output, item.exitCode, item.error);

            _processes.Add(process);
            return process;
        }

        if (_commandArgumentsSet.TryRemove(arguments, out _))
        {
            MockProcess process = new();
            _processes.Add(process);
            return process;
        }

        throw new Exception("Unexpected arguments: " + arguments);
    }

    public IDisposable StageOutput(string arguments, string output, int? exitCode = 0, string? error = null)
    {
        var stack = _outputStackByArguments.GetOrAdd(
            arguments,
            args => new ConcurrentStack<(string output, int? exitCode, string? error)>());

        stack.Push((output, exitCode, error));

        return new DelegateDisposable(() =>
        {
            if (_outputStackByArguments.TryGetValue(arguments,
                    out var queue) &&
                queue.TryPeek(out var item) &&
                output == item.output && error == item.error)
            {
                throw new XunitException(
                    $"Staged output should have been consumed.\nArguments: {arguments}\nOutput: {output}\nError: {error}");
            }
        });
    }

    public IDisposable StageCommand(string arguments)
    {
        var id = Interlocked.Increment(ref _nextCommandId);
        _commandArgumentsSet[arguments] = id;

        return new DelegateDisposable(() =>
        {
            if (_commandArgumentsSet.TryGetValue(arguments, out var storedId) && storedId != id)
            {
                throw new XunitException($"Staged command should have been consumed.\nArguments: {arguments}");
            }
        });
    }

    public bool Exists()
    {
        return true;
    }

    public void Verify()
    {
        _outputStackByArguments.Should().BeEmpty("All staged output should have been consumed.");
        _commandArgumentsSet.Should().BeEmpty("All staged output should have been consumed.");

        foreach (var process in _processes)
        {
            process.Verify();
        }
    }

    private sealed class MockProcess : IProcess
    {
        private readonly int? _exitCode;

        public MockProcess(string? output, int? exitCode = 0, string? error = null)
        {
            StandardOutput = new StreamReader(new MemoryStream(Encoding.UTF8.GetBytes(output ?? "")));
            StandardError = error ?? "";
            StandardInput = new StreamWriter(new MemoryStream());
            _exitCode = exitCode;
        }

        public MockProcess()
        {
            StandardOutput = new StreamReader(new MemoryStream());
            StandardError = "";
            StandardInput = new StreamWriter(new MemoryStream());
            _exitCode = 0;
        }

        StreamWriter IProcess.StandardInput { get; }
        StreamReader IProcess.StandardOutput { get; }
        string IProcess.StandardError { get; }

        void IProcess.Kill(bool entireProcessTree)
        {
        }

        int IProcess.WaitForExit()
        {
            return ThreadHelper.JoinableTaskFactory.Run(WaitForExitAsync);
        }

        Task<int> IProcess.WaitForExitAsync()
        {
            if (_exitCode.HasValue)
            {
                return Task.FromResult(_exitCode.Value);
            }

            using CancellationTokenSource cts = new();
            var ct = cts.Token;
            ThreadHelper.JoinableTaskFactory.RunAsync(() => cts.CancelAsync());
            return Task.FromCanceled<int>(ct);
        }

        Task<int> IProcess.WaitForExitAsync(CancellationToken token)
        {
            return Task.FromResult(_exitCode ?? 0);
        }

        void IProcess.WaitForInputIdle()
        {
            // TODO implement if needed
        }

        void IDisposable.Dispose()
        {
            // TODO implement if needed
        }

        public void Verify()
        {
            // all output should have been read
            StandardOutput.BaseStream.Position.Should().Be(StandardOutput.BaseStream.Length);

            // Only verify if std input is not closed.
            // ExecutableExtensions.ExecuteAsync will close std input when writeInput action is specified
            if (StandardInput.BaseStream is not null && StandardInput.BaseStream.CanRead)
            {
                // no input should have been written (yet)
                StandardInput.BaseStream.Length.Should().Be(0);
            }
        }
    }

    private sealed class DelegateDisposable(Action disposeAction) : IDisposable
    {
        void IDisposable.Dispose()
        {
            disposeAction();
        }
    }
}
