using System.Net;
using System.Text;
using GitCommands.Settings;
using ResourceManager;

namespace GitUI.CommitInfo;

public sealed class RefsFormatter(ILinkFactory linkFactory)
{
    /// <summary>
    ///     The number of displayed lines if the list is limited.
    /// </summary>
    private const int _maximumDisplayedLinesIfLimited = 12;

    /// <summary>
    ///     The number of displayed refs if the list is limited.
    ///     If limited, the line "[Show all]" and an empty line are added.
    ///     Hence the list needs to be limited only if it exceeds MaximumDisplayedRefsIfLimited + 2.
    /// </summary>
    private const int _maximumDisplayedRefsIfLimited = _maximumDisplayedLinesIfLimited - 2;

    private readonly ILinkFactory _linkFactory = linkFactory ??
                                                 throw new ArgumentNullException(nameof(linkFactory),
                                                     "RefsFormatter requires an ILinkFactory instance");

    public string FormatBranches(IEnumerable<string>? branches, bool showAsLinks, bool limit)
    {
        if (branches is null)
        {
            return string.Empty;
        }

        var (formattedBranches, truncated) = FilterAndFormatBranches(branches, showAsLinks, limit);
        return ToString(formattedBranches, TranslatedStrings.ContainedInBranches, TranslatedStrings.ContainedInNoBranch,
            "branches", truncated);
    }

    public string FormatTags(IReadOnlyList<string> tags, bool showAsLinks, bool limit)
    {
        if (tags is null)
        {
            return string.Empty;
        }

        var truncate = limit && tags.Count > _maximumDisplayedLinesIfLimited;
        var formattedTags = FormatTags(truncate ? tags.Take(_maximumDisplayedRefsIfLimited) : tags);
        return ToString(formattedTags, TranslatedStrings.ContainedInTags, TranslatedStrings.ContainedInNoTag, "tags",
            truncate);

        IEnumerable<string> FormatTags(IEnumerable<string> selectedTags)
        {
            return selectedTags.Select(s =>
                showAsLinks ? _linkFactory.CreateTagLink(s ?? string.Empty) : WebUtility.HtmlEncode(s));
        }
    }

    private (IEnumerable<string> formattedBranches, bool truncated) FilterAndFormatBranches(
        IEnumerable<string> branches, bool showAsLinks, bool limit)
    {
        List<string> formattedBranches = [];
        var truncated = false;

        const string remotesPrefix = "remotes/";

        // Include local branches if explicitly requested or when needed to decide whether to show remotes
        var getLocal = AppSettings.CommitInfoShowContainedInBranchesLocal ||
                       AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal;

        // Include remote branches if requested
        var getRemote = AppSettings.CommitInfoShowContainedInBranchesRemote ||
                        AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal;
        var allowLocal = AppSettings.CommitInfoShowContainedInBranchesLocal;
        var allowRemote = getRemote;

        foreach (var branch in branches)
        {
            var noPrefixBranch = branch;
            bool branchIsLocal;
            if (getLocal && getRemote)
            {
                // "git branch -a" prefixes remote branches with "remotes/"
                // It is possible to create a local branch named "remotes/origin/something"
                // so this check is not 100% reliable.
                // This shouldn't be a big problem if we're only displaying information.
                // This could be solved by listing local and remote branches separately.
                branchIsLocal = !branch.StartsWith(remotesPrefix);
                if (!branchIsLocal)
                {
                    noPrefixBranch = branch[remotesPrefix.Length..];
                }
            }
            else
            {
                branchIsLocal = !getRemote;
            }

            if ((branchIsLocal && allowLocal) || (!branchIsLocal && allowRemote))
            {
                var branchText = showAsLinks
                    ? _linkFactory.CreateBranchLink(noPrefixBranch)
                    : WebUtility.HtmlEncode(noPrefixBranch);

                if (limit && formattedBranches.Count == _maximumDisplayedLinesIfLimited)
                {
                    formattedBranches.RemoveRange(_maximumDisplayedRefsIfLimited,
                        _maximumDisplayedLinesIfLimited - _maximumDisplayedRefsIfLimited);
                    truncated = true;
                    break; // from foreach
                }

                formattedBranches.Add(branchText);
            }

            if (branchIsLocal && AppSettings.CommitInfoShowContainedInBranchesRemoteIfNoLocal)
            {
                allowRemote = false;
            }
        }

        return (formattedBranches, truncated);
    }

    private string ToString(IEnumerable<string> formattedRefs, string prefix, string textIfEmpty, string refsType,
        bool truncated)
    {
        var linksJoined = formattedRefs?.Join(Environment.NewLine);
        if (string.IsNullOrEmpty(linksJoined))
        {
            return WebUtility.HtmlEncode(textIfEmpty);
        }

        var sb = new StringBuilder()
            .AppendLine(WebUtility.HtmlEncode(prefix))
            .Append(linksJoined);
        if (truncated)
        {
            sb.AppendLine()
                .Append(_linkFactory.CreateShowAllLink(refsType));
        }

        return sb.ToString();
    }
}
