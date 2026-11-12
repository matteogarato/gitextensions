namespace GitUI.UserControls.RevisionGrid;

public class FilterChangedEventArgs(FilterInfo filter) : EventArgs
{
    public bool ShowAllBranches { get; } = filter.IsShowAllBranchesChecked;
    public bool ShowCurrentBranchOnly { get; } = filter.IsShowCurrentBranchOnlyChecked;
    public bool ShowFilteredBranches { get; } = filter.IsShowFilteredBranchesChecked;
    public bool ShowOnlyFirstParent { get; } = filter.ShowOnlyFirstParent;
    public bool ShowReflogReferences { get; } = filter.ShowReflogReferences;
    public bool HasFilter { get; } = filter.HasFilter;

    /// <summary>
    ///     Gets the currently applied Path filter.
    /// </summary>
    public string PathFilter { get; } = filter.PathFilter;

    /// <summary>
    ///     Gets the currently applied Branch filter.
    /// </summary>
    public string BranchFilter { get; } = filter.BranchFilter;

    /// <summary>
    ///     Gets the currently applied Message filter.
    /// </summary>
    public string MessageFilter { get; } = filter.Message;

    /// <summary>
    ///     Gets the currently applied Committer filter.
    /// </summary>
    public string CommitterFilter { get; } = filter.Committer;

    /// <summary>
    ///     Gets the currently applied Author filter.
    /// </summary>
    public string AuthorFilter { get; } = filter.Author;

    /// <summary>
    ///     Gets the currently applied Diff contains filter.
    /// </summary>
    public string DiffContentFilter { get; } = filter.DiffContent;

    /// <summary>
    ///     Gets a summary of the current filter.
    /// </summary>
    public string FilterSummary { get; } = filter.GetSummary();
}
