using System.ComponentModel.Design;
using CommonTestUtils;
using GitCommands.Git;
using GitCommands.Settings;
using GitCommands.Submodules;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls;

namespace UITests.CommandsDialogs;

public class FormBrowseLeftPanelSubmodulesTests : IDisposable, IClassFixture<FormBrowseLeftPanelSubmodulesTestsFixture>
{
    // Created once for each test
    private GitUiCommands _commands = null!;

    private ISubmoduleStatusProvider _provider = null!;

    private GitModuleTestHelper _repo1 = null!;

    // Note that _repo2Module and _repo3Module point to the submodules under _repo1Module,
    // not _repo2.Module and _repo3.Module respectively. In general, the tests should here
    // should interact with these modules, not with _repo2 and _repo3.
    private IGitModule _repo1Module = null!;
    private GitModuleTestHelper _repo2 = null!;
    private IGitModule _repo2Module = null!;
    private GitModuleTestHelper _repo3 = null!;
    private IGitModule _repo3Module = null!;

    public FormBrowseLeftPanelSubmodulesTests(FormBrowseLeftPanelSubmodulesTestsFixture fixture)
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
        _repo2 = new GitModuleTestHelper("repo2");
        _repo3 = new GitModuleTestHelper("repo3");

        _repo2.AddSubmodule(_repo3, "repo3");
        _repo1.AddSubmodule(_repo2, "repo2");
        var submodules = _repo1.GetSubmodulesRecursive();

        _repo1Module = _repo1.Module;
        _repo2Module = submodules.ElementAt(0);
        _repo3Module = submodules.ElementAt(1);

        _provider = new SubmoduleStatusProvider(new GitExecutorProvider(new GitDirectoryResolver()));

        IServiceContainer serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();
        serviceContainer.RemoveService<ISubmoduleStatusProvider>();
        serviceContainer.AddService(_provider);

        _commands = new GitUiCommands(serviceContainer, _repo1Module);
    }

    private void DisposeTest()
    {
        _provider.Dispose();
        _repo1.Dispose();
        _repo2.Dispose();
        _repo3.Dispose();
    }

    [WinFormsFact]
    public void RepoObjectTree_should_show_all_submodules()
    {
        RunFormTest(form =>
        {
            var submodulesNode = GetSubmoduleNode(form);

            // act: wait until loaded by left panel
            UiTest.ProcessUntil("Loading submodules", () => submodulesNode.Nodes.Count == 1);

            // assert
            submodulesNode.Nodes.Count.Should().Be(1);

            var repo1Node = submodulesNode.Nodes[0];
            repo1Node.Name.Should().StartWith("repo1");
            repo1Node.Nodes.Count.Should().Be(1);

            var repo2Node = repo1Node.Nodes[0];
            repo2Node.Name.Should().StartWith("repo2");
            repo2Node.Nodes.Count.Should().Be(1);

            var repo3Node = repo2Node.Nodes[0];
            repo3Node.Name.Should().StartWith("repo3");
            repo3Node.Nodes.Count.Should().Be(0);

            return Task.CompletedTask;
        });
    }

    private static TreeNode GetSubmoduleNode(FormBrowse form)
    {
        var treeView = form.GetTestAccessor().RepoObjectsTree.GetTestAccessor().TreeView;
        var remotesNode = treeView.Nodes.OfType<TreeNode>()
            .FirstOrDefault(n => n.Text == TranslatedStrings.Submodules);
        remotesNode.Should().NotBeNull();
        return remotesNode;
    }

    private void RunFormTest(Func<FormBrowse, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () => _commands.StartBrowseDialog(owner: null).Should().BeTrue(),
            testDriverAsync);
    }
}

public sealed class FormBrowseLeftPanelSubmodulesTestsFixture : IDisposable
{
    // Track the original setting value
    private readonly bool _originalShowAuthorAvatarColumn;
    private readonly bool _showAvailableDiffTools;

    public FormBrowseLeftPanelSubmodulesTestsFixture()
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
