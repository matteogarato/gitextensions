namespace GitUI.UserControls.RevisionGrid;

/// <summary>
///     The history filters that go beyond the text search of the revision grid.
/// </summary>
public sealed record RevisionFilterOptions
{
    public DateTime? DateFrom { get; init; }

    public DateTime? DateTo { get; init; }

    public string PathFilter { get; init; } = string.Empty;

    public bool HideMergeCommits { get; init; }

    public bool ShowOnlyFirstParent { get; init; }

    public bool ShowCurrentBranchOnly { get; init; }

    public bool ShowReflogReferences { get; init; }

    public bool SimplifyByDecoration { get; init; }

    /// <summary>
    ///     Gets a value indicating whether any of the options restricts the history.
    /// </summary>
    public bool IsActive
    {
        get => this != new RevisionFilterOptions();
    }

    /// <summary>
    ///     Turns a user-entered path into the quoted pathspec passed after <c>--</c> to <c>git log</c>.
    /// </summary>
    public static string BuildPathSpec(string? path)
    {
        var trimmed = path?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? string.Empty : trimmed.QuoteIfNotQuotedAndNe();
    }
}
