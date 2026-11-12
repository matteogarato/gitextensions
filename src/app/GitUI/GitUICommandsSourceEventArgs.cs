namespace GitUI;

public sealed class GitUiCommandsSourceEventArgs(IGitUiCommandsSource gitUiCommandsSource) : EventArgs
{
    public IGitUiCommandsSource GitUiCommandsSource { get; } = gitUiCommandsSource;
}
