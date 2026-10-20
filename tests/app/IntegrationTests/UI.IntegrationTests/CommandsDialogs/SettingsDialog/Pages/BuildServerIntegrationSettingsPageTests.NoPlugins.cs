using CommonTestUtils;
using CommonTestUtils.MEF;
using GitCommands;
using GitExtensions.Extensibility.Settings;
using GitExtensions.UITests;
using GitUI;
using GitUI.CommandsDialogs.SettingsDialog;
using GitUI.CommandsDialogs.SettingsDialog.Pages;
using GitUIPluginInterfaces;
using Microsoft.VisualStudio.Composition;
using Xunit;

namespace UITests.CommandsDialogs.SettingsDialog.Pages;

public class BuildServerIntegrationSettingsPageTests_NoPlugins : System.IDisposable
{
    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;
    private MockHost _form = null!;
    private BuildServerIntegrationSettingsPage _settingsPage = null!;
    public BuildServerIntegrationSettingsPageTests_NoPlugins()
    {
        _referenceRepository = new ReferenceRepository();
        ExportProvider mefExportProvider = TestComposition.Empty.ExportProviderFactory.CreateExportProvider();
        ManagedExtensibility.SetTestExportProvider(mefExportProvider);
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
        RunFormTest(
          async form =>
          {
              await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

              _settingsPage.GetTestAccessor().BuildServerType.Items.Count.Should().Be(1);
              _settingsPage.GetTestAccessor().BuildServerType.SelectedIndex.Should().Be(0);
          });
    }

    private void RunFormTest(Func<MockHost, Task> testDriverAsync)
    {
        UITest.RunForm(
            () =>
            {
                _form = new MockHost(_referenceRepository.Module)
                {
                    Size = new(800, 400)
                };

                _settingsPage = SettingsPageBase.Create<BuildServerIntegrationSettingsPage>(_form, GitUICommands.EmptyServiceProvider);
                _settingsPage.Dock = DockStyle.Fill;

                _form.Controls.Add(_settingsPage);

                _form.ShowDialog(owner: null);
            },
            testDriverAsync);
    }

    private class MockHost : Form, ISettingsPageHost
    {
        public MockHost(GitModule module)
        {
            CheckSettingsLogic = new(new(module));
        }

        public CheckSettingsLogic CheckSettingsLogic { get; }

        public void GotoPage(SettingsPageReference settingsPageReference)
        {
            throw new NotImplementedException();
        }

        public void LoadAll()
        {
            throw new NotImplementedException();
        }

        public void SaveAll()
        {
            throw new NotImplementedException();
        }
    }

    public void Dispose()
    {
        DisposeTest();
    }
}
