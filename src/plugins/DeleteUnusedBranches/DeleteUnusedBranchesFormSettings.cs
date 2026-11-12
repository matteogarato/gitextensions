namespace GitExtensions.Plugins.DeleteUnusedBranches;

public class DeleteUnusedBranchesFormSettings(
    int daysOlderThan,
    string mergedInBranch,
    bool removeDeleteRemoteBranchesFromFlag,
    string remoteName,
    bool userRegexToFilterBranchesFlag,
    string regexFilter,
    bool regexCaseInsensitiveFlag,
    bool regexInvertedFlag,
    bool includeUnmergedBranchesFlag)
{
    public string MergedInBranch { get; } = mergedInBranch;
    public int DaysOlderThan { get; } = daysOlderThan;
    public bool DeleteRemoteBranchesFromFlag { get; } = removeDeleteRemoteBranchesFromFlag;
    public string RemoteName { get; } = remoteName;
    public bool UseRegexToFilterBranchesFlag { get; } = userRegexToFilterBranchesFlag;
    public string RegexFilter { get; } = regexFilter;
    public bool RegexCaseInsensitiveFlag { get; } = regexCaseInsensitiveFlag;
    public bool RegexInvertedFlag { get; } = regexInvertedFlag;
    public bool IncludeUnmergedBranchesFlag { get; } = includeUnmergedBranchesFlag;
}
