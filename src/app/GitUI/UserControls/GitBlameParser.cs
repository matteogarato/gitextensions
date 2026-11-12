using System.Diagnostics;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.UserControls;

internal interface IGitBlameParser
{
    int GetOriginalLineInPreviousCommit(GitRevision selectedBlamedRevision, string filename, int selectedLine);
}

internal partial class GitBlameParser(Func<IGitModule> getModule) : IGitBlameParser
{
    [GeneratedRegex(
        @"\@\@ -(?<PreviousLineNumber>\d+)(,(?<LineRemovedCount>\d+))? \+(?<CurrentLineNumber>\d+)(,(?<LineAddedCount>\d+))? \@\@",
        RegexOptions.ExplicitCapture)]
    private static partial Regex ChunkHeaderRegex { get; }

    int IGitBlameParser.GetOriginalLineInPreviousCommit(GitRevision selectedBlamedRevision, string filename,
        int selectedLine)
    {
        var parentId = selectedBlamedRevision.FirstParentId;

        if (parentId.IsZero)
        {
            return selectedLine;
        }

        try
        {
            // Get the git diff of changes introduced by the blamed selected commit
            // *without context lines* to make starting line value more accurate and possibly have more distincts chunks!
            // git-config diff algorithm option must be overridden for user preferences and tests
            GitArgumentBuilder args = new("diff")
            {
                "--no-ext-diff",
                "-U0",
                $"--diff-algorithm={(AppSettings.UseHistogramDiffAlgorithm ? "histogram" : "default")}",
                { AppSettings.DetectCopyInFileOnBlame, "--find-renames" }, // git-blame only has -M
                { AppSettings.DetectCopyInAllOnBlame, "--find-copies" }, // git-blame only has -C
                { AppSettings.IgnoreWhitespaceOnBlame, "--ignore-all-space" }, // git-blame only has -w
                parentId,
                selectedBlamedRevision.ObjectId,
                "--",
                filename
            };

            var diffOutput = getModule().GitExecutable.GetOutput(args, cache: GitModule.GitCommandCache);

            // Find the good chunk of code with current line number (starting from the end), skipping header
            foreach (var chunk in diffOutput.Split("\n@@").Skip(1).Reverse())
            {
                var match = ChunkHeaderRegex.Match($"@@{chunk}");
                if (!match.Success)
                {
                    Trace.WriteLine($"Blame previous commit: chunk/regex not matching\n@@{chunk}");
                    continue;
                }

                var currentChunkStartingLine = int.Parse(match.Groups["CurrentLineNumber"].ValueSpan);
                if (currentChunkStartingLine <= selectedLine)
                {
                    // Calculate the offset thanks to the diff chunk header
                    var previousChunkStartingLine = int.Parse(match.Groups["PreviousLineNumber"].ValueSpan);
                    var lineRemovedCount = match.Groups["LineRemovedCount"].Success
                        ? int.Parse(match.Groups["LineRemovedCount"].ValueSpan)
                        : 1;
                    var lineAddedCount = match.Groups["LineAddedCount"].Success
                        ? int.Parse(match.Groups["LineAddedCount"].ValueSpan)
                        : 1;

                    // calculate the new position based on changes made above in the file
                    // and changes made in this chunk
                    return Math.Max(previousChunkStartingLine,
                        selectedLine - currentChunkStartingLine + previousChunkStartingLine - lineAddedCount +
                        lineRemovedCount);
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine("Blame previous commit: error when evaluating a better line number. Exception:" + ex);
        }

        return selectedLine;
    }
}
