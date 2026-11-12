using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls.RevisionGrid;

public sealed class DoubleClickRevisionEventArgs(GitRevision? revision) : EventArgs
{
    public GitRevision? Revision { get; } = revision;
}
