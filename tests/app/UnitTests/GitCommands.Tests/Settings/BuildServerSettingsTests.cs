using System.Reflection;
using CommonTestUtils;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;

namespace GitCommandsTests.Settings;

public sealed class BuildServerSettingsTests : IDisposable
{
    private readonly DistributedSettings _effective = null!;
    private readonly DistributedSettings _repoDistributed = null!;
    private readonly string _repoDistributedConfigFilePath = null!;
    private readonly DistributedSettings _repoLocal = null!;
    private readonly string _repoLocalConfigFilePath = null!;
    private readonly GitModuleTestHelper _testHelper = null!;
    private readonly DistributedSettings _userRoaming = null!;
    private readonly string _userRoamingConfigFilePath = null!;

    public BuildServerSettingsTests()
    {
        _testHelper = new GitModuleTestHelper();

        var content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level3_roaming_GitExtensions.settings.xml");
        _userRoamingConfigFilePath = _testHelper.CreateFile(_testHelper.TemporaryPath + "/RoamingProfile",
            "GitExtensions.settings", content);
        content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level2_repodist_GitExtensions.settings.xml");
        _repoDistributedConfigFilePath = _testHelper.CreateRepoFile("GitExtensions.settings", content);
        content = EmbeddedResourceLoader.Load(Assembly.GetExecutingAssembly(),
            $"{GetType().Namespace}.MockData.level1_repogit_GitExtensions.settings.xml");
        _repoLocalConfigFilePath = _testHelper.CreateRepoFile(".git", "GitExtensions.settings", content);

        _userRoaming = new DistributedSettings(null, new GitExtSettingsCache(_userRoamingConfigFilePath),
            SettingLevel.Global);
        _repoDistributed = new DistributedSettings(_userRoaming,
            new GitExtSettingsCache(_repoDistributedConfigFilePath), SettingLevel.Distributed);
        _repoLocal = new DistributedSettings(_repoDistributed, new GitExtSettingsCache(_repoLocalConfigFilePath),
            SettingLevel.Local);
        _effective = new DistributedSettings(_repoLocal, new GitExtSettingsCache(null!), SettingLevel.Effective);
    }

    void IDisposable.Dispose()
    {
        _userRoaming.SettingsCache.Dispose();
        _repoDistributed.SettingsCache.Dispose();
        _repoLocal.SettingsCache.Dispose();

        _testHelper.Dispose();
    }

    [Fact]
    public void UserRoaming_settings_should_return_expected()
    {
        // Explicitly set
        BuildServerSettings.ServerName[_userRoaming].Should()
            .Be("Azure DevOps and Team Foundation Server (since TFS2015)");
        BuildServerSettings.IntegrationEnabled[_userRoaming].Should().BeTrue();
        BuildServerSettings.ShowBuildResultPage[_userRoaming].Should().BeNull();

        // Explicitly set
        var settingsSource = BuildServerSettings.GetSettingsSource(_userRoaming);
        settingsSource.Should().BeOfType<SettingsPath>();
        ((SettingsPath)settingsSource).PathFor("").Should()
            .Be("BuildServer.Azure DevOps and Team Foundation Server (since TFS2015).");
    }

    [Fact]
    public void RepoDistributed_settings_should_return_expected()
    {
        // No explicit settings, inheriting from the repo local
        BuildServerSettings.ServerName[_repoLocal].Should().Be("AppVeyor");
        BuildServerSettings.IntegrationEnabled[_repoLocal].Should().BeTrue();

        // Explicitly set
        BuildServerSettings.ShowBuildResultPage[_repoLocal].Should().BeFalse();

        // No explicit settings, inheriting from the repo local
        var settingsSource = BuildServerSettings.GetSettingsSource(_repoLocal);
        settingsSource.Should().BeOfType<SettingsPath>();
        ((SettingsPath)settingsSource).PathFor("").Should().Be("BuildServer.AppVeyor.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RepoLocalAndEffective_settings_should_return_expected(bool isLocal)
    {
        SettingsSource source = isLocal ? _repoLocal : _effective;
        BuildServerSettings.ServerName[source].Should().Be("AppVeyor");
        BuildServerSettings.IntegrationEnabled[source].Should().BeTrue();

        // No explicit settings, inheriting from the repo distributed
        BuildServerSettings.ShowBuildResultPage[source].Should().BeFalse();

        var settingsSource = BuildServerSettings.GetSettingsSource(source);
        settingsSource.Should().BeOfType<SettingsPath>();
        ((SettingsPath)settingsSource).PathFor("").Should().Be("BuildServer.AppVeyor.");
    }
}
