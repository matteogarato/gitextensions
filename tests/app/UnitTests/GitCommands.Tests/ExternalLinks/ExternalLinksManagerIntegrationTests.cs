using System.Reflection;
using CommonTestUtils;
using GitCommands.ExternalLinks;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitCommandsTests.ExternalLinks;

public class ExternalLinksManagerIntegrationTests : IDisposable
{
    private readonly ExternalLinksStorage _externalLinksStorage = null!;
    private readonly string _level1 = null!;
    private readonly string _level2 = null!;
    private readonly string _level3 = null!;
    private readonly DistributedSettings _repoDistributed = null!;
    private readonly DistributedSettings _repoLocal = null!;
    private readonly GitModuleTestHelper _testHelper = null!;
    private readonly DistributedSettings _userRoaming = null!;

    public ExternalLinksManagerIntegrationTests()
    {
        _testHelper = new GitModuleTestHelper();

        var content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level1_repogit_GitExtensions.settings.xml");
        _level1 = _testHelper.CreateRepoFile(".git", "GitExtensions.settings", content);
        content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level2_repodist_GitExtensions.settings.xml");
        _level2 = _testHelper.CreateFile(_testHelper.TemporaryPath + "/RoamingProfile", "GitExtensions.settings",
            content);
        content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level3_roaming_GitExtensions.settings.xml");
        _level3 = _testHelper.CreateRepoFile("GitExtensions.settings", content);

        _userRoaming = new DistributedSettings(null, new GitExtSettingsCache(_level3), SettingLevel.Global);
        _repoDistributed =
            new DistributedSettings(_userRoaming, new GitExtSettingsCache(_level2), SettingLevel.Distributed);
        _repoLocal = new DistributedSettings(_repoDistributed, new GitExtSettingsCache(_level1), SettingLevel.Local);

        _externalLinksStorage = new ExternalLinksStorage();
    }

    void IDisposable.Dispose()
    {
        _userRoaming.SettingsCache.Dispose();
        _repoDistributed.SettingsCache.Dispose();
        _repoLocal.SettingsCache.Dispose();

        _testHelper.Dispose();
    }

    [Fact]
    public void Add_should_add_new_definition_to_the_lowest_level_level2()
    {
        _externalLinksStorage.Load(_userRoaming).Should().ContainSingle();

        ExternalLinksManager manager = new(_repoDistributed);

        // 1 comes from the user roaming settings
        // 3 come from the distributed
        manager.GetEffectiveSettings().Should().HaveCount(4);

        ExternalLinkDefinition definition = new() { Name = "test", SearchPattern = "pattern" };
        manager.Add(definition);

        var effectiveSettings = manager.GetEffectiveSettings();

        // 2 comes from the user roaming settings
        // 3 come from the distributed
        effectiveSettings.Should().HaveCount(5);

        manager.Save();

        _externalLinksStorage.Load(_userRoaming).Should().HaveCount(2);
    }

    [Fact]
    public void Add_should_add_new_definition_to_the_lowest_level_level3()
    {
        _externalLinksStorage.Load(_userRoaming).Should().ContainSingle();

        ExternalLinksManager manager = new(_repoLocal);

        // 1 comes from the user roaming settings
        // 3 come from the distributed
        // 1 comes from the local
        manager.GetEffectiveSettings().Should().HaveCount(5);

        ExternalLinkDefinition definition = new() { Name = "test", SearchPattern = "pattern" };
        manager.Add(definition);

        var effectiveSettings = manager.GetEffectiveSettings();

        // 1 comes from the local
        // 3 come from the distributed
        // 2 comes from the local
        effectiveSettings.Should().HaveCount(6);

        manager.Save();

        _externalLinksStorage.Load(_userRoaming).Should().HaveCount(2);
    }

    [Fact]
    public void Add_should_add_definition_present_in_lower_level_to_higher_level()
    {
        _externalLinksStorage.Load(_userRoaming).Should().ContainSingle();
        _externalLinksStorage.Load(_repoDistributed).Should().HaveCount(3);

        ExternalLinksManager manager = new(_repoLocal);

        // 1 comes from the user roaming settings
        // 3 come from the distributed
        // 1 comes from the local
        manager.GetEffectiveSettings().Should().HaveCount(5);

        ExternalLinkDefinition definition = new() { Name = "Stash", SearchPattern = "pattern" };
        manager.Add(definition);

        var effectiveSettings = manager.GetEffectiveSettings();

        // 1 comes from the user roaming settings
        // 4 come from the distributed
        // 1 comes from the local
        effectiveSettings.Should().HaveCount(6);

        manager.Save();

        _externalLinksStorage.Load(_userRoaming).Should().ContainSingle();
        _externalLinksStorage.Load(_repoDistributed).Should().HaveCount(4);
    }

    [Fact]
    public void Remove_should_remove_definition_from_collection()
    {
        _externalLinksStorage.Load(_userRoaming).Should().ContainSingle();
        _externalLinksStorage.Load(_repoDistributed).Should().HaveCount(3);

        ExternalLinksManager manager = new(_repoLocal);

        var effective = manager.GetEffectiveSettings();

        // 1 comes from the user roaming settings
        // 3 come from the distributed
        // 1 comes from the local
        effective.Should().HaveCount(5);

        manager.Remove(effective[^1]);

        manager.Save();

        var effectiveSettings = manager.GetEffectiveSettings();

        // 0 comes from the user roaming settings
        // 3 come from the distributed
        // 1 comes from the local
        effectiveSettings.Should().HaveCount(4);

        _externalLinksStorage.Load(_userRoaming).Should().BeEmpty();
        _externalLinksStorage.Load(_repoDistributed).Should().HaveCount(3);
    }
}
