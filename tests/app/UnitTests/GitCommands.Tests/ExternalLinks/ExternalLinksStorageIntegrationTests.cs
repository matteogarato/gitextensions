using System.Reflection;
using CommonTestUtils;
using GitCommands.ExternalLinks;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitCommandsTests.ExternalLinks;
public class ExternalLinksStorageIntegrationTests
{
    private ExternalLinksStorage _externalLinksStorage = null!;

    public ExternalLinksStorageIntegrationTests()
    {
        _externalLinksStorage = new ExternalLinksStorage();
    }

    [Theory]
    [InlineData("level1_repogit_GitExtensions", 1)]
    [InlineData("level2_repodist_GitExtensions", 3)]
    [InlineData("level3_roaming_GitExtensions", 1)]
    public void Can_load_settings(string fileName, int expected)
    {
        string content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(), $"{GetType().Namespace}.MockData.{fileName}.settings.xml");

        using GitModuleTestHelper testHelper = new();
        string settingsFile = testHelper.CreateRepoFile(".git", "GitExtensions.settings", content);
        using GitExtSettingsCache settingsCache = new(settingsFile);
        DistributedSettings settings = new(lowerPriority: null, settingsCache, SettingLevel.Unknown);

        IReadOnlyList<ExternalLinkDefinition>? definitions = _externalLinksStorage.Load(settings);
        definitions.Should().HaveCount(expected);
    }

    [Fact]
    public async Task Can_save_settings()
    {
        using GitModuleTestHelper testHelper = new();
        string settingsFile = testHelper.CreateRepoFile(".git", "GitExtensions.settings", "﻿<dictionary />");
        using GitExtSettingsCache settingsCache = new(settingsFile);
        DistributedSettings settings = new(lowerPriority: null, settingsCache, SettingLevel.Unknown);

        ExternalLinkDefinition definition = new()
        {
            Name = "<new>",
            Enabled = true,
            UseRemotesPattern = "upstream|origin",
            UseOnlyFirstRemote = true,
            SearchInParts = { ExternalLinkDefinition.RevisionPart.Message },
            RemoteSearchInParts = { ExternalLinkDefinition.RemotePart.URL }
        };

        _externalLinksStorage.Save(settings, new[] { definition });

        settings.Save();

        await Verifier.VerifyXml(File.ReadAllText(settingsFile));
    }
}
