using System.ComponentModel.Design;
using CommonTestUtils;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace UITests.CommandsDialogs;

public class FormRemotesTests : IDisposable
{
    private readonly GitUiCommands _commands = null!;
    private readonly bool _originalAlwaysShowAdvOpt;
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormRemotesTests()
    {
        _originalAlwaysShowAdvOpt = AppSettings.AlwaysShowAdvOpt;

        _referenceRepository = new ReferenceRepository();

        var serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();

        _commands = new GitUiCommands(serviceContainer, _referenceRepository.Module);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        AppSettings.AlwaysShowAdvOpt = _originalAlwaysShowAdvOpt;
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Should_display_remotes_tab_by_default()
    {
        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();
            accessor.TabControl.SelectedTab.Should().NotBeNull();
            accessor.TabControl.SelectedIndex.Should().Be(0);
        });
    }

    [WinFormsFact]
    public void Should_stay_on_remotes_tab_when_preselecting_local_without_remotes()
    {
        // Tab switch to "Default pull behaviors" only occurs when remotes are configured
        RunFormTest(
            form =>
            {
                var accessor = form.GetTestAccessor();
                accessor.TabControl.SelectedIndex.Should().Be(0);
            },
            preselectLocal: "master");
    }

    [WinFormsFact]
    public void Should_enable_save_button_when_remote_name_is_entered()
    {
        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();

            accessor.RemoteName.Text = "origin";
            accessor.Save.Enabled.Should().BeTrue();

            accessor.RemoteName.Text = string.Empty;
            accessor.Save.Enabled.Should().BeFalse();
        });
    }

    [WinFormsFact]
    public void Should_disable_delete_and_toggle_buttons_when_no_remotes_configured()
    {
        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();
            accessor.Delete.Enabled.Should().BeFalse();
            accessor.ToggleState.Enabled.Should().BeFalse();
        });
    }

    [WinFormsFact]
    public void Should_show_empty_remote_name_when_no_remotes_configured()
    {
        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();
            accessor.RemoteName.Text.Should().BeEmpty();
        });
    }

    [WinFormsFact]
    public void Should_normalise_remote_prefix_on_leave()
    {
        AppSettings.AlwaysShowAdvOpt = true;

        var branchNameNormaliser = _commands.GetRequiredService<IGitBranchNameNormaliser>();
        branchNameNormaliser.Normalise("invalid branch prefix/", Arg.Any<GitBranchNameOptions>())
            .Returns("invalid-branch-prefix/");

        RunFormTest(form =>
        {
            var accessor = form.GetTestAccessor();

            accessor.RemotePrefix.Focus();
            accessor.RemotePrefix.Text = "invalid branch prefix/";
            accessor.RemoteName.Focus();

            accessor.RemotePrefix.Text.Should().Be("invalid-branch-prefix/");
        });
    }

    private void RunFormTest(Action<FormRemotes> testDriver, string? preselectRemote = null,
        string? preselectLocal = null)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            },
            preselectRemote,
            preselectLocal);
    }

    private void RunFormTest(Func<FormRemotes, Task> testDriverAsync, string? preselectRemote = null,
        string? preselectLocal = null)
    {
        UiTest.RunForm(
            () =>
            {
                _commands.StartRemotesDialog(owner: null, preselectRemote: preselectRemote,
                    preselectLocal: preselectLocal);
            },
            testDriverAsync);
    }
}
