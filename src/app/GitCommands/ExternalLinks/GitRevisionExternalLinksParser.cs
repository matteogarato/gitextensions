using GitCommands.Settings;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitCommands.ExternalLinks;

public interface IGitRevisionExternalLinksParser
{
    IEnumerable<ExternalLink> Parse(GitRevision revision, DistributedSettings settings);
}

public sealed class GitRevisionExternalLinksParser(
    IConfiguredLinkDefinitionsProvider effectiveLinkDefinitionsProvider,
    IExternalLinkRevisionParser externalLinkRevisionParser)
    : IGitRevisionExternalLinksParser
{
    IEnumerable<ExternalLink> IGitRevisionExternalLinksParser.Parse(GitRevision revision, DistributedSettings settings)
    {
        var definitions = effectiveLinkDefinitionsProvider.Get(settings);
        return definitions.Where(definition => definition.Enabled)
            .SelectMany(definition => externalLinkRevisionParser.Parse(revision, definition));
    }
}
