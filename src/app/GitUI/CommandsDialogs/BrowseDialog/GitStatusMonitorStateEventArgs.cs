namespace GitUI.CommandsDialogs.BrowseDialog;

public class GitStatusMonitorStateEventArgs(GitStatusMonitorState state) : EventArgs
{
    public GitStatusMonitorState State { get; } = state;
}
