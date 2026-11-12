using System.Reflection;
using CommonTestUtils;
using GitCommands.ExternalLinks;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using NSubstitute;

namespace GitCommandsTests.ExternalLinks;

public class ConfiguredLinkDefinitionsProviderTests : IDisposable
{
    private readonly IExternalLinksStorage _externalLinksStorage = null!;
    private readonly ConfiguredLinkDefinitionsProvider _provider = null!;
    private readonly DistributedSettings _repoDistributed = null!;
    private readonly string _repoDistributedConfigFilePath = null!;
    private readonly DistributedSettings _repoLocal = null!;
    private readonly string _repoLocalConfigFilePath = null!;
    private readonly GitModuleTestHelper _testHelper = null!;
    private readonly DistributedSettings _userRoaming = null!;
    private readonly string _userRoamingConfigFilePath = null!;

    public ConfiguredLinkDefinitionsProviderTests()
    {
        _testHelper = new GitModuleTestHelper();

        var content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level1_repogit_GitExtensions.settings.xml");
        _repoLocalConfigFilePath = _testHelper.CreateRepoFile(".git", "GitExtensions.settings", content);
        content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level2_repodist_GitExtensions.settings.xml");
        _repoDistributedConfigFilePath = _testHelper.CreateFile(_testHelper.TemporaryPath + "/RoamingProfile",
            "GitExtensions.settings", content);
        content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level3_roaming_GitExtensions.settings.xml");
        _userRoamingConfigFilePath = _testHelper.CreateRepoFile("GitExtensions.settings", content);

        _userRoaming = new DistributedSettings(null, new GitExtSettingsCache(_userRoamingConfigFilePath),
            SettingLevel.Global);
        _repoDistributed = new DistributedSettings(_userRoaming,
            new GitExtSettingsCache(_repoDistributedConfigFilePath), SettingLevel.Distributed);
        _repoLocal = new DistributedSettings(_repoDistributed, new GitExtSettingsCache(_repoLocalConfigFilePath),
            SettingLevel.Local);

        _externalLinksStorage = Substitute.For<IExternalLinksStorage>();

        _provider = new ConfiguredLinkDefinitionsProvider(_externalLinksStorage);
    }

    void IDisposable.Dispose()
    {
        _userRoaming.SettingsCache.Dispose();
        _repoDistributed.SettingsCache.Dispose();
        _repoLocal.SettingsCache.Dispose();

        _testHelper.Dispose();
    }

    [Fact]
    public void Get_should_throw_if_data_null()
    {
        ((Action)(() => _provider.Get(null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Can_load_1_layers_of_settings()
    {
        _externalLinksStorage.Load(Arg.Any<DistributedSettings>()).Returns(new List<ExternalLinkDefinition>
        {
            new() { Name = "user definition 1" }
        });

        var effectiveSettings = _provider.Get(_userRoaming);

        effectiveSettings.Should().ContainSingle();
    }

    [Fact]
    public void Can_load_2_layers_of_settings()
    {
        _externalLinksStorage.Load(Arg.Any<DistributedSettings>()).Returns(
            new List<ExternalLinkDefinition> { new() { Name = "local definition 1" } },
            new List<ExternalLinkDefinition>
            {
                new() { Name = "distributed definition 1" },
                new() { Name = "distributed definition 2" },
                new() { Name = "distributed definition 3" }
            });

        var effectiveSettings = _provider.Get(_repoDistributed);

        // 1 comes from the user roaming settings
        // 3 come from the distributed
        effectiveSettings.Should().HaveCount(4);
    }

    [Fact]
    public void Can_load_3_layers_of_settings()
    {
        _externalLinksStorage.Load(Arg.Any<DistributedSettings>()).Returns(
            new List<ExternalLinkDefinition> { new() { Name = "local definition 1" } },
            new List<ExternalLinkDefinition>
            {
                new() { Name = "distributed definition 1" },
                new() { Name = "distributed definition 2" },
                new() { Name = "distributed definition 3" }
            },
            new List<ExternalLinkDefinition> { new() { Name = "user definition 1" } });

        var effectiveSettings = _provider.Get(_repoLocal);

        // 1 comes from the user roaming settings
        // 3 come from the distributed
        // 1 comes from the local
        effectiveSettings.Should().HaveCount(5);
    }
}
