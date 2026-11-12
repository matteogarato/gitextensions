using System.Text;
using GitCommands.Git;
using ResourceManager;

namespace GitUI.UserControls.RevisionGrid.Graph;

internal sealed class LaneInfoProvider(
    ILaneNodeLocator nodeLocator,
    IGitRevisionSummaryBuilder gitRevisionSummaryBuilder)
{
    private static readonly TranslationString _noInfoText = new("Sorry, this commit seems to be not loaded.");
    private static readonly TranslationString _mergedWithText = new(" (merged with {0})");
    internal static readonly TranslationString ByPullRequestText = new(" by pull request {0}");

    public string GetLaneInfo(int rowIndex, int lane)
    {
        var (node, isAtNode, singleChild) = nodeLocator.FindPrevNode(rowIndex, lane);
        if (node is null)
        {
            return string.Empty;
        }

        if (node.GitRevision is null)
        {
            return _noInfoText.Text;
        }

        StringBuilder laneInfoText = new();

        if (singleChild is not null)
        {
            laneInfoText.Append(singleChild.Objectid.ToShortString()).Append(": ")
                .AppendLine(singleChild.GitRevision?.Subject).AppendLine("|");
        }

        if (!node.GitRevision.IsArtificial)
        {
            if (isAtNode)
            {
                laneInfoText.Append("* ");
            }

            laneInfoText.AppendLine(node.GitRevision.Guid);

            BranchFinder branch = new(node);
            if (!string.IsNullOrWhiteSpace(branch.CommittedTo))
            {
                laneInfoText.AppendFormat("\n{0}: {1}", TranslatedStrings.Branch, branch.CommittedTo);
                if (!string.IsNullOrWhiteSpace(branch.MergedWith))
                {
                    laneInfoText.AppendFormat(_mergedWithText.Text, branch.MergedWith);
                }
            }

            laneInfoText.AppendLine();
        }

        if (node.GitRevision.Body is not null)
        {
            laneInfoText.Append(gitRevisionSummaryBuilder.BuildSummary(node.GitRevision.Body));
        }
        else
        {
            laneInfoText.Append(node.GitRevision.Subject);
            if (node.GitRevision.HasMultiLineMessage)
            {
                laneInfoText.Append(TranslatedStrings.BodyNotLoaded);
            }
        }

        return laneInfoText.ToString();
    }

    internal readonly struct TestAccessor
    {
        internal static TranslationString NoInfoText
        {
            get => _noInfoText;
        }

        internal static TranslationString MergedWithText
        {
            get => _mergedWithText;
        }

        internal static TranslationString ByPullRequestText
        {
            get => LaneInfoProvider.ByPullRequestText;
        }
    }
}
