using GitExtensions.Extensibility.Git;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls.RevisionGrid;

public class RevisionLoadEventArgs(
    IWin32Window? ownerForm,
    IGitUiCommands gitUiCommands,
    Lazy<IReadOnlyList<IGitRef>> getRefs,
    Lazy<IReadOnlyCollection<GitRevision>> getStashRevs,
    bool forceRefresh)
    : GitUiEventArgs(ownerForm, gitUiCommands, getRefs)
{
    public Lazy<IReadOnlyCollection<GitRevision>> GetStashRevs { get; init; } = getStashRevs;
    public bool ForceRefresh { get; init; } = forceRefresh;
}
