using System.Text;
using GitCommands.Git;
using GitCommands.Logging;
using GitExtensions.Extensibility;
using GitExtUtils.GitUI;
using Microsoft.VisualStudio.Threading;

namespace GitCommandsTests;

public sealed class ExecutableTests : IDisposable
{
    void IDisposable.Dispose()
    {
    }

    [Fact]
    public async Task Process_shall_be_killed_on_cancellation()
    {
        var cancelDelay = TimeSpan.FromSeconds(1);

        await TaskScheduler.Default;

        using CancellationTokenSource cts = new();

        // start a process running for seconds
        IExecutable executable = new Executable("ping.exe");
        var process =
            executable.Start($"-n {cancelDelay.TotalSeconds + 60} 127.0.0.1", cancellationToken: cts.Token);
        var startedAt = DateTime.Now;

        // cancel after delay
        await Task.Delay(cancelDelay, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // the process is killed on dispose but not before
        using (CancellationTokenSource ctsWaitExit = new())
        {
            try
            {
                ctsWaitExit.CancelAfter(cancelDelay);
                var exitCode = await process.WaitForExitAsync(ctsWaitExit.Token);
                Assert.Fail($"should not have exited, received: {exitCode}");
            }
            catch (OperationCanceledException)
            {
            }
        }

        process.Dispose();

        var durationWaitTimeout = DateTime.Now - startedAt;
        durationWaitTimeout.Should().BeGreaterThan(1.5 * cancelDelay).And.BeLessThan(4 * cancelDelay);

        var cmd = CommandLog.Commands.LastOrDefault();
        (cmd?.Exception?.Message).Should().Be("Process killed");
    }

    [Fact]
    public async Task WaitForProcessExitAsync_shall_return_latest_after_timeout()
    {
        var halfRuntime = TimeSpan.FromSeconds(3);

        await TaskScheduler.Default;

        // start a process running for seconds
        IExecutable executable = new Executable("ping.exe");
        using var process = executable.Start($"-n {(halfRuntime.TotalSeconds * 2) + 1} 127.0.0.1",
            cancellationToken: TestContext.Current.CancellationToken);

        // wait for process exit, but cancel the wait while the process is still running
        using CancellationTokenSource cts = new();
        var startedAt = DateTime.Now;
        cts.CancelAfter(halfRuntime);
        try
        {
            await process.WaitForExitAsync(cts.Token); // may or may not throw on cancel
        }
        catch (OperationCanceledException)
        {
            // ignore
        }

        var durationWaitTimeout = DateTime.Now - startedAt;
        durationWaitTimeout.Should().BeGreaterThan(0.5 * halfRuntime).And.BeLessThan(1.5 * halfRuntime);

        // wait for process exit without cancellation
        await process.WaitForExitAsync(CancellationToken.None);

        var durationExit = DateTime.Now - startedAt;
        durationExit.Should().BeGreaterThan(1.5 * halfRuntime).And.BeLessThan(2.5 * halfRuntime);
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("ping.exe")]
    public async Task ExecuteAsync_shall_return_latest_after_timeout(string exeFile)
    {
        const int cancelDelay = 1000;
        const int exitDelay = cancelDelay;
        const int minRuntime = cancelDelay + exitDelay;
        // cmd.exe with no arguments exits immediately when stdin is not a terminal (e.g., on CI runners).
        // Run a subcommand that blocks for the required duration instead.
        var arguments = exeFile.Contains("ping") ? $"-n {(minRuntime / 1000) + 2} 127.0.0.1"
            : exeFile.Contains("cmd") ? $"/c ping -n {(minRuntime / 1000) + 2} 127.0.0.1"
            : "";

        using CancellationTokenSource cancellationTokenSource = new();
        var cancellationToken = cancellationTokenSource.Token;
        IExecutable executable = new Executable(exeFile);

        Exception? exception = null;
        ExecutionResult? executionResult = null;

        async Task ExecuteAsync()
        {
            try
            {
                await TaskScheduler.Default.SwitchTo(true);
                executionResult = await executable.ExecuteAsync(arguments, outputEncoding: Encoding.Default,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }

        _ = ThreadHelper.JoinableTaskFactory.RunAsync(ExecuteAsync, JoinableTaskCreationOptions.LongRunning);

        await Task.Delay(cancelDelay, TestContext.Current.CancellationToken);
        await cancellationTokenSource.CancelAsync();
        await Task.Delay(exitDelay, TestContext.Current.CancellationToken);

        exception.Should().NotBeNull();
        exception.GetType().Should().Be<OperationCanceledException>();
        executionResult.Should().BeNull();
    }
}
