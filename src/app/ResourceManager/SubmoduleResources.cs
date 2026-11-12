using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using ResourceManager.CommitDataRenders;

namespace ResourceManager;

public static partial class SubmoduleResources
{
    private static readonly ICommitDataHeaderRenderer _plainCommitDataHeaderRenderer =
        new CommitDataHeaderRenderer(new MonospacedHeaderLabelFormatter(), new DateFormatter(),
            new MonospacedHeaderRenderStyleProvider(), null);

    [GeneratedRegex(@"^(?<prefix>\s*\S+)\s+", RegexOptions.Multiline | RegexOptions.ExplicitCapture)]
    private static partial Regex ReplaceTrailingSpacesWithTabRegex { get; }

    public static string GetSubmoduleText(IGitModule superproject, string name, string hash)
    {
        StringBuilder sb = new();
        sb.AppendLine("Submodule " + name);
        sb.AppendLine();

        // Submodule directory must exist to run commands, unknown otherwise
        var module = superproject.GetSubmodule(name);
        if (module.IsValidGitWorkingDir()
            && new CommitDataManager(() => module).GetCommitData(hash) is { } data)
        {
            // Get body without Notes, to cache the command
            var header = _plainCommitDataHeaderRenderer.RenderPlain(data);
            var body = data.Body.Trim();
            sb.AppendLine(header);
            sb.AppendLine();
            sb.Append(body);
        }
        else if (!string.IsNullOrWhiteSpace(hash))
        {
            sb.AppendLine("Commit hash:\t" + hash);
        }
        else
        {
            sb.AppendLine("Invalid git directory and commit hash");
        }

        return sb.ToString();
    }

    public static string GetSubmoduleStatusText(IGitModule module, GitSubmoduleStatus status,
        bool moduleIsParent = true, bool limitOutput = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(status);

        var gitModule = moduleIsParent ? module.GetSubmodule(status.Name) : module;
        StringBuilder sb = new();
        sb.AppendLine($"Submodule {status.Name}");

        if (status.OldCommit != status.Commit && !status.OldCommit.IsZero)
        {
            sb.AppendLine();
            sb.Append("From:\t");
            sb.AppendLine(status.OldCommit.ToString());

            // Submodule directory must exist to run commands, unknown otherwise
            if (status.OldCommitData is not null)
            {
                sb.AppendLine(
                    "\t\t\t" + LocalizationHelpers.GetRelativeDateString(DateTime.UtcNow,
                        status.OldCommitData.CommitDate.UtcDateTime) + " (" +
                    LocalizationHelpers.GetFullDateString(status.OldCommitData.CommitDate) + ")");
                var lines = status.OldCommitData.Body.Trim(Delimiters.LineFeedAndCarriageReturn)
                    .Split(Delimiters.LineFeedAndCarriageReturn, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    sb.AppendLine("\t\t" + line);
                }
            }
        }

        if (!status.Commit.IsZero)
        {
            sb.AppendLine();
            var dirty = !status.IsDirty ? "" : " (dirty)";

            // Note: add spaces to get "To" aligned the same as From
            sb.Append(status.OldCommit != status.Commit ? "To:  \t" : "Commit:\t");
            sb.AppendLine(status.Commit + dirty);

            if (status.CommitData is not null)
            {
                sb.AppendLine(
                    "\t\t\t" + LocalizationHelpers.GetRelativeDateString(DateTime.UtcNow,
                        status.CommitData.CommitDate.UtcDateTime) + " (" +
                    LocalizationHelpers.GetFullDateString(status.CommitData.CommitDate) + ")");
                var lines = status.CommitData.Body.Trim(Delimiters.LineFeedAndCarriageReturn)
                    .Split(Delimiters.LineFeedAndCarriageReturn, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    sb.AppendLine("\t\t" + line);
                }
            }
        }

        sb.AppendLine();
        sb.Append("Type: ");
        switch (status.Status)
        {
            case SubmoduleStatus.Modified:
                sb.Append("Modified");
                break;
            case SubmoduleStatus.NewSubmodule:
                sb.Append("New submodule");
                break;
            case SubmoduleStatus.RemovedSubmodule:
                sb.AppendLine("Removed submodule");
                break;
            case SubmoduleStatus.SameCommit:
                sb.Append("Same commit");
                break;
            case SubmoduleStatus.FastForward:
                sb.Append("Fast Forward");
                break;
            case SubmoduleStatus.Rewind:
                sb.Append("Rewind");
                break;
            case SubmoduleStatus.NewerTime:
                sb.Append("Newer commit time");
                break;
            case SubmoduleStatus.OlderTime:
                sb.Append("Older commit time");
                break;
            case SubmoduleStatus.Unknown:
            default:
                sb.Append("Unknown");

                break;
        }

        sb.AppendLine(status.IsDirty ? " Dirty" : "");

        if (AddedAndRemovedStringLong(status) is { } addedRemoved
            && !string.IsNullOrWhiteSpace(addedRemoved))
        {
            sb.AppendLine();
            sb.AppendLine($"Commits: {addedRemoved}");
        }

        if (!status.Commit.IsZero && !status.OldCommit.IsZero && gitModule.IsValidGitWorkingDir())
        {
            const int maxLimitedLines = 5;
            if (status.IsDirty)
            {
                var statusText = gitModule.GetStatusText(false);
                if (!string.IsNullOrEmpty(statusText))
                {
                    sb.AppendLine();
                    sb.AppendLine("Status:");
                    if (limitOutput)
                    {
                        var txt = statusText.Replace("\r\n", "\n")
                            .Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries);
                        if (txt.Length > maxLimitedLines)
                        {
                            statusText = new List<string>(txt).Take(maxLimitedLines).Join(Environment.NewLine) +
                                         $"{Environment.NewLine} {txt.Length - maxLimitedLines} more changes";
                        }
                    }

                    // format similar to Differences:
                    sb.Append(ReplaceTrailingSpacesWithTab(statusText));
                }
            }

            var exec = gitModule.GetDiffFiles(status.OldCommit.ToString(), status.Commit.ToString(), false,
                false, default);
            if (exec.ExitedSuccessfully)
            {
                var diffs = exec.StandardOutput;
                if (!string.IsNullOrEmpty(diffs))
                {
                    sb.AppendLine();
                    sb.AppendLine("Differences:");
                    if (limitOutput)
                    {
                        var txt = diffs.Replace("\r\n", "\n")
                            .Split(Delimiters.LineFeed, StringSplitOptions.RemoveEmptyEntries);
                        if (txt.Length > maxLimitedLines)
                        {
                            diffs = new List<string>(txt).Take(maxLimitedLines).Join(Environment.NewLine) +
                                    $"{Environment.NewLine} {txt.Length - maxLimitedLines} more differences";
                        }
                    }

                    sb.Append(diffs);
                }
            }
        }

        return sb.ToString();

        static string AddedAndRemovedStringLong(GitSubmoduleStatus status)
        {
            if (status.RemovedCommits is null || status.AddedCommits is null ||
                (status.RemovedCommits == 0 && status.AddedCommits == 0))
            {
                return "";
            }

            return $"{status.AddedCommits} added, {status.RemovedCommits} removed";
        }

        static string ReplaceTrailingSpacesWithTab(string input)
        {
            return ReplaceTrailingSpacesWithTabRegex.Replace(input, "${prefix}\t");
        }
    }
}
