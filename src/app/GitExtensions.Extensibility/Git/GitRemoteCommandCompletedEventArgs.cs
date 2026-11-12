namespace GitExtensions.Extensibility.Git;

public class GitRemoteCommandCompletedEventArgs(IGitRemoteCommand command, bool isError, bool handled) : EventArgs
{
    public IGitRemoteCommand Command { get; } = command;

    public bool IsError { get; } = isError;

    public bool Handled { get; } = handled;
}
