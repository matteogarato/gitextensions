using CommonTestUtils;
using CommonTestUtils.MEF;
using GitExtensions.Extensibility.Settings;
using GitUI.CommandsDialogs.SettingsDialog;
using GitUI.CommandsDialogs.SettingsDialog.Pages;
using GitUIPluginInterfaces;
using Microsoft.VisualStudio.Composition;
using GitModule = GitCommands.Git.GitModule;

namespace UITests.CommandsDialogs.SettingsDialog.Pages;

public class BuildServerIntegrationSettingsPageTestsNoPlugins : IDisposable
{
    // Created once for the fixture
    private readonly ReferenceRepository _referenceRepository = null!;
    private MockHost _form = null!;
    private BuildServerIntegrationSettingsPage _settingsPage = null!;

    public BuildServerIntegrationSettingsPageTestsNoPlugins()
    {
        _referenceRepository = new ReferenceRepository();
        var mefExportProvider = TestComposition.Empty.ExportProviderFactory.CreateExportProvider();
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
    public void BuildServerType_should_contain_only_None_if_not_build_server_plugins_found()
    {
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            _settingsPage.GetTestAccessor().BuildServerType.Items.Count.Should().Be(1);
            _settingsPage.GetTestAccessor().BuildServerType.SelectedIndex.Should().Be(0);
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
