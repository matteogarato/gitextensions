using GitCommands.ExternalLinks;
using GitUI.CommandsDialogs.SettingsDialog.RevisionLinks;

namespace GitUITests.CommandsDialogs.Settings;

public class AzureDevOpsExternalLinkDefinitionExtractorTests
{
    [Theory]
    [InlineData("https://owner@dev.azure.com/owner/project/_git/repo")]
    [InlineData("git@ssh.dev.azure.com:v3/owner/project/repo")]
    [InlineData("https://owner.visualstudio.com/project/_git/repo")]
    [InlineData("owner@vs-ssh.visualstudio.com:v3/owner/project/repo")]
    public void Should_get_link_definitions_When_successfuly_parsing_remote_url(string url)
    {
        var externalLinkDefinitions =
            new AzureDevopsExternalLinkDefinitionExtractor().GetDefinitions(url);
        externalLinkDefinitions.Should().HaveCount(2);
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
            new AzureDevopsExternalLinkDefinitionExtractor().GetDefinitions(null!);
        externalLinkDefinitions.Should().HaveCount(2);
        foreach (var externalLinkDefinition in externalLinkDefinitions)
        {
            externalLinkDefinition.LinkFormats.Should().HaveCountGreaterThanOrEqualTo(1);
            foreach (var externalLinkFormat in externalLinkDefinition.LinkFormats)
            {
                externalLinkFormat.Format.Should().Contain("ACCOUNT_NAME").And.Contain("REPO_NAME");
            }
        }
    }
}
