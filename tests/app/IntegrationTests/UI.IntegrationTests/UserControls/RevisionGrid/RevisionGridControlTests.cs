using System.Diagnostics;
using CommonTestUtils;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.UserControls.RevisionGrid;

namespace UITests.UserControls.RevisionGrid;

public class RevisionGridControlTests : IDisposable, IClassFixture<RevisionGridControlTestsFixture>
{
    private string _branch1Commit = null!;

    // Created once for each test
    private GitUiCommands _commands = null!;
    private string _headCommit = null!;

    private string _initialCommit = null!;

    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;

    public RevisionGridControlTests(RevisionGridControlTestsFixture fixture)
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
        _initialCommit = _referenceRepository.CommitHash!;

        _referenceRepository.CreateCommit("Commit1", "Commit1");
        _branch1Commit = _referenceRepository.CommitHash!;
        _referenceRepository.CreateBranch("Branch1", _branch1Commit);
        _referenceRepository.CreateCommit("Commit2", "Commit2");
        _referenceRepository.CreateBranch("Branch2", _referenceRepository.CommitHash!);

        _referenceRepository.CreateCommit("head commit");
        _headCommit = _referenceRepository.CommitHash!;

        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);

        AppSettings.RevisionGraphShowArtificialCommits = true;
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Assert_default_filter_related_settings()
    {
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.ShowGitNotes = true;

        RunSetAndApplyBranchFilterTest(
            "",
            "",
            revisionGridControl =>
            {
                AppSettings.BranchFilterEnabled.Value.Should().BeFalse();
                AppSettings.ShowCurrentBranchOnly.Value.Should().BeFalse();

                revisionGridControl.CurrentFilter.IsShowAllBranchesChecked.Should().BeTrue();
                revisionGridControl.CurrentFilter.IsShowCurrentBranchOnlyChecked.Should().BeFalse();
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeFalse();
            });

        RunSetAndApplyBranchFilterTest(
            "Branch1",
            "",
            revisionGridControl =>
            {
                AppSettings.BranchFilterEnabled.Value.Should().BeTrue();
                AppSettings.ShowCurrentBranchOnly.Value.Should().BeFalse();

                revisionGridControl.CurrentFilter.IsShowAllBranchesChecked.Should().BeFalse();
                revisionGridControl.CurrentFilter.IsShowCurrentBranchOnlyChecked.Should().BeFalse();
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeTrue();
            });

        RunSetAndApplyBranchFilterTest(
            "",
            "Commit1",
            revisionGridControl =>
            {
                AppSettings.BranchFilterEnabled.Value.Should().BeFalse();
                AppSettings.ShowCurrentBranchOnly.Value.Should().BeFalse();

                revisionGridControl.CurrentFilter.IsShowAllBranchesChecked.Should().BeTrue();
                revisionGridControl.CurrentFilter.IsShowCurrentBranchOnlyChecked.Should().BeFalse();
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeFalse();
            });
    }

    [WinFormsFact]
    public void View_reflects_applied_branch_filter()
    {
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.ShowGitNotes = true;

        RunSetAndApplyBranchFilterTest(
            "",
            "",
            revisionGridControl =>
            {
                WaitForRevisionsToBeLoaded(revisionGridControl);

                var ta = revisionGridControl.GetTestAccessor();
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeFalse();
#if DEBUG
                // https://github.com/gitextensions/gitextensions/issues/10170
                // This test occasionaly fails with 3 visible revisions
                ta.VisibleRevisionCount.Should().Be(4);
#endif

                // Verify the view hasn't changed until we refresh
                revisionGridControl.LatestSelectedRevision!.ObjectId.ToString().Should().Be(_headCommit);

                // set filter
                revisionGridControl.SetAndApplyBranchFilter("Branch1");
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeTrue();

#if DEBUG
                // https://github.com/gitextensions/gitextensions/issues/10170
                // This step occasionaly fails with 'Loading Revisions' didn't finish in 25 iterations
                WaitForRevisionsToBeLoaded(revisionGridControl);

                // Confirm the filter has been applied
                // This test occasionaly fails with 4 visible revisions
                ta.VisibleRevisionCount.Should().Be(2);
#endif
            });
    }

    [WinFormsFact]
    public void View_reflects_reset_branch_filter()
    {
        AppSettings.ShowReflogReferences.Value = false;
        AppSettings.BranchFilterEnabled.Value = false;
        AppSettings.ShowCurrentBranchOnly.Value = false;
        AppSettings.ShowGitNotes = true;

        RunSetAndApplyBranchFilterTest(
            "Branch1",
            "",
            revisionGridControl =>
            {
                WaitForRevisionsToBeLoaded(revisionGridControl);

                var ta = revisionGridControl.GetTestAccessor();
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeTrue();
                ta.VisibleRevisionCount.Should().Be(2);

                // Verify the view hasn't changed until we refresh
                revisionGridControl.LatestSelectedRevision!.ObjectId.ToString().Should().Be(_branch1Commit);

                // reset filter
                revisionGridControl.SetAndApplyBranchFilter(string.Empty);
                revisionGridControl.CurrentFilter.IsShowFilteredBranchesChecked.Should().BeFalse();

#if DEBUG
                // https://github.com/gitextensions/gitextensions/issues/10170
                // This step occasionaly fails with 'Loading Revisions' didn't finish in 25 iterations
                WaitForRevisionsToBeLoaded(revisionGridControl);

                // Confirm the filter has been reset, all commits are shown
                // This test occasionaly fails with 3 visible revisions
                ta.VisibleRevisionCount.Should().Be(4);
#endif
            });
    }

    [WinFormsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToggleBetweenArtificialAndHeadCommits_no_empty(bool showGitStatusForArtificialCommits)
    {
        File.WriteAllText(Path.Combine(_referenceRepository.Module.WorkingDir, "new.txt"), "new");
        File.WriteAllText(Path.Combine(_referenceRepository.Module.WorkingDir, "stage.txt"), "staged");
        _referenceRepository.Module.StageFile("stage.txt");

        RunToggleBetweenArtificialAndHeadCommitsTest(
            showGitStatusForArtificialCommits,
            revisionGridControl =>
            {
                // If showGitStatusForArtificialCommits is false, we do not update ChangeCount and HasChanges returns false.
                // Then ToggleBetweenArtificialAndHeadCommits does not check HasChanges and toggles through all three commits.
                while (revisionGridControl.GetChangeCount(ObjectId.WorkTreeId)!.HasChanges !=
                       showGitStatusForArtificialCommits
                       || revisionGridControl.GetChangeCount(ObjectId.IndexId)!.HasChanges !=
                       showGitStatusForArtificialCommits)
                {
                    DoEvents();
                }

                revisionGridControl.GoToRef(_initialCommit, false);
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_initialCommit);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.WorkTreeGuid);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.IndexGuid);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_headCommit);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.WorkTreeGuid);
            });
    }

    [WinFormsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToggleBetweenArtificialAndHeadCommits_no_workdir_change(bool showGitStatusForArtificialCommits)
    {
        File.WriteAllText(Path.Combine(_referenceRepository.Module.WorkingDir, "stage.txt"), "staged");
        _referenceRepository.Module.StageFile("stage.txt");

        RunToggleBetweenArtificialAndHeadCommitsTest(
            showGitStatusForArtificialCommits,
            revisionGridControl =>
            {
                while (revisionGridControl.GetChangeCount(ObjectId.WorkTreeId)!.HasChanges
                       || revisionGridControl.GetChangeCount(ObjectId.IndexId)!.HasChanges !=
                       showGitStatusForArtificialCommits)
                {
                    DoEvents();
                }

                revisionGridControl.GoToRef(_initialCommit, false);
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_initialCommit);

                if (!showGitStatusForArtificialCommits)
                {
                    revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                    revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.WorkTreeGuid);
                }

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.IndexGuid);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_headCommit);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid
                    .Should().Be(showGitStatusForArtificialCommits ? GitRevision.IndexGuid : GitRevision.WorkTreeGuid);
            });
    }

    [WinFormsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToggleBetweenArtificialAndHeadCommits_no_index_change(bool showGitStatusForArtificialCommits)
    {
        File.WriteAllText(Path.Combine(_referenceRepository.Module.WorkingDir, "new.txt"), "new");

        RunToggleBetweenArtificialAndHeadCommitsTest(
            showGitStatusForArtificialCommits,
            revisionGridControl =>
            {
                while (revisionGridControl.GetChangeCount(ObjectId.WorkTreeId)!.HasChanges !=
                       showGitStatusForArtificialCommits
                       || revisionGridControl.GetChangeCount(ObjectId.IndexId)!.HasChanges)
                {
                    DoEvents();
                }

                revisionGridControl.GoToRef(_initialCommit, false);
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_initialCommit);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.WorkTreeGuid);

                if (!showGitStatusForArtificialCommits)
                {
                    revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                    revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.IndexGuid);
                }

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_headCommit);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.WorkTreeGuid);
            });
    }

    [WinFormsTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ToggleBetweenArtificialAndHeadCommits_no_change(bool showGitStatusForArtificialCommits)
    {
        RunToggleBetweenArtificialAndHeadCommitsTest(
            showGitStatusForArtificialCommits,
            revisionGridControl =>
            {
                while (revisionGridControl.GetChangeCount(ObjectId.WorkTreeId)!.HasChanges
                       || revisionGridControl.GetChangeCount(ObjectId.IndexId)!.HasChanges)
                {
                    DoEvents();
                }

                revisionGridControl.GoToRef(_initialCommit, false);
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_initialCommit);

                if (!showGitStatusForArtificialCommits)
                {
                    revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                    revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.WorkTreeGuid);

                    revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                    revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(GitRevision.IndexGuid);
                }

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_headCommit);

                revisionGridControl.ToggleBetweenArtificialAndHeadCommits();
                revisionGridControl.LatestSelectedRevision!.Guid
                    .Should().Be(showGitStatusForArtificialCommits ? _headCommit : GitRevision.WorkTreeGuid);
            });
    }

    private void RunSetAndApplyBranchFilterTest(string initialFilter, string grepMessage,
        Action<RevisionGridControl> runTest)
    {
        // Disable artificial commits as they appear to destabilise these tests
        AppSettings.RevisionGraphShowArtificialCommits = false;

        UiTest.RunForm<FormBrowse>(
            () => _commands.StartBrowseDialog(owner: null).Should().BeTrue(),
            async formBrowse =>
            {
                DoEvents();

                // wait for the revisions to be loaded
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

                formBrowse.RevisionGridControl.SetSelectedRevision(ObjectId.Parse(_headCommit)).Should().BeTrue();

                formBrowse.RevisionGridControl.CurrentFilter.ByMessage = !string.IsNullOrWhiteSpace(grepMessage);
                formBrowse.RevisionGridControl.CurrentFilter.Message = grepMessage;
                formBrowse.RevisionGridControl.SetAndApplyBranchFilter(initialFilter);

                // wait for the revisions to be loaded
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

                try
                {
                    runTest(formBrowse.RevisionGridControl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{runTest.Method.Name} failed: {ex.Demystify()}");
                    Console.WriteLine(_referenceRepository.Module.GitExecutable.GetOutput("status"));
                    throw;
                }
            });
    }

    private void RunToggleBetweenArtificialAndHeadCommitsTest(bool showGitStatusForArtificialCommits,
        Action<RevisionGridControl> runTest)
    {
        AppSettings.ShowGitStatusForArtificialCommits = showGitStatusForArtificialCommits;

        UiTest.RunForm<FormBrowse>(
            () => _commands.StartBrowseDialog(owner: null).Should().BeTrue(),
            async formBrowse =>
            {
                DoEvents();

                // wait for the revisions to be loaded
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

                formBrowse.RevisionGridControl.LatestSelectedRevision!.Guid.Should().Be(_headCommit);

                var ta = formBrowse.GetTestAccessor();
                ta.CommitInfoTabControl.SelectedTab = ta.TreeTabPage;

                // let the focus events be handled
                DoEvents();

                try
                {
                    runTest(formBrowse.RevisionGridControl);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{runTest.Method.Name} failed: {ex.Demystify()}");
                    Console.WriteLine(_referenceRepository.Module.GitExecutable.GetOutput("status"));
                    throw;
                }

                // let the focus events be handled
                DoEvents();

                ta.CommitInfoTabControl.SelectedTab.Should().Be(ta.DiffTabPage, "Diff tab should be active");
            });
    }

    private static void DoEvents()
    {
        for (var i = 0; i < 5; ++i)
        {
            Thread.Sleep(50);
            Application.DoEvents();
        }
    }

    private static void WaitForRevisionsToBeLoaded(RevisionGridControl revisionGridControl)
    {
        UiTest.ProcessUntil("Loading Revisions", () => revisionGridControl.GetTestAccessor().IsDataLoadComplete);
    }
}

public sealed class RevisionGridControlTestsFixture
{
    public RevisionGridControlTestsFixture()
    {
        // There is no need to restore the original AppSettings because AppSettings is routed to a temp folder.

        // We don't want avatars during tests, otherwise we will be attempting to download them from gravatar.
        AppSettings.ShowAuthorAvatarColumn = false;
    }
}
