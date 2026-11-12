using GitExtensions.Extensibility;

namespace GitCommands;

public abstract class LockableNotifier : ILockableNotifier
{
    private int _lockCount;
    private bool _notifyRequested;

    /// <summary>
    ///     notifies if is unlocked
    /// </summary>
    void ILockableNotifier.Notify()
    {
        _notifyRequested = true;
        CheckNotify(_lockCount);
    }

    /// <summary>
    ///     locks raising notification
    /// </summary>
    void ILockableNotifier.Lock()
    {
        Interlocked.Increment(ref _lockCount);
    }

    /// <summary>
    ///     unlocks raising notification
    ///     to unlock raising notification, UnLock has to be called as many times as Lock was called
    /// </summary>
    /// <param name="requestNotify">true if Notify has to be called</param>
    void ILockableNotifier.UnLock(bool requestNotify)
    {
        var newCount = Interlocked.Decrement(ref _lockCount);

        if (newCount < 0)
        {
            throw new InvalidOperationException("There was no counterpart call to Lock");
        }

        if (requestNotify)
        {
            _notifyRequested = true;
        }

        CheckNotify(newCount);
    }

    /// <summary>
    ///     true if raising notification is locked
    /// </summary>
    bool ILockableNotifier.IsLocked
    {
        get => _lockCount != 0;
    }

    protected abstract void InternalNotify();

    private void CheckNotify(int lockCount)
    {
        if (lockCount == 0 && _notifyRequested)
        {
            _notifyRequested = false;
            InternalNotify();
        }
    }
}

public class ActionNotifier(Action notifyAction) : LockableNotifier
{
    private readonly Action _notifyAction = notifyAction ?? throw new ArgumentNullException(nameof(notifyAction));

    protected override void InternalNotify()
    {
        _notifyAction();
    }
}
