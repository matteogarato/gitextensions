using GitExtUtils.GitUI;

namespace GitUI.UserControls.RevisionGrid;

public sealed partial class RevisionDataGridView
{
    /// <summary>
    ///     Coordinates background update executions. Requested reruns only "queue up" upto one.
    /// </summary>
    private sealed class BackgroundUpdater(TaskManager taskManager, Func<Task> operation, int cooldownMilliseconds)
    {
        private readonly Lock _lock = new();
        private readonly Func<Task> _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        private readonly TaskManager _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));

        private volatile bool _executing;
        private volatile bool _rerunRequested;

        public void ScheduleExecution()
        {
            lock (_lock)
            {
                if (!_executing)
                {
                    // if not running, start it
                    _executing = true;
                    _taskManager.FileAndForget(WrappedOperationAsync);
                }
                else
                {
                    // if currently running make sure it runs again
                    _rerunRequested = true;
                }
            }
        }

        private async Task WrappedOperationAsync()
        {
            try
            {
                await _operation();
            }
            finally
            {
                await Task.Delay(cooldownMilliseconds);

                lock (_lock)
                {
                    if (_rerunRequested)
                    {
                        _taskManager.FileAndForget(WrappedOperationAsync);
                        _rerunRequested = false;
                    }
                    else
                    {
                        _executing = false;
                    }
                }
            }
        }
    }
}
