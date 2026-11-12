using System.ComponentModel;

namespace GitExtensions.Extensibility.Git;

public class GitUiEventArgs : CancelEventArgs
{
    private readonly IFilteredGitRefsProvider _getRefs;

    public GitUiEventArgs(IWin32Window? ownerForm, IGitUiCommands gitUiCommands,
        Lazy<IReadOnlyList<IGitRef>>? getRefs = null)
        : base(false)
    {
        OwnerForm = ownerForm;
        GitUiCommands = gitUiCommands;
        if (getRefs is null)
        {
            _getRefs = new FilteredGitRefsProvider(GitModule);
        }
        else
        {
            _getRefs = new FilteredGitRefsProvider(getRefs);
        }
    }

    public IGitUiCommands GitUiCommands { get; }

    public IWin32Window? OwnerForm { get; }

    public IGitModule GitModule
    {
        get => GitUiCommands.Module;
    }

    public IReadOnlyList<IGitRef> GetRefs(RefsFilter filter)
    {
        return _getRefs.GetRefs(filter);
    }
}
