using Microsoft.VisualStudio.Threading;

namespace GitExtUtils.GitUI;

public class ExclusiveTaskRunner(TaskManager taskManager) : IDisposable
{
    private readonly CancellationTokenSequence _cancellationTokenSequence = new();
    private readonly Lock _cancellationTokenSequenceLock = new();
    private JoinableTask? _runningTask;

    void IDisposable.Dispose()
    {
        _cancellationTokenSequence.Dispose();
        _runningTask = null;
        GC.SuppressFinalize(this);
    }

    public void CancelCurrent()
    {
        _cancellationTokenSequence.CancelCurrent();
    }

    /// <summary>
    ///     Exclusivly run <paramref name="asyncAction" /> asynchronously on the current thread and forward all exceptions to
    ///     <see cref="Application.OnThreadException" /> except for <see cref="OperationCanceledException" />, which is
    ///     ignored.
    ///     A previous task is cancelled and its termination is awaited before.
    /// </summary>
    public JoinableTask RunDetached(Func<CancellationToken, Task> asyncAction)
    {
        lock (_cancellationTokenSequenceLock)
        {
            var cancellationToken = _cancellationTokenSequence.Next();
            var previousTask = _runningTask;
            _runningTask = taskManager.JoinableTaskFactory.RunAsync(async () =>
            {
                // Absolutely await the completion or the cancellation of the previous run, i.e. without cancellation
                if (previousTask is not null)
                {
                    await previousTask.JoinAsync();
                }

                await TaskManager.HandleExceptionsAsync(
                    () => asyncAction(cancellationToken).WithCancellation(cancellationToken),
                    taskManager.ReportExceptionOnMainThreadAsync);
            });
            return _runningTask;
        }
    }
}
