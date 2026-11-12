using GitExtensions.Extensibility.Git;

namespace GitUI.UserControls;

partial class FileStatusDiffCalculator
{
    private record struct FileStatusDiffCalculatorInfo(
        IReadOnlyList<GitRevision> Revisions,
        ObjectId HeadId,
        bool AllowMultiDiff,
        bool FileTreeMode,
        string GrepArguments,
        bool ShowSkipWorktreeFiles,
        UntrackedFilesMode UntrackedFilesMode);
}
