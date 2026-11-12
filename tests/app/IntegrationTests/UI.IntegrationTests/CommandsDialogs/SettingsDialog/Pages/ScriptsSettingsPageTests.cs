using System.ComponentModel.Design;
using System.Reflection;
using CommonTestUtils;
using GitExtensions.Extensibility.Settings;
using GitExtUtils;
using GitUI.CommandsDialogs.SettingsDialog;
using GitUI.CommandsDialogs.SettingsDialog.Pages;
using GitUI.ScriptsEngine;
using NSubstitute;
using GitModule = GitCommands.Git.GitModule;

namespace UITests.CommandsDialogs.SettingsDialog.Pages;

public class ScriptsSettingsPageTests : IDisposable
{
    private readonly ReferenceRepository _referenceRepository = null!;
    private readonly ServiceContainer _serviceContainer = null!;
    private MockHost _form = null!;
    private ScriptsSettingsPage _settingsPage = null!;

    public ScriptsSettingsPageTests()
    {
        _referenceRepository = new ReferenceRepository();

        var scriptsManager = Substitute.For<IScriptsManager>();
        scriptsManager.GetScripts().Returns(
        [
            new ScriptInfo
            {
                HotkeyCommandIdentifier = 9001, Name = "first", Enabled = true, Icon = "StatusBadgeSuccess"
            },
            new ScriptInfo
            {
                HotkeyCommandIdentifier = 9002, Name = "second", Enabled = true, Icon = "StatusBadgeSuccess"
            }
        ]);

        _serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();
        _serviceContainer.RemoveService<IScriptsManager>();
        _serviceContainer.AddService(scriptsManager);
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _settingsPage.Dispose();
        _form.Dispose();
        _serviceContainer.Dispose();
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Committing_a_pending_grid_edit_while_rebinding_after_an_Icon_change_should_not_crash()
    {
        RunFormTest(async form =>
        {
            _settingsPage.LoadSettings();
            _settingsPage.OnPageShown();
            await UiTest.WaitForIdleAsync();

            var accessor = _settingsPage.GetTestAccessor();
            var propertyGrid = accessor.PropertyGrid1;
            propertyGrid.SelectedObject.Should().NotBeNull();

            var root = propertyGrid.SelectedGridItem!;
            while (root.Parent is not null)
            {
                root = root.Parent;
            }

            var iconItem = FindProperty(root, "Icon");
            var iconFilePathItem = FindProperty(root, "IconFilePath");

            // The user has typed into the grid, but the edit is not committed yet.
            // (In #12353 the pending edit was the item picked in the open Icon drop-down list.)
            iconFilePathItem.Select().Should().BeTrue();
            var gridView = propertyGrid.Controls.Cast<Control>()
                .Single(c => c.GetType().Name == "PropertyGridView");
            var editTextBox = gridView.Controls.OfType<TextBox>().Single();
            editTextBox.Visible.Should().BeTrue();
            editTextBox.Text = "pending.ico";

            // The grid commits a new Icon value and raises PropertyValueChanged, as it does on a cursor key in the drop-down list.
            // BindScripts must not make the grid commit the pending edit (which rebinds again) while it selects a list item.
            typeof(PropertyGrid)
                .GetMethod("OnPropertyValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(propertyGrid, [new PropertyValueChangedEventArgs(iconItem, null)]);

            await UiTest.WaitForIdleAsync();

            accessor.LvScripts.Items.Count.Should().Be(2);
            accessor.LvScripts.SelectedItems.Count.Should().Be(1);
        });
    }

    private static GridItem FindProperty(GridItem root, string name)
    {
        return Flatten(root).First(item =>
            item.GridItemType == GridItemType.Property && item.PropertyDescriptor?.Name == name);
    }

    private static IEnumerable<GridItem> Flatten(GridItem item)
    {
        yield return item;

        foreach (GridItem child in item.GridItems)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private void RunFormTest(Func<MockHost, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () =>
            {
                _form = new MockHost(_referenceRepository.Module) { Size = new Size(800, 600) };

                _settingsPage = SettingsPageBase.Create<ScriptsSettingsPage>(_form, _serviceContainer);
                _settingsPage.Dock = DockStyle.Fill;

                _form.Controls.Add(_settingsPage);

                _form.ShowDialog(null);
            },
            testDriverAsync);
    }

    private sealed class MockHost(GitModule module) : Form, ISettingsPageHost
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
