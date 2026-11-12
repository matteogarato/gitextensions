using GitExtensions.Extensibility.Git;

namespace GitUI.UserControls;

partial class FileStatusList
{
    private sealed record GroupBy(
        Func<GitItemStatus, GroupKey> GetGroupKey,
        Func<IGrouping<GroupKey, GitItemStatus>, string> GetImageKey,
        Func<IGrouping<GroupKey, GitItemStatus>, string> GetLabel);
}
