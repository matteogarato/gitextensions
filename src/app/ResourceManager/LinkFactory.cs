using System.Diagnostics.CodeAnalysis;
using System.Net;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;

namespace ResourceManager;

public interface ILinkFactory
{
    string CreateLink(string? caption, string uri);

    string CreateTagLink(string tag);

    string CreateBranchLink(string noPrefixBranch);

    string CreateCommitLink(ObjectId objectId, string? linkText = null, bool preserveGuidInLinkText = false);

    string CreateShowAllLink(string what);

    void ExecuteLink(string? linkUri, Action<CommandEventArgs>? handleInternalLink = null,
        Action<string?>? showAll = null);
}

public sealed class LinkFactory : ILinkFactory
{
    private const string _internalScheme = "gitext";
    private const string _showAll = "showall";

    string ILinkFactory.CreateLink(string? caption, string uri)
    {
        var htmlUri = WebUtility.HtmlEncode(uri);

        var htmlLink = "<a href=" + htmlUri.Quote("'") + ">" + WebUtility.HtmlEncode(caption) + "</a>";
        return htmlLink;
    }

    string ILinkFactory.CreateTagLink(string tag)
    {
        if (tag != "…")
        {
            return CreateLink(tag, $"{_internalScheme}://gototag/" + tag);
        }

        return WebUtility.HtmlEncode(tag);
    }

    string ILinkFactory.CreateBranchLink(string noPrefixBranch)
    {
        if (noPrefixBranch != "…")
        {
            var linkTarget = DetachedHeadParser.IsDetachedHead(noPrefixBranch) ? "HEAD" : noPrefixBranch;
            return CreateLink(noPrefixBranch, $"{_internalScheme}://gotobranch/{linkTarget}");
        }

        return WebUtility.HtmlEncode(noPrefixBranch);
    }

    string ILinkFactory.CreateCommitLink(ObjectId objectId, string? linkText = null,
        bool preserveGuidInLinkText = false)
    {
        if (linkText is null)
        {
            if (objectId == ObjectId.WorkTreeId)
            {
                linkText = TranslatedStrings.Workspace;
            }
            else if (objectId == ObjectId.IndexId)
            {
                linkText = TranslatedStrings.Index;
            }
            else if (preserveGuidInLinkText)
            {
                linkText = objectId.ToString();
            }
            else
            {
                linkText = objectId.ToShortString();
            }
        }

        return CreateLink(linkText, $"{_internalScheme}://gotocommit/" + objectId);
    }

    string ILinkFactory.CreateShowAllLink(string what)
    {
        return CreateLink($"[ {TranslatedStrings.ShowAll} ]", $"{_internalScheme}://{_showAll}/{what}");
    }

    void ILinkFactory.ExecuteLink(string? linkUri, Action<CommandEventArgs>? handleInternalLink = null,
        Action<string?>? showAll = null)
    {
        if (!TryParseLink(linkUri, out var uri))
        {
            return;
        }

        if (ParseInternalScheme(uri, out var commandEventArgs))
        {
            if (commandEventArgs.Command == _showAll)
            {
                if (showAll is null)
                {
                    throw new InvalidOperationException($"unexpected internal link: {linkUri}");
                }

                showAll(commandEventArgs.Data);
                return;
            }

            if (handleInternalLink is null)
            {
                throw new InvalidOperationException($"unexpected internal link: {linkUri}");
            }

            handleInternalLink(commandEventArgs);
            return;
        }

        OsShellUtil.OpenUrlInDefaultBrowser(uri.AbsoluteUri);
    }

    private static bool ParseInternalScheme(Uri? uri, [NotNullWhen(true)] out CommandEventArgs? commandEventArgs)
    {
        if (uri?.Scheme == _internalScheme)
        {
            commandEventArgs = new CommandEventArgs(uri.Host, uri.AbsolutePath.TrimStart('/'));
            return true;
        }

        commandEventArgs = null;
        return false;
    }

    private static bool TryParseLink(string? linkUri, [NotNullWhen(true)] out Uri? uri)
    {
        if (string.IsNullOrWhiteSpace(linkUri))
        {
            uri = null;
            return false;
        }

        var uriCandidate = linkUri;
        while (true)
        {
            if (Uri.TryCreate(uriCandidate, UriKind.Absolute, out uri))
            {
                return true;
            }

            var idx = uriCandidate.IndexOf('#');
            if (idx == -1)
            {
                break;
            }

            uriCandidate = uriCandidate[(idx + 1)..];
        }

        return false;
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(LinkFactory linkFactory)
    {
        private readonly LinkFactory _linkFactory = linkFactory;

        public readonly bool ParseInternalScheme(Uri uri, [NotNullWhen(true)] out CommandEventArgs? commandEventArgs)
        {
            return LinkFactory.ParseInternalScheme(uri, out commandEventArgs);
        }

        public readonly bool TryParseLink(string? linkUri, [NotNullWhen(true)] out Uri? uri)
        {
            return LinkFactory.TryParseLink(linkUri, out uri);
        }
    }
}
