using System.Net;
using GitExtensions.Extensibility.Extensions;
using GitExtensions.Extensibility.Git;

namespace ResourceManager.CommitDataRenders;

/// <summary>
///     Provides the ability to render the body of a commit message.
/// </summary>
public interface ICommitDataBodyRenderer
{
    /// <summary>
    ///     Render the body of a commit message.
    /// </summary>
    string Render(CommitData commitData, bool showRevisionsAsLinks);
}

/// <summary>
///     Renders the body of a commit message.
/// </summary>
public sealed class CommitDataBodyRenderer(Func<IGitModule> getModule, ILinkFactory linkFactory)
    : ICommitDataBodyRenderer
{
    /// <summary>
    ///     Render the body of a commit message.
    /// </summary>
    string ICommitDataBodyRenderer.Render(CommitData commitData, bool showRevisionsAsLinks)
    {
        ArgumentNullException.ThrowIfNull(commitData);

        var body =
            WebUtility.HtmlEncode((UiExtensions.FormatBodyAndNotes(commitData.Body, commitData.Notes) ?? "").Trim());

        if (showRevisionsAsLinks)
        {
            body = GitRevision.Sha1HashShortRegex.Replace(body, match => ProcessHashCandidate(match.Value));
        }

        return body;
    }

    private string ProcessHashCandidate(string hash)
    {
        var module = getModule();

        if (module is null)
        {
            return hash;
        }

        if (!module.TryResolvePartialCommitId(hash, out var fullHash))
        {
            return hash;
        }

        return linkFactory.CreateCommitLink(fullHash, hash, true);
    }
}
