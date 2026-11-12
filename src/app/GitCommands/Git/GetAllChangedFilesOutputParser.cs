using System.Diagnostics;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;

namespace GitCommands.Git;

/// <summary>
///     Provides a parser for output of <see cref="Commands.GetAllChangedFiles" /> command.
/// </summary>
public class GetAllChangedFilesOutputParser(Func<IGitModule> getModule)
{
    private readonly Func<IGitModule> _getModule = getModule;

    /// <summary>
    ///     Parse the output from git-status --porcelain=2 -z
    ///     Note that the caller should check for fatal errors in the Git output.
    /// </summary>
    /// <param name="getAllChangedFilesCommandOutput">An output of <see cref="Commands.GetAllChangedFiles" /> command.</param>
    /// <returns>list with the parsed GitItemStatus.</returns>
    /// <seealso href="https://git-scm.com/docs/git-status" />
    public IReadOnlyList<GitItemStatus> Parse(string getAllChangedFilesCommandOutput)
    {
        return GetAllChangedFilesFromString_v2(getAllChangedFilesCommandOutput);
    }

    /// <summary>
    ///     Parse git-diff --raw
    ///     format is similar to "git-status --porcelain=1 -z" and "git diff --name-status", no longer supported
    /// </summary>
    /// <param name="getAllChangedFilesCommandOutput">An output of <see cref="Commands.GetAllChangedFiles" /> command.</param>
    /// <param name="staged">
    ///     The staged status <see cref="GitItemStatus" />, only relevant for git-diff (parsed for
    ///     git-status).
    /// </param>
    /// <returns>list with the git items.</returns>
    internal List<GitItemStatus> GetDiffChangedFilesFromString(string getAllChangedFilesCommandOutput,
        StagedStatus staged)
    {
        List<GitItemStatus> diffFiles = [];

        if (string.IsNullOrEmpty(getAllChangedFilesCommandOutput))
        {
            return diffFiles;
        }

        // Split all files on '\0' (WE NEED ALL COMMANDS TO BE RUN WITH -z! THIS IS ALSO IMPORTANT FOR ENCODING ISSUES!)
        var files = getAllChangedFilesCommandOutput.Split(Delimiters.Null, StringSplitOptions.RemoveEmptyEntries);
        for (var n = 0; n < files.Length; ++n)
        {
            if (string.IsNullOrEmpty(files[n]))
            {
                continue;
            }

            if (n >= files.Length - 1)
            {
                // no file name for last entry, ignore
                Trace.WriteLine($"No filename for last entry, ignoring: {files[n]}");
                continue;
            }

            var status = files[n].AsSpan();
            ++n;
            var fileName = files[n].AsSpan();

            if (status[0] != ':' || status.Length < 15)
            {
                Trace.WriteLine($"Illegal status line: {fileName}");
                continue;
            }

            var statusIndex = status.LastIndexOfAnyExceptInRange('0', '9');
            var x = status[statusIndex];

            if (staged == StagedStatus.WorkTree && x == GitItemStatusConverter.UnmergedStatus)
            {
                // git-diff has two lines to inform that a file is modified and has a merge conflict
                continue;
            }

            var gitItemStatusX = GitItemStatusConverter.FromStatusCharacter(staged, fileName.ToString(), x);
            if (x is GitItemStatusConverter.RenamedStatus or GitItemStatusConverter.CopiedStatus)
            {
                gitItemStatusX.RenameCopyPercentage = status[(statusIndex + 1)..].ToString();
                gitItemStatusX.OldName = gitItemStatusX.Name;

                if (n + 1 >= files.Length)
                {
                    Trace.WriteLine($"No next file for renamed entry: {status}::{gitItemStatusX.Name}");
                }
                else
                {
                    // Renamed file in an extra entry
                    // No check if the entry seem to be a file (if it starts with '.' it is likely next status)
                    ++n;
                    gitItemStatusX.Name = files[n];
                }
            }

            const string subMode = "160000";
            if (status.Slice(1, 6).SequenceEqual(subMode) || status.Slice(8, 6).SequenceEqual(subMode))
            {
                gitItemStatusX.IsSubmodule = true;
            }

            // ignore the sha in status (could be used to set TreeId)

            diffFiles.Add(gitItemStatusX);
        }

        return diffFiles;
    }

    /// <summary>
    ///     Parse the output from git-status --porcelain=2.
    /// </summary>
    /// <param name="getAllChangedFilesCommandOutput">output from the git command.</param>
    /// <returns>list with the parsed GitItemStatus.</returns>
    private static IReadOnlyList<GitItemStatus> GetAllChangedFilesFromString_v2(string getAllChangedFilesCommandOutput)
    {
        List<GitItemStatus> diffFiles = [];

        if (string.IsNullOrEmpty(getAllChangedFilesCommandOutput))
        {
            return diffFiles;
        }

        // Split all files on '\0'
        var files = getAllChangedFilesCommandOutput.Split(Delimiters.Null, StringSplitOptions.RemoveEmptyEntries);
        for (var n = 0; n < files.Length; ++n)
        {
            var line = files[n].AsSpan();
            if (line.Length <= 2 || line[1] != ' ' || line[0] == '#')
            {
                // Illegal info, like error output,
                // except '#' that is an ignored header
                continue;
            }

            var entryType = line[0];

            if (entryType == GitItemStatusConverter.UntrackedStatus ||
                entryType == GitItemStatusConverter.IgnoredStatus)
            {
                // Untracked and ignored with just the path following, supply dummy data for most info.
                var otherFileName = line[2..].ToString();
                const string notSumoduleEntry = "N...";
                UpdateItemStatus(entryType, false, notSumoduleEntry, otherFileName, null, null);
                continue;
            }

            const char ordinaryEntry = '1';
            const char renamedEntry = '2';
            const char unmergedEntry = 'u';

            if ((entryType != ordinaryEntry && entryType != renamedEntry && entryType != unmergedEntry) ||
                line.Length <= 3)
            {
                // Illegal entry type
                continue;
            }

            // Parse from git-status documentation, assuming SHA-1 is used
            // Ignore octal and treeGuid
            // 1 XY subm <mH> <mI> <mW> <hH> <hI> <path>
            // renamed:
            // 2 XY subm <mH> <mI> <mW> <hH> <hI> <X><score> <path><sep><origPath>
            // worktree (merge conflicts)
            // u XY subm <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>

            var x = line[2];
            var y = line[3];
            string fileName;
            string? oldFileName = null;
            string? renamePercent = null;
            var subm = line[5..9].ToString();

            if (entryType == ordinaryEntry)
            {
                DebugHelpers.Assert(line.Length > 113, $"Cannot parse line: {line}");
                fileName = line[113..].ToString();
            }
            else if (entryType == renamedEntry)
            {
                DebugHelpers.Assert(n + 1 < files.Length, "Cannot parse renamed: {line}");

                // Find renamed files...
                var pos = line[114..].IndexOfAnyExceptInRange('0', '9');
                DebugHelpers.Assert(pos >= 0, "Cannot find space separating rename: {line}");
                renamePercent = line[114..(114 + pos)].ToString();
                fileName = line[(114 + pos + 1)..].ToString();
                oldFileName = files[++n];
            }
            else if (entryType == unmergedEntry)
            {
                DebugHelpers.Assert(line.Length > 161, $"Cannot parse line: {line}");
                fileName = line[161..].ToString();
            }
            else
            {
                // illegal, already checked
                continue;
            }

            // Skip unmerged where both are modified: Only worktree interesting.
            if (entryType != unmergedEntry || x != GitItemStatusConverter.UnmergedStatus ||
                y != GitItemStatusConverter.UnmergedStatus)
            {
                UpdateItemStatus(x, true, subm, fileName, oldFileName, renamePercent);
            }

            UpdateItemStatus(y, false, subm, fileName, oldFileName, renamePercent);
        }

        return diffFiles;

        void UpdateItemStatus(char x, bool isIndex, string subm, string fileName, string? oldFileName,
            string? renamePercent)
        {
            if (x == GitItemStatusConverter.UnmodifiedStatusV2)
            {
                return;
            }

            var staged = isIndex ? StagedStatus.Index : StagedStatus.WorkTree;
            var gitItemStatus = GitItemStatusConverter.FromStatusCharacter(staged, fileName, x);
            if (oldFileName is not null)
            {
                gitItemStatus.OldName = oldFileName;
            }

            if (renamePercent is not null)
            {
                gitItemStatus.RenameCopyPercentage = renamePercent;
            }

            const char submoduleState = 'S';
            if (subm[0] == submoduleState)
            {
                gitItemStatus.IsSubmodule = true;

                if (!isIndex)
                {
                    // Slight modification on how the following flags are used
                    // Changed commit
                    gitItemStatus.IsChanged = subm[1] == GitItemStatusConverter.CopiedStatus;
                    gitItemStatus.IsDirty = subm[2] == GitItemStatusConverter.ModifiedStatus ||
                                            subm[3] == GitItemStatusConverter.UnmergedStatus;
                }
            }

            diffFiles.Add(gitItemStatus);
        }
    }
}
