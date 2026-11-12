using System.Globalization;
using CommonTestUtils;
using GitCommands.Config;
using GitCommands.Remotes;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Remote;

public class ConfigFileRemoteSettingsManagerTests : IDisposable
{
    private readonly CultureInfo _currentCulture = null!;
    private readonly CultureInfo _currentUiCulture = null!;
    private readonly IConfigFileRemoteSettingsManager _remotesManager = null!;
    private IGitModule _module = null!;

    public ConfigFileRemoteSettingsManagerTests()
    {
        _currentCulture = CultureInfo.CurrentCulture;
        _currentUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = new CultureInfo("en-US");
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        _module = Substitute.For<IGitModule>();

        _remotesManager = new ConfigFileRemoteSettingsManager(() => _module);
    }

    void IDisposable.Dispose()
    {
        CultureInfo.CurrentCulture = _currentCulture;
        CultureInfo.CurrentUICulture = _currentUiCulture;
    }

    [Fact]
    public void LoadRemotes_should_not_throw_if_module_is_null()
    {
        _module = null!;

        ((Action)(() => _remotesManager.LoadRemotes(true))).Should().NotThrow();
    }

    [Fact]
    public void LoadRemotes_should_not_populate_remotes_if_none()
    {
        _module.GetRemoteNames().Returns(x => []);

        var remotes = _remotesManager.LoadRemotes(true);

        remotes.Should().BeEmpty();
        _module.Received(1).GetRemoteNames();
        _module.DidNotReceive().GetSetting(Arg.Any<string>());
        _module.DidNotReceive().GetSettings(Arg.Any<string>());
    }

    [Fact]
    public void LoadRemotes_should_not_populate_remotes_if_those_are_null_or_whitespace()
    {
        _module.GetRemoteNames().Returns(x => new[] { null!, "", " ", "    ", "\t" });

        var remotes = _remotesManager.LoadRemotes(true);

        remotes.Should().BeEmpty();
        _module.Received(1).GetRemoteNames();
        _module.DidNotReceive().GetSetting(Arg.Any<string>());
        _module.DidNotReceive().GetSettings(Arg.Any<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadRemotes_should_populate_remotes_if_any(bool loadDisabled)
    {
        const string remoteName1 = "name1";
        const string remoteName2 = "name2";
        _module.GetRemoteNames().Returns(x => new[] { null!, "", " ", "    ", remoteName1, "\t" });
        (string Setting, string Value)[] settings =
        [
            ($"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{ConfigFileRemoteSettingsManager.SectionRemote}.{remoteName2}.dummy-name",
                "dummy value")
        ];
        _module.GetAllLocalSettings().Returns(x => settings);

        var remotes = _remotesManager.LoadRemotes(loadDisabled);

        remotes.Should().HaveCount(loadDisabled ? 2 : 1);

        _module.Received(1).GetRemoteNames();
        _module.Received(1).GetSetting(string.Format(SettingKeyString.RemoteUrl, remoteName1));
        _module.Received(1).GetSetting(string.Format(SettingKeyString.RemotePushUrl, remoteName1));
        _module.Received(1).GetSetting(string.Format(SettingKeyString.RemotePuttySshKey, remoteName1));
        _module.Received(1).GetSettings(string.Format(SettingKeyString.RemotePush, remoteName1));

        var count = loadDisabled ? 1 : 0;
        _module.Received(count).GetAllLocalSettings();
        _module.Received(count).GetSetting(ConfigFileRemoteSettingsManager.DisabledSectionPrefix +
                                           string.Format(SettingKeyString.RemoteUrl, remoteName2));
        _module.Received(count).GetSetting(ConfigFileRemoteSettingsManager.DisabledSectionPrefix +
                                           string.Format(SettingKeyString.RemotePushUrl, remoteName2));
        _module.Received(count).GetSetting(ConfigFileRemoteSettingsManager.DisabledSectionPrefix +
                                           string.Format(SettingKeyString.RemotePuttySshKey, remoteName2));
        _module.Received(count).GetSettings(ConfigFileRemoteSettingsManager.DisabledSectionPrefix +
                                            string.Format(SettingKeyString.RemotePush, remoteName2));
    }

    [Fact]
    public void RemoveRemote_should_throw_if_remote_is_null()
    {
        ((Action)(() => _remotesManager.RemoveRemote(null!))).Should().Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'remote')");
    }

    [Fact]
    public void RemoveRemote_success()
    {
        ConfigFileRemote remote = new() { Name = "bla" };

        _remotesManager.RemoveRemote(remote);

        _module.Received(1).RemoveRemote(remote.Name);
    }

    [Fact]
    public void SaveRemote_should_throw_if_remoteName_is_null_or_empty()
    {
        ((Action)(() => _remotesManager.SaveRemote(null, null!, "b", "c", "d", "e", "f"))).Should()
            .Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'remoteName')");
        ((Action)(() => _remotesManager.SaveRemote(null, "", "b", "c", "d", "e", "f"))).Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'remoteName')");
        ((Action)(() => _remotesManager.SaveRemote(null, "  ", "b", "c", "d", "e", "f"))).Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "The value cannot be an empty string or composed entirely of whitespace. (Parameter 'remoteName')");
    }

    [Fact]
    public void SaveRemote_null_remote_should_invoke_AddRemote_and_require_update()
    {
        const string remoteName = "a";
        const string remoteUrl = "b";
        const string output = "";
        _module.AddRemote(Arg.Any<string>(), Arg.Any<string>()).Returns(x => output);

        var result =
            _remotesManager.SaveRemote(null, remoteName, remoteUrl, null, null!, null, null);

        result.UserMessage.Should().Be(output);
        result.ShouldUpdateRemote.Should().BeTrue();
        _module.Received(1).AddRemote(remoteName, remoteUrl);
    }

    [Fact]
    public void SaveRemote_null_remote_should_set_settings()
    {
        const string remoteName = "a";
        const string remoteUrl = "b";
        const string remotePushUrl = "c";
        const string remotePuttySshKey = "";
        const string remoteColor = "";
        const string remotePrefix = "";
        const string output = "";
        _module.AddRemote(Arg.Any<string>(), Arg.Any<string>()).Returns(x => output);

        var result = _remotesManager.SaveRemote(null, remoteName, remoteUrl, remotePushUrl,
            remotePuttySshKey, remoteColor, remotePrefix);

        result.UserMessage.Should().Be(output);
        result.ShouldUpdateRemote.Should().BeTrue();
        _module.Received(1).SetSetting(string.Format(SettingKeyString.RemoteUrl, remoteName), remoteUrl);
        _module.Received(1).SetSetting(string.Format(SettingKeyString.RemotePushUrl, remoteName), remotePushUrl);
        _module.Received(1).UnsetSetting(string.Format(SettingKeyString.RemotePuttySshKey, remoteName));
        _module.Received(1).UnsetSetting(string.Format(SettingKeyString.RemoteColor, remoteName));
    }

    [Fact]
    public void SaveRemote_populated_remote_should_invoke_RenameRemote_if_remoteName_mismatch_no_update_required()
    {
        const string remoteName = "a";
        const string remoteUrl = "b";
        const string output = "yes!";
        ConfigFileRemote gitRemote = new() { Name = "old", Url = remoteUrl };
        _module.RenameRemote(Arg.Any<string>(), Arg.Any<string>()).Returns(x => output);

        var result =
            _remotesManager.SaveRemote(gitRemote, remoteName, remoteUrl, null, null!, null, null);

        result.UserMessage.Should().Be(output);
        result.ShouldUpdateRemote.Should().BeFalse();
        _module.Received(1).RenameRemote(gitRemote.Name, remoteName);
    }

    [Fact]
    public void SaveRemote_populated_remote_should_require_update_if_remoteUrl_mismatch()
    {
        const string remoteName = "a";
        const string remoteUrl = "b";
        const string output = "yes!";
        ConfigFileRemote gitRemote = new() { Name = "old", Url = "old" };
        _module.RenameRemote(Arg.Any<string>(), Arg.Any<string>()).Returns(x => output);

        var result =
            _remotesManager.SaveRemote(gitRemote, remoteName, remoteUrl, null, null!, null, null);

        result.UserMessage.Should().Be(output);
        result.ShouldUpdateRemote.Should().BeTrue();
        _module.Received(1).RenameRemote(gitRemote.Name, remoteName);
    }

    [Theory]
    [InlineData(null, null, null, null, null)]
    [InlineData("a", null, null, null, null)]
    [InlineData("a", "b", null, null, null)]
    [InlineData("a", "b", "c", null, null)]
    [InlineData("a", "b", "c", "d", null)]
    [InlineData("a", "b", "c", "d", "e")]
    public void SaveRemote_should_update_settings(string? remoteUrl, string? remotePushUrl, string? remotePuttySshKey,
        string? remoteColor, string? remotePrefix)
    {
        ConfigFileRemote remote = new() { Name = "bla", Url = remoteUrl };

        _remotesManager.SaveRemote(remote, remote.Name, remoteUrl!, remotePushUrl, remotePuttySshKey!, remoteColor,
            remotePrefix);

        void Ensure(string setting, string value)
        {
            setting = string.Format(setting, remote.Name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                _module.Received(1).SetSetting(setting, value);
            }
            else
            {
                _module.Received(1).UnsetSetting(setting);
            }
        }

        Ensure(SettingKeyString.RemoteUrl, remoteUrl!);
        Ensure(SettingKeyString.RemotePushUrl, remotePushUrl!);
        Ensure(SettingKeyString.RemotePuttySshKey, remotePuttySshKey!);
    }

    [Fact]
    public void SetRemoteState_should_throw_if_remote_is_null()
    {
        ((Action)(() => _remotesManager.ToggleRemoteState(null!, false))).Should().Throw<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'remoteName')");
    }

    [Fact]
    public void SetRemoteState_should_do_nothing_if_section_not_found()
    {
        _module.GetAllLocalSettings().Returns(x => []);

        _remotesManager.ToggleRemoteState("boo", false);

        _module.Received(1).GetAllLocalSettings();
        _module.DidNotReceive().RemoveRemote(Arg.Any<string>());
        _module.DidNotReceive().RemoveConfigSection(Arg.Any<string>());
    }

    [Theory]
    [InlineData("name1", false)]
    [InlineData("name2", true)]
    public void SetRemoteState_should_call_ToggleRemoteState(string remoteName, bool remoteDisabled)
    {
        (string Setting, string Value)[] settings =
            [("-remote.name1.dummy-name1", "dummy value 1"), ("remote.name2.dummy-name2", "dummy value 2")];
        _module.GetAllLocalSettings().Returns(x => settings);

        _remotesManager.ToggleRemoteState(remoteName, remoteDisabled);

        _module.Received(1).GetAllLocalSettings();
        _module.Received(remoteDisabled ? 1 : 0).RemoveRemote(remoteName);
        _module.Received(remoteDisabled ? 0 : 1).RemoveConfigSection(
            $"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{ConfigFileRemoteSettingsManager.SectionRemote}",
            remoteName);

        (var setting, var value) = settings[remoteDisabled ? 1 : 0];
        setting = remoteDisabled
            ? $"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{setting}"
            : setting.TrimStart(ConfigFileRemoteSettingsManager.DisabledSectionPrefix[0]);
        _module.Received(1).SetSetting(setting, value, true);
    }

    [Fact]
    public void GetDisabledRemotes_returns_disabled_remotes_only()
    {
        var enabledRemoteName = "enabledRemote";
        var disabledRemoteName = "disabledRemote";

        _module.GetRemoteNames().Returns(x => new[] { enabledRemoteName });

        (string Setting, string Value)[] settings =
        [
            ($"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{ConfigFileRemoteSettingsManager.SectionRemote}.{disabledRemoteName}.dummy-name",
                "dummy value")
        ];
        _module.GetAllLocalSettings().Returns(x => settings);

        var disabledRemotes = _remotesManager.GetDisabledRemotes();
        disabledRemotes.Count.Should().Be(1);
        disabledRemotes[0].Name.Should().Be(disabledRemoteName);

        var disabledRemoteNames = _remotesManager.GetDisabledRemoteNames();
        disabledRemoteNames.Count.Should().Be(1);
        disabledRemoteNames[0].Should().Be(disabledRemoteName);
    }

    [Fact]
    public void GetEnabledRemoteNames_returns_enabled_remotes_only()
    {
        var enabledRemoteName = "enabledRemote";
        var disabledRemoteName = "disabledRemote";

        _module.GetRemoteNames().Returns(x => new[] { enabledRemoteName });

        (string Setting, string Value)[] settings =
        [
            ($"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{ConfigFileRemoteSettingsManager.SectionRemote}.{disabledRemoteName}.dummy-name",
                "dummy value")
        ];
        _module.GetAllLocalSettings().Returns(x => settings);

        var enabledRemoteNames = _remotesManager.GetEnabledRemoteNames();
        enabledRemoteNames.Count.Should().Be(1);
        enabledRemoteNames[0].Should().Be(enabledRemoteName);
    }

    [Fact]
    public void EnabledRemoteExists_returns_true_for_enabled_remotes_only()
    {
        var enabledRemoteName = "enabledRemote";
        var disabledRemoteName = "disabledRemote";

        _module.GetRemoteNames().Returns(x => new[] { enabledRemoteName });

        (string Setting, string Value)[] settings =
        [
            ($"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{ConfigFileRemoteSettingsManager.SectionRemote}.{disabledRemoteName}.dummy-name",
                "dummy value")
        ];
        _module.GetAllLocalSettings().Returns(x => settings);

        _remotesManager.EnabledRemoteExists(enabledRemoteName).Should().BeTrue();
        _remotesManager.EnabledRemoteExists(disabledRemoteName).Should().BeFalse();
    }

    [Fact]
    public void DisabledRemoteExists_returns_true_for_disabled_remotes_only()
    {
        var enabledRemoteName = "enabledRemote";
        var disabledRemoteName = "disabledRemote";

        _module.GetRemoteNames().Returns(x => new[] { enabledRemoteName });

        (string Setting, string Value)[] settings =
        [
            ($"{ConfigFileRemoteSettingsManager.DisabledSectionPrefix}{ConfigFileRemoteSettingsManager.SectionRemote}.{disabledRemoteName}.dummy-name",
                "dummy value")
        ];
        _module.GetAllLocalSettings().Returns(x => settings);

        _remotesManager.DisabledRemoteExists(disabledRemoteName).Should().BeTrue();
        _remotesManager.DisabledRemoteExists(enabledRemoteName).Should().BeFalse();
    }

    [Fact]
    public void GetDefaultPushRemote_returns_null_for_no_remote_branch()
    {
        ConfigFileRemote remote = new() { Push = [] };
        _remotesManager.GetDefaultPushRemote(remote, "BranchName").Should().BeNull();
    }

    [Theory]
    [InlineData("BranchName", "BranchName")]
    [InlineData("BranchName", "dev/me/BranchName")]
    public void GetDefaultPushRemote_returns_value_for_mapped_remote_branch(string branchName, string pushToBranch)
    {
        ConfigFileRemote remote = new() { Push = [$"refs/heads/{branchName}:refs/heads/{pushToBranch}"] };
        _remotesManager.GetDefaultPushRemote(remote, branchName).Should().Be(pushToBranch);
    }

    [Theory]
    [InlineData("BranchName", "*")]
    [InlineData("BranchName", "dev/me/*")]
    public void GetDefaultPushRemote_returns_value_for_wildcard_mapped_remote_branch(string branchName,
        string pushToBranch)
    {
        ConfigFileRemote remote = new() { Push = [$"refs/heads/*:refs/heads/{pushToBranch}"] };
        _remotesManager.GetDefaultPushRemote(remote, branchName).Should().Be(pushToBranch.Replace("*", branchName));
    }

    public class IntegrationTests
    {
        [Fact]
        public void ToggleRemoteState_should_not_fail_if_activate_repeatedly()
        {
            using GitModuleTestHelper helper = new();
            ConfigFileRemoteSettingsManager manager = new(() => helper.Module);

            const string remoteName = "active";
            helper.Module.AddRemote(remoteName, "http://localhost/remote/repo.git");
            manager.ToggleRemoteState(remoteName, true);

            helper.Module.AddRemote(remoteName, "http://localhost/remote/repo.git");
            manager.ToggleRemoteState(remoteName, false);

            helper.Module.GetEffectiveSetting("reload now");
            helper.Module.GetSettings("reload local settings, too");
        }

        [Fact]
        public void ToggleRemoteState_should_not_fail_if_deactivate_repeatedly()
        {
            using GitModuleTestHelper helper = new();
            ConfigFileRemoteSettingsManager manager = new(() => helper.Module);

            const string remoteName = "active";
            helper.Module.AddRemote(remoteName, "http://localhost/remote/repo.git");
            manager.ToggleRemoteState(remoteName, true);

            helper.Module.AddRemote(remoteName, "http://localhost/remote/repo.git");
            manager.ToggleRemoteState(remoteName, true);

            helper.Module.GetEffectiveSetting("reload now");
            helper.Module.GetSettings("reload local settings, too");
        }
    }
}
