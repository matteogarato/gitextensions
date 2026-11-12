using GitExtUtils.GitUI;
using Microsoft.VisualStudio.Threading;

// This diagnostic is unnecessarily noisy when testing async patterns

namespace GitUITests;

public class ControlThreadingExtensionsTests
{
    [StaFact]
    public async Task ControlSwitchToMainThreadOnMainThread()
    {
        using Form form = new();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        await form.SwitchToMainThreadAsync();
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
    }

    [StaFact]
    public async Task ControlSwitchToMainThreadOnMainThread_with_cancellation_token()
    {
        using Form form = new();

        using CancellationTokenSource cancellationTokenSource = new();
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        await form.SwitchToMainThreadAsync(cancellationTokenSource.Token);
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
    }

    [StaFact]
    public void ControlSwitchToMainThreadOnMainThreadCompletesSynchronously()
    {
        using Form form = new();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();

        var awaiter = form.SwitchToMainThreadAsync().GetAwaiter();
        awaiter.IsCompleted.Should().BeTrue();
        awaiter.GetResult();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();

        using CancellationTokenSource cancellationTokenSource = new();
        awaiter = form.SwitchToMainThreadAsync(cancellationTokenSource.Token).GetAwaiter();
        awaiter.IsCompleted.Should().BeTrue();
        awaiter.GetResult();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
    }

    [StaFact]
    public async Task ControlSwitchToMainThreadOnBackgroundThread()
    {
        using Form form = new();

        await TaskScheduler.Default;
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeFalse();
        await form.SwitchToMainThreadAsync();
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
    }

    [StaFact]
    public async Task ControlSwitchToMainThreadOnBackgroundThread_with_cancellation_token()
    {
        using Form form = new();

        using CancellationTokenSource cancellationTokenSource = new();
        await TaskScheduler.Default;
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeFalse();
        await form.SwitchToMainThreadAsync(cancellationTokenSource.Token);
        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
    }

    [StaFact]
    public async Task ControlDisposedBeforeSwitchOnMainThread()
    {
        Form form = new();
        form.Dispose();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        var act = async () => await form.SwitchToMainThreadAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task TokenCancelledBeforeSwitchOnMainThread()
    {
        using Form form = new();
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        var act = async () => await form.SwitchToMainThreadAsync(cancellationTokenSource.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task ControlDisposedAndTokenCancelledBeforeSwitchOnMainThread()
    {
        Form form = new();
        form.Dispose();
        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        var act = async () => await form.SwitchToMainThreadAsync(cancellationTokenSource.Token);
        var exception = (await act.Should().ThrowAsync<OperationCanceledException>()).Which;

        // If both conditions are met on entry, the explicit cancellation token is the one used for the exception
        exception.CancellationToken.Should().Be(cancellationTokenSource.Token);
    }

    [StaFact]
    public async Task ControlDisposedAfterSwitchOnMainThread()
    {
        Form form = new();

        var awaitable = form.SwitchToMainThreadAsync();

        form.Dispose();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        var act = async () => await awaitable;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task TokenCancelledAfterSwitchOnMainThread()
    {
        using Form form = new();
        using CancellationTokenSource cancellationTokenSource = new();

        var awaitable =
            form.SwitchToMainThreadAsync(cancellationTokenSource.Token);

        await cancellationTokenSource.CancelAsync();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeTrue();
        var act = async () => await awaitable;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task ControlDisposedBeforeSwitchOnBackgroundThread()
    {
        Form form = new();
        form.Dispose();

        await TaskScheduler.Default;

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeFalse();
        var act = async () => await form.SwitchToMainThreadAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task TokenCancelledBeforeSwitchOnBackgroundThread()
    {
        using Form form = new();

        await TaskScheduler.Default;

        using CancellationTokenSource cancellationTokenSource = new();
        await cancellationTokenSource.CancelAsync();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeFalse();
        var act = async () => await form.SwitchToMainThreadAsync(cancellationTokenSource.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task ControlDisposedAfterSwitchOnBackgroundThread()
    {
        Form form = new();

        await TaskScheduler.Default;

        var awaitable = form.SwitchToMainThreadAsync();
        await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            form.Dispose();
        });

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeFalse();
        var act = async () => await awaitable;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [StaFact]
    public async Task TokenCancelledAfterSwitchOnBackgroundThread()
    {
        using Form form = new();

        await TaskScheduler.Default;

        using CancellationTokenSource cancellationTokenSource = new();

        var awaitable =
            form.SwitchToMainThreadAsync(cancellationTokenSource.Token);

        await cancellationTokenSource.CancelAsync();

        ThreadHelper.JoinableTaskContext.IsOnMainThread.Should().BeFalse();
        var act = async () => await awaitable;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
