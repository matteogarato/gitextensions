using GitCommands.ExternalLinks;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;

namespace GitUITests.CommandsDialogs.Settings;

public class GitHubExternalLinkDefinitionExtractorTests
{
    [Theory]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("http://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("ssh://git@github.com/owner/repo.git")]
    [InlineData("git@github.com/owner/repo.git")]
    public void Should_get_link_definitions(string url)
    {
        var externalLinkDefinitions =
            new GitHubExternalLinkDefinitionExtractor().GetDefinitions(url);
        externalLinkDefinitions.Should().HaveCount(3);
        foreach (var externalLinkDefinition in externalLinkDefinitions)
        {
            externalLinkDefinition.LinkFormats.Should().HaveCountGreaterThanOrEqualTo(1);
            foreach (var externalLinkFormat in externalLinkDefinition.LinkFormats)
            {
                externalLinkFormat.Format.Should().Contain("owner").And.Contain("repo");
            }
        }
    }

    [Fact]
    public void Should_get_link_definitions_When_no_remote_url_provided()
    {
        var externalLinkDefinitions =
            new GitHubExternalLinkDefinitionExtractor().GetDefinitions(null!);
        externalLinkDefinitions.Should().HaveCount(3);
        foreach (var externalLinkDefinition in externalLinkDefinitions)
        {
            externalLinkDefinition.LinkFormats.Should().HaveCountGreaterThanOrEqualTo(1);
            foreach (var externalLinkFormat in externalLinkDefinition.LinkFormats)
            {
                externalLinkFormat.Format.Should().Contain("ORGANIZATION_NAME").And.Contain("REPO_NAME");
            }
        }
    }
}
