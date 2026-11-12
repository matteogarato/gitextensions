namespace GitExtensions.Extensibility.Git;

// TODO: This should not be in Extensibility!

public sealed class FilteredGitRefsProvider : IFilteredGitRefsProvider
{
    private readonly Lazy<IReadOnlyList<IGitRef>> _getRefs;

    public FilteredGitRefsProvider(IGitModule module)
    {
        _getRefs = new Lazy<IReadOnlyList<IGitRef>>(() => module.GetRefs(RefsFilter.NoFilter));
    }

    public FilteredGitRefsProvider(Lazy<IReadOnlyList<IGitRef>> getRefs)
    {
        _getRefs = getRefs;
    }

    /// <inherit />
    IReadOnlyList<IGitRef> IFilteredGitRefsProvider.GetRefs(RefsFilter filter)
    {
        if (filter == RefsFilter.NoFilter)
        {
            return _getRefs.Value;
        }

        return _getRefs.Value
            .Where(r =>
                ((filter & RefsFilter.Tags) != 0 && r.IsTag)
                || ((filter & RefsFilter.Remotes) != 0 && r.IsRemote)
                || ((filter & RefsFilter.Heads) != 0 && r.IsHead))
            .ToList();
    }
}
