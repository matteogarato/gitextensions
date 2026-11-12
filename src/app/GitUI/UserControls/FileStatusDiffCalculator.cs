using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitUI.Properties;

namespace GitUI.UserControls;

public sealed partial class FileStatusDiffCalculator(Func<IGitModule> getModule)
{
    internal const string GitGrepIconName = nameof(GitGrepIconName);
    private const string _grepSummaryPrefix = "grep: ";

    // Currently bound revisions etc. Cache so we can reload the view, if AppSettings.ShowDiffForAllParents is changed.
    private FileStatusDiffCalculatorInfo _fileStatusDiffCalculatorInfo;

    // Default helper functions, can be set
    public Func<ObjectId, string>? DescribeRevision { get; set; }
    public Func<GitRevision, GitRevision>? GetActualRevision { get; set; }

    public static bool IsGrepItemStatuses(FileStatusWithDescription itemStatuses)
    {
        return itemStatuses.Summary.StartsWith(_grepSummaryPrefix);
    }

    public void SetDiff(
        IReadOnlyList<GitRevision> revisions,
        ObjectId headId,
        bool allowMultiDiff,
        bool showSkipWorktreeFiles = false,
        bool showUntrackedFiles = true)
    {
        _fileStatusDiffCalculatorInfo.Revisions = revisions;
        _fileStatusDiffCalculatorInfo.HeadId = headId;
        _fileStatusDiffCalculatorInfo.AllowMultiDiff = allowMultiDiff;
        _fileStatusDiffCalculatorInfo.ShowSkipWorktreeFiles = showSkipWorktreeFiles;
        _fileStatusDiffCalculatorInfo.UntrackedFilesMode =
            showUntrackedFiles ? UntrackedFilesMode.Default : UntrackedFilesMode.No;
    }

    public void SetGrep(string grepArguments, bool fileTreeMode)
    {
        _fileStatusDiffCalculatorInfo.GrepArguments = grepArguments;
        _fileStatusDiffCalculatorInfo.FileTreeMode = fileTreeMode;
    }

    public IReadOnlyList<FileStatusWithDescription> Calculate(IReadOnlyList<FileStatusWithDescription> prevList,
        bool refreshDiff, bool refreshGrep, CancellationToken cancellationToken)
    {
        if (_fileStatusDiffCalculatorInfo.Revisions?.Count is not > 0
            || _fileStatusDiffCalculatorInfo.Revisions[0] is not { } selectedRev)
        {
            return [];
        }

        var fileStatusDescs = refreshDiff
            ? CalculateDiffs(_fileStatusDiffCalculatorInfo.Revisions,
                selectedRev,
                _fileStatusDiffCalculatorInfo.HeadId,
                _fileStatusDiffCalculatorInfo.AllowMultiDiff,
                _fileStatusDiffCalculatorInfo.ShowSkipWorktreeFiles,
                _fileStatusDiffCalculatorInfo.UntrackedFilesMode,
                cancellationToken)
            : [.. prevList.Where(p => !IsGrepItemStatuses(p))];

        var grepItemStatuses = refreshGrep
            ? GetGrepItemStatuses(selectedRev, cancellationToken)
            : prevList.FirstOrDefault(IsGrepItemStatuses);

        if (grepItemStatuses is not null)
        {
            fileStatusDescs.Add(grepItemStatuses);
        }

        return fileStatusDescs;
    }

    private List<FileStatusWithDescription> CalculateDiffs(
        IReadOnlyList<GitRevision> revisions,
        GitRevision selectedRev,
        ObjectId headId,
        bool allowMultiDiff,
        bool showSkipWorktreeFiles,
        UntrackedFilesMode untrackedFilesMode,
        CancellationToken cancellationToken)
    {
        var module = GetModule();
        List<FileStatusWithDescription> fileStatusDescs = [];
        if (revisions.Count == 1)
        {
            // If the grid is filtered, parents may be rewritten
            var actualRev = GetActualRevisionForRevision(selectedRev);

            if (actualRev.ParentIds?.Count is > 0)
            {
                // Get the parents for the selected revision
                // Exclude the optional third group with the diff to the orphan commit containing the untracked files of a stash
                var multipleParents = AppSettings.ShowDiffForAllParents ? actualRev.ParentIds.Count : 1;
                fileStatusDescs.AddRange(actualRev
                    .ParentIds
                    .Take(multipleParents)
                    .Where(parentId =>
                        !(multipleParents == 3 &&
                          DescribeRevision?.Invoke(parentId).Contains(": untracked files on ") is true))
                    .Select(parentId =>
                        new FileStatusWithDescription(
                            new GitRevision(parentId),
                            selectedRev,
                            TranslatedStrings.DiffWithParent + GetDescriptionForRevision(parentId),
                            module.GetDiffFilesWithSubmodulesStatus(parentId, selectedRev.ObjectId,
                                actualRev.ParentIds[0], !showSkipWorktreeFiles, untrackedFilesMode,
                                cancellationToken))));
            }
            else
            {
                fileStatusDescs.Add(new FileStatusWithDescription(
                    null,
                    selectedRev,
                    GetDescriptionForRevision(selectedRev.ObjectId),
                    selectedRev.TreeId.IsZero

                        // likely index commit without HEAD
                        ? module.GetDiffFilesWithSubmodulesStatus(default, selectedRev.ObjectId, default,
                            cancellationToken: cancellationToken)

                        // No parent for the initial commit, show files and explicitly set IsNew
                        : module.GetTreeFiles(selectedRev.TreeId, true, cancellationToken)
                            .Select(i =>
                            {
                                i.IsNew = true;
                                return i;
                            }).ToList()));
            }

            // Show combined (merge conflicts) when a single merge commit is selected
            var isMergeCommit = (selectedRev.ParentIds?.Count ?? 0) > 1;
            if (isMergeCommit && AppSettings.ShowDiffForAllParents)
            {
                var conflicts = module.GetCombinedDiffFileList(selectedRev.ObjectId);
                if (conflicts.Count != 0)
                {
                    // Create an artificial commit
                    fileStatusDescs.Add(new FileStatusWithDescription(
                        new GitRevision(ObjectId.CombinedDiffId), selectedRev, TranslatedStrings.CombinedDiff,
                        conflicts, iconName: nameof(Images.DiffC)));
                }
            }

            return fileStatusDescs;
        }

        // With more than 4, only first -> selected is interesting
        // Show multi compare if 2-4 are selected
        const int maxMultiCompare = 4;

        // With 4 selected, assume that ranges are selected: baseA..headA baseB..headB
        // the first item is therefore the second selected
        var firstRev = AppSettings.ShowDiffForAllParents && revisions.Count == maxMultiCompare
            ? revisions[2]
            : revisions[^1];

        fileStatusDescs.Add(new FileStatusWithDescription(
            firstRev,
            selectedRev,
            TranslatedStrings.DiffWithParent + GetDescriptionForRevision(firstRev.ObjectId),
            module.GetDiffFilesWithSubmodulesStatus(firstRev.ObjectId, selectedRev.ObjectId, selectedRev.FirstParentId,
                cancellationToken: cancellationToken)));

        if (!AppSettings.ShowDiffForAllParents || revisions.Count > maxMultiCompare || !allowMultiDiff)
        {
            return fileStatusDescs;
        }

        // Get merge base commit, use HEAD for artificial
        var firstRevHead = GetRevisionOrHead(firstRev, headId);
        var selectedRevHead = GetRevisionOrHead(selectedRev, headId);
        ObjectId baseRevId = default;
        if (revisions.Count != 3)
        {
            baseRevId = GetMergeBase(firstRevHead, selectedRevHead);
        }
        else
        {
            // Check whether the middle commit is base to the first/second
            // Allow commits earlier to the actual base commit
            var middleId = revisions[1].ObjectId;
            if (!middleId.IsArtificial
                && GetMergeBase(firstRevHead, middleId) == middleId
                && GetMergeBase(selectedRevHead, middleId) == middleId)
            {
                baseRevId = middleId;
            }
        }

        // If four selected: check if two ranges are selected
        ObjectId baseA = default;
        ObjectId baseB = default;

        // Check for separate branches (note that artificial commits both have HEAD as BASE)
        if (!baseRevId.IsZero)
        {
            // Two/Three: Check that the selections are in separate branches
            if (revisions.Count < 4)
            {
                if (baseRevId == firstRevHead || baseRevId == selectedRevHead)
                {
                    baseRevId = default;
                }
            }

            // Four: Two ranges must be selected
            else
            {
                baseA = GetMergeBase(GetRevisionOrHead(revisions[3], headId), firstRevHead);
                if (baseA == revisions[3].ObjectId)
                {
                    baseB = GetMergeBase(GetRevisionOrHead(revisions[1], headId), selectedRevHead);
                    if (baseB != revisions[1].ObjectId)
                    {
                        baseB = default;
                    }
                }

                if (baseB.IsZero)
                {
                    // baseA/baseB were not ranges, this is no merge base
                    baseRevId = default;
                    baseA = default;
                }
            }
        }

        if (baseRevId.IsZero)
        {
            // No variant of range diff, show multi diff
            fileStatusDescs.AddRange(
                revisions
                    .Where(rev => rev != firstRev && rev != selectedRev)
                    .Select(rev => new FileStatusWithDescription(
                        rev,
                        selectedRev,
                        TranslatedStrings.DiffWithParent + GetDescriptionForRevision(rev.ObjectId),
                        module.GetDiffFilesWithSubmodulesStatus(rev.ObjectId, selectedRev.ObjectId,
                            selectedRev.FirstParentId, cancellationToken: cancellationToken))));

            return fileStatusDescs;
        }

        var allAToB = fileStatusDescs[0].Statuses;
        var allBaseToB = module.GetDiffFilesWithSubmodulesStatus(baseRevId,
            selectedRev.ObjectId, selectedRev.FirstParentId, cancellationToken: cancellationToken);
        var allBaseToA = module.GetDiffFilesWithSubmodulesStatus(baseRevId, firstRev.ObjectId,
            firstRev.FirstParentId, cancellationToken: cancellationToken);

        GitItemStatusNameEqualityComparer comparer = new();
        GitItemStatus[] allAToBExceptExactRenameCopy =
            [.. allAToB.Where(i => !((i.IsRenamed || i.IsCopied) && i.RenameCopyPercentage == "100"))];
        GitItemStatus[] sameBaseToAandB =
            [.. allBaseToB.Intersect(allBaseToA, comparer).Except(allAToBExceptExactRenameCopy, comparer)];
        GitItemStatus[] onlyA = [.. allBaseToA.Except(allBaseToB, comparer)];
        GitItemStatus[] onlyB = [.. allBaseToB.Except(allBaseToA, comparer)];

        foreach (var l in new[] { allAToB, allBaseToB, allBaseToA })
        {
            foreach (var f in l)
            {
                f.DiffStatus = GetDiffStatus(f, l == allAToB);
            }
        }

        DiffBranchStatus GetDiffStatus(GitItemStatus f, bool atoBDiff)
        {
            // Always show where the change is done
            // This means that if a file is added in A it is shown as removed in the A->B diff,
            // but marked with A
            return sameBaseToAandB.Any(i => comparer.Equals(i, f)) ? DiffBranchStatus.SameChange
                : onlyA.Any(i => comparer.Equals(i, f)) ? DiffBranchStatus.OnlyAChange
                : onlyB.Any(i => comparer.Equals(i, f)) ? DiffBranchStatus.OnlyBChange
                : DiffBranchStatus.UnequalChange;
        }

        GitRevision revBase = new(baseRevId);
        fileStatusDescs.Add(new FileStatusWithDescription(
            revBase,
            selectedRev,
            $"{TranslatedStrings.DiffBaseWith} B {GetDescriptionForRevision(selectedRev.ObjectId)}",
            allBaseToB,
            iconName: nameof(Images.DiffB)));
        fileStatusDescs.Add(new FileStatusWithDescription(
            revBase,
            firstRev,
            $"{TranslatedStrings.DiffBaseWith} A {GetDescriptionForRevision(firstRev.ObjectId)}",
            allBaseToA,
            iconName: nameof(Images.DiffA)));

        if (!module.GitVersion.SupportRangeDiffTool)
        {
            return fileStatusDescs;
        }

        // Add rangeDiff as a FileStatus item (even with artificial commits)
        var first = firstRev.ObjectId == firstRevHead ? firstRev : new GitRevision(firstRevHead);
        var selected = selectedRev.ObjectId == selectedRevHead ? selectedRev : new GitRevision(selectedRevHead);
        (var baseToFirstCount, var baseToSecondCount) =
            module.GetCommitRangeDiffCount(first.ObjectId, selected.ObjectId);

        // first and selected has a common merge base and count must be available
        // Only a printout, so no Validates
        var desc =
            $"{TranslatedStrings.DiffRange} {baseToFirstCount ?? -1}↓ {baseToSecondCount ?? -1}↑ BASE {GetDescriptionForRevision(baseRevId)}";
        fileStatusDescs.Insert(1, new FileStatusWithDescription(
            firstRev,
            selectedRev,
            desc,
            [new GitItemStatus(desc) { IsRangeDiff = true }],
            iconName: nameof(Images.DiffR)));

        return fileStatusDescs;

        ObjectId GetMergeBase(ObjectId a, ObjectId b)
        {
            if (a.IsZero || b.IsZero || a == b)
            {
                return default;
            }

            return module.GetMergeBase(a, b);
        }

        static ObjectId GetRevisionOrHead(GitRevision rev, ObjectId headId)
        {
            return rev.IsArtificial ? headId : rev.ObjectId;
        }

        GitRevision GetActualRevisionForRevision(GitRevision revision)
        {
            return GetActualRevision is not null ? GetActualRevision(revision) : revision;
        }
    }

    private FileStatusWithDescription? GetGrepItemStatuses(GitRevision selectedRev, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_fileStatusDiffCalculatorInfo.GrepArguments) &&
            !_fileStatusDiffCalculatorInfo.FileTreeMode)
        {
            return null;
        }

        var module = GetModule();
        var statuses = string.IsNullOrEmpty(_fileStatusDiffCalculatorInfo.GrepArguments)
            ? module.GetTreeFiles(selectedRev.ObjectId, true, cancellationToken)
            : module.GetGrepFilesStatus(selectedRev.ObjectId, _fileStatusDiffCalculatorInfo.GrepArguments, true,
                cancellationToken);

        return new FileStatusWithDescription(
            null,
            selectedRev,
            $"{_grepSummaryPrefix}{_fileStatusDiffCalculatorInfo.GrepArguments} {GetDescriptionForRevision(selectedRev.ObjectId)}",
            statuses,
            iconName: GitGrepIconName);
    }

    private string GetDescriptionForRevision(ObjectId objectId)
    {
        return DescribeRevision is not null ? DescribeRevision(objectId) : objectId.ToShortString();
    }

    private IGitModule GetModule()
    {
        var module =
            getModule() ?? throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");

        if (!module.IsValidGitWorkingDir())
        {
            // The repository may have been closed or switched while this calculation was queued: the
            // module is resolved when the work runs, not when it was requested. Give up rather than
            // run git in a directory which is not a repository any more.
            throw new OperationCanceledException();
        }

        return module;
    }
}
