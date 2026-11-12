using CommonTestUtils;
using GitCommands.Settings;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.LeftPanel;

namespace UITests.CommandsDialogs;

public class FormBrowseLeftPanelTests : IDisposable, IClassFixture<FormBrowseLeftPanelTestsFixture>
{
    private const string _remoteName = "remote1";
    private GitUiCommands _commands = null!;
    private ReferenceRepository _referenceRepository = null!;

    // Created once for each test
    private ReferenceRepository _remoteReferenceRepository = null!;

    public FormBrowseLeftPanelTests(FormBrowseLeftPanelTestsFixture fixture)
    {
        InitializeTest();
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void InitializeTest()
    {
        _remoteReferenceRepository = new ReferenceRepository();

        _referenceRepository = new ReferenceRepository();

        _referenceRepository.Module.AddRemote(_remoteName, _remoteReferenceRepository.Module.WorkingDir);
        _referenceRepository.Fetch(_remoteName);

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);

        _referenceRepository.CreateCommit("Commit1", "Commit1");
        _referenceRepository.CreateBranch("Branch1", _referenceRepository.CommitHash!);
        _referenceRepository.CreateTag("Branch1", _referenceRepository.CommitHash!);
        _referenceRepository.CreateCommit("Commit2", "Commit2");
        _referenceRepository.CreateBranch("Branch2", _referenceRepository.CommitHash!);

        _referenceRepository.CreateCommit("head commit");
    }

    private void DisposeTest()
    {
        _remoteReferenceRepository.Dispose();
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Branch_and_tag_context_menu_should_show_Sort_By_entry()
    {
        RunRepoObjectsTreeTest(contextMenu =>
        {
            contextMenu.Items.Count.Should().BeGreaterThan(5);

            var count = contextMenu.Items.Count;

            // Assert items from bottom to the top
            var item = contextMenu.Items[--count];
            item.Text.Should().Be("Run script");
            item.Enabled.Should().BeFalse("because this test includes no user scripts");

            contextMenu.Items[--count].Should().BeOfType<ToolStripSeparator>()
                .Which.Enabled.Should().BeFalse("because this test includes no user scripts");

            item = contextMenu.Items[--count];
            item.Text.Should().Be(TranslatedStrings.SortOrder);
            item.Enabled.Should().BeFalse("because sort order in this test is default");

            item = contextMenu.Items[--count];
            item.Text.Should().Be(TranslatedStrings.SortBy);
            item.Enabled.Should().BeTrue("because tags and branches are sortable");

            contextMenu.Items[--count].Should().BeOfType<ToolStripSeparator>();
        });
    }

    private void RunRepoObjectsTreeTest(Action<ContextMenuStrip> testDriver)
    {
        var branchName = _referenceRepository.Module.GetCurrentBranchName();

        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var ta = form.GetTestAccessor().RepoObjectsTree.GetTestAccessor();

            // We are running several tests one after another to speed up the test execution
            // as we don't need to re-create the host form

            var contextMenu = ta.ContextMenu;

            ta.SelectNode<LocalBranchNode>([TranslatedStrings.Branches, branchName]);
            ta.OpenContextMenu();
            testDriver(contextMenu);

            ta.SelectNode<RemoteBranchNode>([TranslatedStrings.Remotes, _remoteName, branchName]);
            ta.OpenContextMenu();
            testDriver(contextMenu);

            ta.SelectNode<TagNode>([TranslatedStrings.Tags, "Branch1"]);
            ta.OpenContextMenu();
            testDriver(contextMenu);
        });
    }

    private void RunFormTest(Func<FormBrowse, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () => _commands.StartBrowseDialog(owner: null).Should().BeTrue(),
            testDriverAsync);
    }
}

public sealed class FormBrowseLeftPanelTestsFixture : IDisposable
{
    // Track the original setting value
    private readonly bool _originalShowAuthorAvatarColumn;
    private readonly bool _showAvailableDiffTools;

    public FormBrowseLeftPanelTestsFixture()
    {
        // Remember the current settings...
        _originalShowAuthorAvatarColumn = AppSettings.ShowAuthorAvatarColumn;
        _showAvailableDiffTools = AppSettings.ShowAvailableDiffTools;

        // Stop loading custom diff tools
        AppSettings.ShowAvailableDiffTools = false;

        // We don't want avatars during tests, otherwise we will be attempting to download them from gravatar....
        AppSettings.ShowAuthorAvatarColumn = false;

        AppSettings.RepoObjectsTreeShowTags = true;
        AppSettings.RepoObjectsTreeShowStashes = true;
    }

    void IDisposable.Dispose()
    {
        AppSettings.ShowAuthorAvatarColumn = _originalShowAuthorAvatarColumn;
        AppSettings.ShowAvailableDiffTools = _showAvailableDiffTools;
    }
}
