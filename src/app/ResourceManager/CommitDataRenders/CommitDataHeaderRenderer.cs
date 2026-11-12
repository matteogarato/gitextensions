using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GitExtensions.Extensibility.Git;

namespace ResourceManager.CommitDataRenders;

/// <summary>
///     Provides the ability to render commit information.
/// </summary>
public interface ICommitDataHeaderRenderer
{
    /// <summary>
    ///     Gets the plain text for the clipboard - without tabs, relative date, children and parents.
    /// </summary>
    string GetPlainText(string header);

    Font GetFont(Graphics g);

    IEnumerable<int> GetTabStops();

    /// <summary>
    ///     Generate header.
    /// </summary>
    string Render(CommitData commitData, bool showRevisionsAsLinks);

    /// <summary>
    ///     Generate header.
    /// </summary>
    string RenderPlain(CommitData commitData);
}

/// <summary>
///     Renders commit information in a tabular format with data columns aligned with spaces.
/// </summary>
public sealed partial class CommitDataHeaderRenderer(
    IHeaderLabelFormatter labelFormatter,
    IDateFormatter dateFormatter,
    IHeaderRenderStyleProvider headerRendererStyleProvider,
    ILinkFactory? linkFactory)
    : ICommitDataHeaderRenderer
{
    [GeneratedRegex(@"[ \t]+", RegexOptions.ExplicitCapture)]
    private static partial Regex SpacesRegex { get; }

    [GeneratedRegex(@"(?<reltime>\n[^:]+: ).* ago \((?<unit>[^)]+)\)", RegexOptions.ExplicitCapture)]
    private static partial Regex RemoveAgoRegex { get; }

    string ICommitDataHeaderRenderer.GetPlainText(string header)
    {
        var children =
            $"({TranslatedStrings.GetChildren(1)})|({TranslatedStrings.GetChildren(2)})|({TranslatedStrings.GetChildren(10)})";
        var parents =
            $"({TranslatedStrings.GetParents(1)})|({TranslatedStrings.GetParents(2)})|({TranslatedStrings.GetParents(10)})";
        header = SpacesRegex.Replace(header, " ");
        header = RemoveAgoRegex.Replace(header, "$1$2");
        header = Regex.Replace(header, @$"\n({children}|{parents})[^\n]*", "");
        return header;
    }

    Font ICommitDataHeaderRenderer.GetFont(Graphics g)
    {
        return headerRendererStyleProvider.GetFont(g);
    }

    IEnumerable<int> ICommitDataHeaderRenderer.GetTabStops()
    {
        return headerRendererStyleProvider.GetTabStops();
    }

    /// <summary>
    ///     Generate header.
    /// </summary>
    string ICommitDataHeaderRenderer.Render(CommitData commitData, bool showRevisionsAsLinks)
    {
        ArgumentNullException.ThrowIfNull(commitData);

        var isArtificial = commitData.ObjectId.IsArtificial;
        var authorIsCommitter =
            string.Equals(commitData.Author, commitData.Committer, StringComparison.CurrentCulture);
        var datesEqual = commitData.AuthorDate.EqualsExact(commitData.CommitDate);
        var padding = headerRendererStyleProvider.GetMaxWidth();
        var authorEmail = GetEmail(commitData.Author);

        Validates.NotNull(linkFactory);

        StringBuilder header = new();
        header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.Author, padding) +
                          linkFactory.CreateLink(commitData.Author, "mailto:" + authorEmail));

        if (!isArtificial)
        {
            header.AppendLine(
                labelFormatter.FormatLabel(datesEqual ? TranslatedStrings.Date : TranslatedStrings.AuthorDate,
                    padding) + WebUtility.HtmlEncode(dateFormatter.FormatDateAsRelativeLocal(commitData.AuthorDate)));
        }

        if (!authorIsCommitter)
        {
            var committerEmail = GetEmail(commitData.Committer);
            header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.Committer, padding) +
                              linkFactory.CreateLink(commitData.Committer, "mailto:" + committerEmail));
        }

        if (!isArtificial)
        {
            if (!datesEqual)
            {
                header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.CommitDate, padding) +
                                  WebUtility.HtmlEncode(
                                      dateFormatter.FormatDateAsRelativeLocal(commitData.CommitDate)));
            }

            header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.CommitHash, padding) +
                              WebUtility.HtmlEncode(commitData.ObjectId.ToString()));
        }

        if (commitData.ChildIds is not null && commitData.ChildIds.Count != 0)
        {
            header.AppendLine(
                labelFormatter.FormatLabel(TranslatedStrings.GetChildren(commitData.ChildIds.Count), padding) +
                RenderObjectIds(commitData.ChildIds, showRevisionsAsLinks));
        }

        var parentIds = commitData.ParentIds;
        if (parentIds?.Count > 0)
        {
            header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.GetParents(parentIds.Count), padding) +
                              RenderObjectIds(parentIds, showRevisionsAsLinks));
        }

        // remove the trailing newline character
        header.Length -= Environment.NewLine.Length;

        return header.ToString();
    }

    /// <summary>
    ///     Generate header.
    /// </summary>
    string ICommitDataHeaderRenderer.RenderPlain(CommitData commitData)
    {
        ArgumentNullException.ThrowIfNull(commitData);

        var authorIsCommitter =
            string.Equals(commitData.Author, commitData.Committer, StringComparison.CurrentCulture);
        var datesEqual = commitData.AuthorDate.EqualsExact(commitData.CommitDate);
        var padding = headerRendererStyleProvider.GetMaxWidth();

        StringBuilder header = new();
        header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.Author, padding) + commitData.Author);
        header.AppendLine(
            labelFormatter.FormatLabel(datesEqual ? TranslatedStrings.Date : TranslatedStrings.AuthorDate, padding) +
            dateFormatter.FormatDateAsRelativeLocal(commitData.AuthorDate));
        if (!authorIsCommitter)
        {
            header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.Committer, padding) + commitData.Committer);
        }

        if (!datesEqual)
        {
            header.AppendLine(labelFormatter.FormatLabel(TranslatedStrings.CommitDate, padding) +
                              dateFormatter.FormatDateAsRelativeLocal(commitData.CommitDate));
        }

        header.Append(labelFormatter.FormatLabel(TranslatedStrings.CommitHash, padding) + commitData.ObjectId);

        return header.ToString();
    }

    private static string GetEmail(string? author)
    {
        if (string.IsNullOrEmpty(author))
        {
            return "";
        }

        var ind = author.IndexOf('<');
        if (ind == -1)
        {
            return "";
        }

        ++ind;
        return author[ind..author.LastIndexOf('>')];
    }

    private string RenderObjectIds(IEnumerable<ObjectId> objectIds, bool showRevisionsAsLinks)
    {
        Validates.NotNull(linkFactory);
        return showRevisionsAsLinks
            ? objectIds.Select(id => linkFactory.CreateCommitLink(id)).Join(" ")
            : objectIds.Select(id => id.ToShortString()).Join(" ");
    }
}
