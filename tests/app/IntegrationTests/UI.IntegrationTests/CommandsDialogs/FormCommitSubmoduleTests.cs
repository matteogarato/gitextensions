using CommonTestUtils;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls;

namespace UITests.CommandsDialogs;

public class FormCommitSubmoduleTests : IDisposable, IClassFixture<FormCommitSubmoduleTestsFixture>
{
    private const string _submodulePath = "sub";
    private GitUiCommands _commands = null!;

    // Created once for each test
    private GitModuleTestHelper _parentRepo = null!;
    private GitModuleTestHelper _submoduleRepo = null!;

    public FormCommitSubmoduleTests(FormCommitSubmoduleTestsFixture fixture)
    {
        InitializeTest();
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void InitializeTest()
    {
        _parentRepo = new GitModuleTestHelper("parent");
        _submoduleRepo = new GitModuleTestHelper("submodule");

        _parentRepo.Module.GitExecutable.GetOutput(@"commit --allow-empty -m ""Initial commit""");

        // AddSubmodule commits the submodule; undo that commit to get the state of a freshly added
        // submodule, which is staged. Unstaging it then leaves an untracked path, which is what
        // git-status reports it as.
        _parentRepo.AddSubmodule(_submoduleRepo, _submodulePath);
        _parentRepo.Module.GitExecutable.GetOutput("reset --soft HEAD~1");

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(), _parentRepo.Module);
    }

    private void DisposeTest()
    {
        _parentRepo.Dispose();
        _submoduleRepo.Dispose();
    }

    [WinFormsFact]
    public void Unstaging_a_new_submodule_should_leave_the_status_a_rescan_reports()
    {
        RunFormTest(form =>
        {
            var ta = form.GetTestAccessor();

            var staged = GetSubmoduleItem(ta.StagedList);
            staged.IsSubmodule.Should().BeTrue("`git submodule add` stages the submodule");

            ta.StagedList.SelectedGitItems = [staged];
            ta.Unstage();

            var afterUnstaging = GetSubmoduleItem(ta.UnstagedList).IsSubmodule;

            ta.RescanChanges();
            AsyncTestHelper.JoinPendingOperations();

            GetSubmoduleItem(ta.UnstagedList).IsSubmodule
                .Should().Be(afterUnstaging, "unstaging must not leave a status which the next rescan changes");

            return Task.CompletedTask;
        });

        return;

        static GitItemStatus GetSubmoduleItem(FileStatusList list)
        {
            return list.AllItems.Items().Single(item => item.Name.TrimEnd('/') == _submodulePath);
        }
    }

    private void RunFormTest(Func<FormCommit, Task> testDriverAsync)
    {
        UiTest.RunForm(
            () =>
            {
                _commands.StartCommitDialog(owner: null).Should().BeTrue();

                // Await updated FileViewer
                AsyncTestHelper.JoinPendingOperations();
            },
            testDriverAsync);
    }
}

public sealed class FormCommitSubmoduleTestsFixture : IDisposable
{
    // Track the original setting value
    private readonly bool _showAvailableDiffTools;

    public FormCommitSubmoduleTestsFixture()
    {
        _showAvailableDiffTools = AppSettings.ShowAvailableDiffTools;

        // Stop loading custom diff tools
        AppSettings.ShowAvailableDiffTools = false;
    }

    void IDisposable.Dispose()
    {
        AppSettings.ShowAvailableDiffTools = _showAvailableDiffTools;
    }
}
