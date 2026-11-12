namespace GitExtensions.Extensibility.Git;

public sealed class GitModuleEventArgs(IGitModule gitModule) : EventArgs
{
    public IGitModule GitModule { get; } = gitModule;
}
