namespace GitExtensions.Extensibility.Git;

public class GitUiPostActionEventArgs(IWin32Window? ownerForm, IGitUiCommands gitUiCommands, bool actionDone)
    : GitUiEventArgs(ownerForm, gitUiCommands)
{
    public bool ActionDone { get; } = actionDone;
}
