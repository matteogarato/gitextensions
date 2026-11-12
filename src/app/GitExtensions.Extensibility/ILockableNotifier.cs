namespace GitExtensions.Extensibility;

public interface ILockableNotifier
{
    /// <summary>
    ///     true if raising notification is locked
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    ///     notifies if is unlocked
    /// </summary>
    void Notify();

    /// <summary>
    ///     locks raising notification
    /// </summary>
    void Lock();

    /// <summary>
    ///     unlocks raising notification
    ///     to unlock raising notification, UnLock has to be called as many times as Lock was called
    /// </summary>
    /// <param name="requestNotify">true if Notify has to be called</param>
    void UnLock(bool requestNotify);
}
