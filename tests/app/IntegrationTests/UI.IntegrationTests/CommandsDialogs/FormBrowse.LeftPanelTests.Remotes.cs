using CommonTestUtils;
using GitCommands.Remotes;
using GitCommands.Settings;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls;

namespace UITests.CommandsDialogs;

public class FormBrowseLeftPanelRemotesTests : IDisposable, IClassFixture<FormBrowseLeftPanelRemotesTestsFixture>
{
    private static readonly string[] _remoteNames = ["remote1", "remote5", "remote3", "remote4", "remote2"];

    // Created once for each test
    private GitUiCommands _commands = null!;

    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;
    private IConfigFileRemoteSettingsManager _remotesManager = null!;

    public FormBrowseLeftPanelRemotesTests(FormBrowseLeftPanelRemotesTestsFixture fixture)
    {
        InitializeTest();
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void InitializeTest()
    {
        _referenceRepository = new ReferenceRepository();

        foreach (var name in _remoteNames)
        {
            _referenceRepository.Module.AddRemote(name, $"http://localhost/remotes/{name}.git");
        }

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);
        _remotesManager = new ConfigFileRemoteSettingsManager(() => _referenceRepository.Module);
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void RepoObjectTree_should_load_active_remotes()
    {
        RunRepoObjectsTreeTest(remotesNode =>
        {
            // act
            // no-op: by the virtue of loading the form, the left panel has loaded its content

            // assert
            remotesNode.Nodes.Count.Should().Be(_remoteNames.Length);
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_should_order_active_remotes_alphabetically()
    {
        RunRepoObjectsTreeTest(remotesNode =>
        {
            // act
            // no-op: by the virtue of loading the form, the left panel has loaded its content

            // assert
            List<string> names = [.. remotesNode.Nodes.OfType<TreeNode>().Select(x => x.Text)];
            names.Should().BeEquivalentTo(_remoteNames);
            names.Should().BeInAscendingOrder();
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_should_order_should_not_display_inactive_if_none()
    {
        RunRepoObjectsTreeTest(remotesNode =>
        {
            // act
            // no-op: by the virtue of loading the form, the left panel has loaded its content

            // assert
            remotesNode.Nodes.OfType<TreeNode>().Any(n => n.Text == TranslatedStrings.Inactive).Should().BeFalse();
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_should_order_should_display_inactive_if_present()
    {
        // setup
        DeactivateTreeNode(_remoteNames[1]);

        RunRepoObjectsTreeTest(remotesNode =>
        {
            // act
            // no-op: by the virtue of loading the form, the left panel has loaded its content

            // assert
            remotesNode.Nodes.OfType<TreeNode>().Count(n => n.Text == TranslatedStrings.Inactive).Should().Be(1);
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_should_order_should_display_inactive_after_active_nodes()
    {
        // setup
        DeactivateTreeNode(_remoteNames[1]);

        RunRepoObjectsTreeTest(remotesNode =>
        {
            // act
            // no-op: by the virtue of loading the form, the left panel has loaded its content

            // assert
            remotesNode.Nodes.OfType<TreeNode>().Last().Text.Should().Be(TranslatedStrings.Inactive);
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_should_order_inactive_remotes_alphabetically()
    {
        // setup
        DeactivateTreeNode(_remoteNames[3]);
        DeactivateTreeNode(_remoteNames[0]);
        DeactivateTreeNode(_remoteNames[1]);

        RunRepoObjectsTreeTest(remotesNode =>
        {
            // act
            // no-op: by the virtue of loading the form, the left panel has loaded its content

            // assert
            List<string> inactiveNodes =
                [.. remotesNode.Nodes.OfType<TreeNode>().Last().Nodes.OfType<TreeNode>().Select(n => n.Text)];
            inactiveNodes.Should().HaveCount(3);
            inactiveNodes.Should().BeEquivalentTo(_remoteNames[3], _remoteNames[0], _remoteNames[1]);
            inactiveNodes.Should().BeInAscendingOrder();
        });
    }

    private void DeactivateTreeNode(string nodeText)
    {
        _remotesManager.ToggleRemoteState(nodeText, true);
    }

    private static TreeNode GetRemoteNode(FormBrowse form)
    {
        form.MainSplitContainer.Panel1Collapsed.Should().BeFalse();

        // Await all async operation such as load of branches and remotes in the left panel
        AsyncTestHelper.JoinPendingOperations();

        var treeView = form.GetTestAccessor().RepoObjectsTree.GetTestAccessor().TreeView;
        var remotesNode =
            treeView.Nodes.OfType<TreeNode>().FirstOrDefault(n => n.Text == TranslatedStrings.Remotes);
        remotesNode.Should().NotBeNull();

        return remotesNode;
    }

    private void RunRepoObjectsTreeTest(Action<TreeNode> testDriver)
    {
        RunFormTest(form =>
        {
            testDriver(GetRemoteNode(form));
            return Task.CompletedTask;
        });
    }

    private void RunFormTest(Func<FormBrowse, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () => _commands.StartBrowseDialog(owner: null).Should().BeTrue(),
            testDriverAsync);
    }
}

public sealed class FormBrowseLeftPanelRemotesTestsFixture : IDisposable
{
    // Track the original setting value
    private readonly bool _originalShowAuthorAvatarColumn;
    private readonly bool _showAvailableDiffTools;

    public FormBrowseLeftPanelRemotesTestsFixture()
    {
        // Remember the current settings...
        _originalShowAuthorAvatarColumn = AppSettings.ShowAuthorAvatarColumn;
        _showAvailableDiffTools = AppSettings.ShowAvailableDiffTools;

        // Stop loading custom diff tools
        AppSettings.ShowAvailableDiffTools = false;

        // We don't want avatars during tests, otherwise we will be attempting to download them from gravatar....
        AppSettings.ShowAuthorAvatarColumn = false;
    }

    void IDisposable.Dispose()
    {
        AppSettings.ShowAuthorAvatarColumn = _originalShowAuthorAvatarColumn;
        AppSettings.ShowAvailableDiffTools = _showAvailableDiffTools;
    }
}
