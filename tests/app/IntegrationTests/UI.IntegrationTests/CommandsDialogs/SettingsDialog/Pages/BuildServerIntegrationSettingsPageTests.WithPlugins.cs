using CommonTestUtils;
using CommonTestUtils.MEF;
using GitExtensions.Extensibility.Settings;
using GitUI.CommandsDialogs.SettingsDialog;
using GitUI.CommandsDialogs.SettingsDialog.Pages;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;
using Microsoft.VisualStudio.Composition;
using UITests.MEF;
using GitModule = GitCommands.Git.GitModule;

namespace UITests.CommandsDialogs.SettingsDialog.Pages;

public class BuildServerIntegrationSettingsPageTestsWithPlugins : IDisposable
{
    private readonly ReferenceRepository _referenceRepository = null!;
    private MockHost _form = null!;
    private BuildServerIntegrationSettingsPage _settingsPage = null!;

    public BuildServerIntegrationSettingsPageTestsWithPlugins()
    {
        _referenceRepository = new ReferenceRepository();
        var composition = TestComposition.Empty
            .AddParts(typeof(MockGenericBuildServerAdapter))
            .AddParts(typeof(MockGenericBuildServerSettingsUserControl));
        var mefExportProvider = composition.ExportProviderFactory.CreateExportProvider();
        ManagedExtensibility.SetTestExportProvider(mefExportProvider);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _settingsPage.Dispose();
        _form.Dispose();
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void BuildServerType_should_contain_discovered_build_server_plugins()
    {
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            _settingsPage.GetTestAccessor().BuildServerType.Items.Count.Should()
                .Be( /* default None + GenericBuildServerMock */2);
            _settingsPage.GetTestAccessor().BuildServerType.SelectedIndex.Should().Be(0);
            _settingsPage.GetTestAccessor().BuildServerType.Items[1].Should().Be("GenericBuildServerMock");
        });
    }

    [WinFormsFact]
    public void BuildServerType_should_toggle_plugin_settings_controls()
    {
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            // Default option, no custom control
            _settingsPage.GetTestAccessor().BuildServerSettingsPanel.Controls.Count.Should().Be(0);

            // Select the custom build server
            _settingsPage.GetTestAccessor().BuildServerType.SelectedIndex = 1;
            _settingsPage.GetTestAccessor().BuildServerSettingsPanel.Controls.Count.Should().Be(1);
            _settingsPage.GetTestAccessor().BuildServerSettingsPanel.Controls[0].Should()
                .BeAssignableTo<IBuildServerSettingsUserControl>();
        });
    }

    private void RunFormTest(Func<MockHost, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () =>
            {
                _form = new MockHost(_referenceRepository.Module) { Size = new Size(800, 400) };

                _settingsPage =
                    SettingsPageBase.Create<BuildServerIntegrationSettingsPage>(_form,
                        GitUICommands.EmptyServiceProvider);
                _settingsPage.Dock = DockStyle.Fill;

                _form.Controls.Add(_settingsPage);

                _form.ShowDialog(null);
            },
            testDriverAsync);
    }

    private class MockHost(GitModule module) : Form, ISettingsPageHost
    {
        CheckSettingsLogic ISettingsPageHost.CheckSettingsLogic { get; } = new(new CommonLogic(module));

        void ISettingsPageHost.GotoPage(SettingsPageReference settingsPageReference)
        {
            throw new NotImplementedException();
        }

        void ISettingsPageHost.LoadAll()
        {
            throw new NotImplementedException();
        }

        void ISettingsPageHost.SaveAll()
        {
            throw new NotImplementedException();
        }
    }
}
