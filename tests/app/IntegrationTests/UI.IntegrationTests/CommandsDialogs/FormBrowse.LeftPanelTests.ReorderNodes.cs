using System.Collections;
using CommonTestUtils;
using GitCommands.Settings;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.LeftPanel;

namespace UITests.CommandsDialogs;

public class FormBrowseLeftPanelReorderNodesTest : IDisposable,
    IClassFixture<FormBrowseLeftPanelReorderNodesTestFixture>
{
    // Created once for each test
    private GitUiCommands _commands = null!;

    private GitModuleTestHelper _repo1 = null!;

    public FormBrowseLeftPanelReorderNodesTest(FormBrowseLeftPanelReorderNodesTestFixture fixture)
    {
        InitializeTest();
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void InitializeTest()
    {
        _repo1 = new GitModuleTestHelper();

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(), _repo1.Module);
    }

    private void DisposeTest()
    {
        _repo1.Dispose();
    }

    [WinFormsFact]
    public void RepoObjectTree_moving_first_up_and_last_down_does_nothing()
    {
        RunRepoObjectsTreeTest(repoObjectTree =>
        {
            var testAccessor = repoObjectTree.GetTestAccessor();

            // act
            var currNodes = testAccessor.TreeView.Nodes;
            List<TreeNode> initialNodes = [.. currNodes.OfType<TreeNode>()];

            // assert
            AssertListCount(currNodes, 6);
            ValidateOrder(initialNodes, currNodes, 0, 1, 2, 3, 4, 5);

            var first = 0;
            var last = currNodes.Count - 1;

            // Trying to move first node up should do nothing
            testAccessor.ReorderTreeNode(currNodes[first], true);
            ValidateOrder(initialNodes, currNodes, 0, 1, 2, 3, 4, 5);

            // Similarly, moving last node down should do nothing
            testAccessor.ReorderTreeNode(currNodes[last], false);
            ValidateOrder(initialNodes, currNodes, 0, 1, 2, 3, 4, 5);
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_moving_node_legally_moves_it()
    {
        RunRepoObjectsTreeTest(repoObjectTree =>
        {
            var testAccessor = repoObjectTree.GetTestAccessor();

            // act
            var currNodes = testAccessor.TreeView.Nodes;
            List<TreeNode> initialNodes = [.. currNodes.OfType<TreeNode>()];

            // assert
            AssertListCount(currNodes, 6);
            ValidateOrder(initialNodes, currNodes, 0, 1, 2, 3, 4, 5);

            // Move first down
            testAccessor.ReorderTreeNode(currNodes[0], false);
            ValidateOrder(initialNodes, currNodes, 1, 0, 2, 3, 4, 5);
            testAccessor.ReorderTreeNode(currNodes[1], false);
            ValidateOrder(initialNodes, currNodes, 1, 2, 0, 3, 4, 5);
            testAccessor.ReorderTreeNode(currNodes[2], false);
            ValidateOrder(initialNodes, currNodes, 1, 2, 3, 0, 4, 5);

            // Then back up
            testAccessor.ReorderTreeNode(currNodes[3], true);
            ValidateOrder(initialNodes, currNodes, 1, 2, 0, 3, 4, 5);
            testAccessor.ReorderTreeNode(currNodes[2], true);
            ValidateOrder(initialNodes, currNodes, 1, 0, 2, 3, 4, 5);
            testAccessor.ReorderTreeNode(currNodes[1], true);
            ValidateOrder(initialNodes, currNodes, 0, 1, 2, 3, 4, 5);
        });
    }

    [WinFormsFact]
    public void RepoObjectTree_moving_node_across_hidden_trees_skips_them()
    {
        RunRepoObjectsTreeTest(repoObjectTree =>
        {
            var testAccessor = repoObjectTree.GetTestAccessor();

            // act
            var currNodes = testAccessor.TreeView.Nodes;
            List<TreeNode> initialNodes = [.. currNodes.OfType<TreeNode>()];
            AssertListCount(initialNodes, 6);

            // Hide nodes between first and last
            testAccessor.SetTreeVisibleByIndex(1, false);
            testAccessor.SetTreeVisibleByIndex(2, false);

            // assert
            AssertListCount(currNodes, 4);

            // Move node 0 down, which should move it to index 3
            testAccessor.ReorderTreeNode(currNodes[0], false);

            // Unhide nodes between first and last
            testAccessor.SetTreeVisibleByIndex(1, true);
            testAccessor.SetTreeVisibleByIndex(2, true);

            // Reset currNodes, should be back
            AssertListCount(currNodes, 6);

            // Only first and last-visible nodes should have swapped
            ValidateOrder(initialNodes, currNodes, 3, 1, 2, 0, 4, 5);
        });
    }

    private static void AssertListCount(ICollection collection, int expectedCount)
    {
        var actualCount = collection.Count;
        if (actualCount == expectedCount)
        {
            return;
        }

        var items = collection.OfType<object>().Select(n => n.ToString()!).Join(", ");
        actualCount.Should().Be(expectedCount, $"Actual items: {items}");
    }

    private static void ValidateOrder(List<TreeNode> initialNodes, TreeNodeCollection currNodes,
        params int[] expectedOrder)
    {
        AssertListCount(currNodes, expectedOrder.Length);
        AssertListCount(initialNodes, expectedOrder.Length);

        for (var i = 0; i < initialNodes.Count; ++i)
        {
            currNodes[i].Should().Be(initialNodes[expectedOrder[i]]);
        }
    }

    private void RunRepoObjectsTreeTest(Action<RepoObjectsTree> testDriver)
    {
        RunFormTest(form =>
        {
            testDriver(form.GetTestAccessor().RepoObjectsTree);
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

public sealed class FormBrowseLeftPanelReorderNodesTestFixture : IDisposable
{
    private readonly List<bool> _originalRepoObjectsTreeShow = [];

    // Track the original setting value
    private readonly bool _originalShowAuthorAvatarColumn;
    private readonly bool _showAvailableDiffTools;

    public FormBrowseLeftPanelReorderNodesTestFixture()
    {
        // Remember the current settings...
        _originalShowAuthorAvatarColumn = AppSettings.ShowAuthorAvatarColumn;
        _showAvailableDiffTools = AppSettings.ShowAvailableDiffTools;

        // Stop loading custom diff tools
        AppSettings.ShowAvailableDiffTools = false;

        // We don't want avatars during tests, otherwise we will be attempting to download them from gravatar....
        AppSettings.ShowAuthorAvatarColumn = false;

        // Show all root nodes for test, restore when done
        _originalRepoObjectsTreeShow.Add(AppSettings.RepoObjectsTreeShowBranches);
        _originalRepoObjectsTreeShow.Add(AppSettings.RepoObjectsTreeShowRemotes);
        _originalRepoObjectsTreeShow.Add(AppSettings.RepoObjectsTreeShowWorktrees);
        _originalRepoObjectsTreeShow.Add(AppSettings.RepoObjectsTreeShowTags);
        _originalRepoObjectsTreeShow.Add(AppSettings.RepoObjectsTreeShowStashes);
        _originalRepoObjectsTreeShow.Add(AppSettings.RepoObjectsTreeShowSubmodules);
        AppSettings.RepoObjectsTreeShowBranches = true;
        AppSettings.RepoObjectsTreeShowRemotes = true;
        AppSettings.RepoObjectsTreeShowWorktrees = true;
        AppSettings.RepoObjectsTreeShowTags = true;
        AppSettings.RepoObjectsTreeShowStashes = true;
        AppSettings.RepoObjectsTreeShowSubmodules = true;
    }

    void IDisposable.Dispose()
    {
        AppSettings.ShowAuthorAvatarColumn = _originalShowAuthorAvatarColumn;
        AppSettings.ShowAvailableDiffTools = _showAvailableDiffTools;

        AppSettings.RepoObjectsTreeShowBranches = _originalRepoObjectsTreeShow[0];
        AppSettings.RepoObjectsTreeShowRemotes = _originalRepoObjectsTreeShow[1];
        AppSettings.RepoObjectsTreeShowWorktrees = _originalRepoObjectsTreeShow[2];
        AppSettings.RepoObjectsTreeShowTags = _originalRepoObjectsTreeShow[3];
        AppSettings.RepoObjectsTreeShowSubmodules = _originalRepoObjectsTreeShow[4];
        AppSettings.RepoObjectsTreeShowStashes = _originalRepoObjectsTreeShow[5];
    }
}
