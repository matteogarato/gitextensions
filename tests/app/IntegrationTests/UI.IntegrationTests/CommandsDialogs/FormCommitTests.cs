using System.ComponentModel.Design;
using CommonTestUtils;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI.Theming;
using GitUI;
using GitUI.CommandsDialogs;
using GitUI.Editor;
using GitUI.ScriptsEngine;
using GitUI.UserControls;
using ICSharpCode.TextEditor.Document;
using NSubstitute;

namespace UITests.CommandsDialogs;

public class FormCommitTests : IDisposable, IClassFixture<FormCommitTestsFixture>
{
    // Created once for each test
    private GitUiCommands _commands = null!;

    // Created once for the fixture
    private ReferenceRepository _referenceRepository = null!;

    public FormCommitTests(FormCommitTestsFixture fixture)
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

        var serviceContainer = GlobalServiceContainer.CreateDefaultMockServiceContainer();
        serviceContainer.RemoveService<IScriptsRunner>();

        var scriptsRunner = Substitute.For<IScriptsRunner>();
        scriptsRunner.RunEventScripts(Arg.Any<ScriptEvent>(), Arg.Any<FormCommit>()).Returns(true);
        serviceContainer.AddService(scriptsRunner);

        _commands = new GitUiCommands(serviceContainer, _referenceRepository.Module);
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsFact]
    public void Should_show_committer_info_on_open()
    {
        RunFormTest(async form =>
        {
            var commitAuthorStatus = form.GetTestAccessor().CommitAuthorStatusToolStripStatusLabel;

            await Task.Delay(1000);
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            commitAuthorStatus.Text.Should().Be("Committer author <author@mail.com>");
        });
    }

    [WinFormsFact]
    public void Should_not_update_committer_info_on_form_activated()
    {
        RunFormTest(async form =>
        {
            var commitAuthorStatus = form.GetTestAccessor().CommitAuthorStatusToolStripStatusLabel;

            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            commitAuthorStatus.Text.Should().Be("Committer author <author@mail.com>");

            using (Form tempForm = new())
            {
                tempForm.Owner = form;
                tempForm.Show();
                tempForm.Focus();

                _referenceRepository.Module.GitExecutable.GetOutput(@"config user.name ""new author""");
                _referenceRepository.Module.GitExecutable.GetOutput(@"config user.email ""new_author@mail.com""");
            }

            form.Focus();
            Application.DoEvents();

            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            commitAuthorStatus.Text.Should().Be("Committer author <author@mail.com>");
        });
    }

    [WinFormsFact]
    public void Should_display_branch_and_no_remote_info_in_statusbar()
    {
        var branchName = _referenceRepository.Module.GetCurrentBranchName();

        _referenceRepository.CheckoutBranch(branchName);
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var currentBranchNameLabelStatus = form.GetTestAccessor().CurrentBranchNameLabelStatus;
            var remoteNameLabelStatus = form.GetTestAccessor().RemoteNameLabelStatus;

            currentBranchNameLabelStatus.Text.Should().Be($"{branchName} →");
            remoteNameLabelStatus.Text.Should().Be("(remote not configured)");
        });
    }

    [WinFormsFact]
    public void Should_display_detached_head_info_in_statusbar()
    {
        _referenceRepository.CheckoutRevision();
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var currentBranchNameLabelStatus = form.GetTestAccessor().CurrentBranchNameLabelStatus;
            var remoteNameLabelStatus = form.GetTestAccessor().RemoteNameLabelStatus;

            // For a yet unknown cause randomly, the wait in UITest.RunForm does not suffice.
            if (!string.IsNullOrEmpty(remoteNameLabelStatus.Text))
            {
                Console.WriteLine($"{nameof(Should_display_detached_head_info_in_statusbar)} waits again");
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);
            }

            currentBranchNameLabelStatus.Text.Should().Be("(no branch)");
            remoteNameLabelStatus.Text.Should().Be(string.Empty);
        });
    }

    [WinFormsFact]
    public void Should_display_branch_and_remote_info_in_statusbar()
    {
        var branchName = _referenceRepository.Module.GetCurrentBranchName();

        _referenceRepository.CreateRemoteForBranch(branchName);
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var currentBranchNameLabelStatus = form.GetTestAccessor().CurrentBranchNameLabelStatus;
            var remoteNameLabelStatus = form.GetTestAccessor().RemoteNameLabelStatus;

            currentBranchNameLabelStatus.Text.Should().Be($"{branchName} →");
            remoteNameLabelStatus.Text.Should().Be($"origin/{branchName}");
        });
    }

    [WinFormsFact]
    public void PreserveCommitMessageOnReopen()
    {
        var generatedCommitMessage = Guid.NewGuid().ToString();

        RunFormTest(form =>
        {
            form.GetTestAccessor().Message.Text.Should().BeEmpty();
            form.GetTestAccessor().Message.Text = generatedCommitMessage;
        });

        RunFormTest(form =>
        {
            form.GetTestAccessor().Message.Text.Should().Be(generatedCommitMessage);
        });
    }

    [WinFormsTheory]
    [InlineData(CommitKind.Fixup)]
    [InlineData(CommitKind.Squash)]
    public void DoNotPreserveCommitMessageOnReopenFromSpecialCommit(CommitKind commitKind)
    {
        var generatedCommitMessage = Guid.NewGuid().ToString();

        RunFormTest(
            form =>
            {
                var prefix = commitKind.ToString().ToLowerInvariant();
                form.GetTestAccessor().Message.Text.Should().Be($"{prefix}! A commit message");
                form.GetTestAccessor().Message.Text = generatedCommitMessage;
            },
            commitKind);

        RunFormTest(form =>
        {
            form.GetTestAccessor().Message.Text.Should().BeEmpty();
        });
    }

    [WinFormsFact]
    public void PreserveCommitMessageOnReopenFromAmendCommit()
    {
        var oldCommitMessage = _referenceRepository.Module.GetRevision().Body;
        var newCommitMessageWithAmend = $"amend! {oldCommitMessage}\n\nNew commit message";

        RunFormTest(
            form =>
            {
                form.GetTestAccessor().Message.Text.Should().Be($"amend! {oldCommitMessage}\n\n{oldCommitMessage}");
                form.GetTestAccessor().Message.Text = newCommitMessageWithAmend;
            },
            CommitKind.Amend);

        RunFormTest(form =>
        {
            form.GetTestAccessor().Message.Text.Should().Be(newCommitMessageWithAmend);
        });
    }

    [WinFormsFact]
    public void SelectMessageFromHistory()
    {
        const string lastCommitMessage = "last commit message";
        AppSettings.LastCommitMessage = lastCommitMessage;

        RunFormTest(form =>
        {
            var commitMessageToolStripMenuItem =
                form.GetTestAccessor().CommitMessageToolStripMenuItem;

            // Verify the message appears correctly
            commitMessageToolStripMenuItem.ShowDropDown();
            commitMessageToolStripMenuItem.DropDownItems[0].Text.Should().Be(lastCommitMessage);

            // Verify the message is selected correctly
            commitMessageToolStripMenuItem.DropDownItems[0].PerformClick();
            form.GetTestAccessor().Message.Text.Should().Be(lastCommitMessage);
        });
    }

    [WinFormsFact]
    public void Should_handle_well_commit_message_in_commit_message_menu()
    {
        const string lastCommitMessage = "last commit message";
        AppSettings.LastCommitMessage = lastCommitMessage;

        _referenceRepository.CreateCommit(
            "Only first line\n\nof a multi-line commit message\nmust be displayed in the menu");
        _referenceRepository.CreateCommit(
            "Too long commit message that should be shorten because first line of a commit message is only 50 chars long");
        RunFormTest(form =>
        {
            var commitMessageToolStripMenuItem =
                form.GetTestAccessor().CommitMessageToolStripMenuItem;

            // Verify the message appears correctly
            commitMessageToolStripMenuItem.ShowDropDown();
            commitMessageToolStripMenuItem.DropDownItems[0].Text.Should().Be(lastCommitMessage);
            commitMessageToolStripMenuItem.DropDownItems[1].Text.Should()
                .Be("Too long commit message that should be shorten because first line of ...");
            commitMessageToolStripMenuItem.DropDownItems[2].Text.Should().Be("Only first line");
        });
    }

    [UseCulture("en-US")]
    [WinFormsFact]
    public void Should_stage_only_filtered_on_StageAll()
    {
        _referenceRepository.CreateRepoFile("file1A.txt", "Test");
        _referenceRepository.CreateRepoFile("file1B.txt", "Test");
        _referenceRepository.CreateRepoFile("file2.txt", "Test");

        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            form.GetTestAccessor().StageAllToolItem.ToolTipText.Should().Be("Stage all");
        });

        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var testform = form.GetTestAccessor();

            testform.UnstagedList.ClearSelected();
            testform.UnstagedList.SetFilter("file1");

            testform.StageAllToolItem.ToolTipText.Should().Be("Stage filtered");

            testform.StageAllToolItem.PerformClick();

            var fileNotMatchedByFilterIsStillUnstaged =
                testform.UnstagedList.AllItems.Any(i => i.Item.Name == "file2.txt");

            testform.StagedList.AllItemsCount.Should().Be(2);
            testform.UnstagedList.AllItemsCount.Should().Be(1);
            fileNotMatchedByFilterIsStillUnstaged.Should().BeTrue();
        });
    }

    [UseCulture("en-US")]
    [WinFormsFact]
    public void Should_unstage_only_filtered_on_UnstageAll()
    {
        _referenceRepository.CreateRepoFile("file1A-Привет.txt", "Test"); // escaped and not escaped in the same string
        _referenceRepository.CreateRepoFile("file1B-두다.txt",
            "Test"); // escaped octal code points (Korean Hangul in this case)
        _referenceRepository.CreateRepoFile("file2.txt", "Test");

        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            form.GetTestAccessor().UnstageAllToolItem.ToolTipText.Should().Be("Unstage all");
        });

        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            var testform = form.GetTestAccessor();

            testform.StagedList.AllItemsCount.Should().Be(0);
            testform.UnstagedList.AllItemsCount.Should().Be(3);

            testform.StagedList.SetFilter("");
            testform.StageAllToolItem.PerformClick();

            testform.StagedList.AllItemsCount.Should().Be(3);
            testform.UnstagedList.AllItemsCount.Should().Be(0);

            testform.StagedList.ClearSelected();
            testform.StagedList.SetFilter("file1");

            testform.UnstageAllToolItem.ToolTipText.Should().Be("Unstage filtered");

            testform.UnstageAllToolItem.PerformClick();

            var fileNotMatchedByFilterIsStillStaged =
                testform.StagedList.AllItems.Any(i => i.Item.Name == "file2.txt");

            testform.UnstagedList.AllItemsCount.Should().Be(2);
            testform.StagedList.AllItemsCount.Should().Be(1);
            fileNotMatchedByFilterIsStillStaged.Should().BeTrue();
        });
    }

    [WinFormsTheory]
    [MemberData(nameof(CommitMessageTestData.TestCases), MemberType = typeof(CommitMessageTestData))]
    public void AddSelectionToCommitMessage_shall_be_ignored_unless_diff_is_focused(
        string message,
        int selectionStart,
        int selectionLength,
#pragma warning disable IDE0060 // Remove unused parameter
        string expectedMessage,
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable IDE0060 // Remove unused parameter
        int expectedSelectionStart)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TestAddSelectionToCommitMessage(false, CommitMessageTestData.SelectedText,
            message, selectionStart, selectionLength,
            false, expectedMessage, expectedSelectionStart);
    }

    [WinFormsTheory]
    [MemberData(nameof(CommitMessageTestData.TestCases), MemberType = typeof(CommitMessageTestData))]
    public void AddSelectionToCommitMessage_shall_be_ignored_if_no_difftext_is_selected(
        string message,
        int selectionStart,
        int selectionLength,
#pragma warning disable IDE0060 // Remove unused parameter
        string expectedMessage,
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable IDE0060 // Remove unused parameter
        int expectedSelectionStart)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TestAddSelectionToCommitMessage(true, "",
            message, selectionStart, selectionLength,
            false, expectedMessage, expectedSelectionStart);
    }

    [WinFormsTheory]
    [MemberData(nameof(CommitMessageTestData.TestCases), MemberType = typeof(CommitMessageTestData))]
    public void AddSelectionToCommitMessage_shall_modify_the_commit_message(
        string message,
        int selectionStart,
        int selectionLength,
        string expectedMessage,
        int expectedSelectionStart)
    {
        TestAddSelectionToCommitMessage(true, CommitMessageTestData.SelectedText,
            message, selectionStart, selectionLength,
            true, expectedMessage, expectedSelectionStart);
    }

    [WinFormsFact]
    public void EditFileToolStripMenuItem_Click_no_selection_should_not_throw()
    {
        RunFormTest(async form =>
        {
            await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);

            form.GetTestAccessor().UnstagedList.ClearSelected();

            var editFileToolStripMenuItem = form.GetTestAccessor().EditFileToolStripMenuItem;

            // asserting by the virtue of not crashing
            editFileToolStripMenuItem.PerformClick();
        });
    }

    [WinFormsFact]
    public void ResetAuthor_depends_on_amend()
    {
        RunFormTest(form =>
        {
            var testForm = form.GetTestAccessor();

            // check initial state
            testForm.Amend.Checked.Should().BeFalse();
            testForm.ResetAuthor.Checked.Should().BeFalse();
            testForm.ResetAuthor.Visible.Should().BeFalse();

            testForm.Amend.Checked = true;

            // check that reset author checkbox becomes visible when amend is checked
            testForm.Amend.Checked.Should().BeTrue();
            testForm.ResetAuthor.Visible.Should().BeTrue();

            testForm.ResetAuthor.Checked = true;

            testForm.Amend.Checked.Should().BeTrue();

            testForm.Amend.Checked = false;

            // check that reset author checkbox becomes invisible and unchecked when amend is unchecked
            testForm.Amend.Checked.Should().BeFalse();
            testForm.ResetAuthor.Checked.Should().BeFalse();
            testForm.ResetAuthor.Visible.Should().BeFalse();

            testForm.Amend.Checked = true;

            // check that when amend is checked again reset author is still unchecked
            testForm.Amend.Checked.Should().BeTrue();
            testForm.ResetAuthor.Visible.Should().BeTrue();
            testForm.ResetAuthor.Checked.Should().BeFalse();
        });
    }

    [WinFormsFact]
    public void ResetSoft()
    {
        AppSettings.CommitAndPushForcedWhenAmend = true;
        AppSettings.DontConfirmAmend.Value = true;
        AppSettings.CloseCommitDialogAfterCommit = false;
        AppSettings.CloseCommitDialogAfterLastCommit = false;
        AppSettings.CloseProcessDialog = true;

        var defaultBackColor = SystemColors.ButtonFace.ToArgb();
        var forceBackColor = OtherColors.AmendButtonForcedColor.ToArgb();

        const string originalCommitMessage = "commit to be amended by reset soft";
        const string amendedCommitMessage = "replacement commit";

        var previousCommitId = _commands.Module.RevParse("HEAD");
        var originalCommitHash =
            _referenceRepository.CreateCommit(originalCommitMessage, "original content", "theFile");

        RunFormTest(form =>
        {
            var ta = form.GetTestAccessor();

            // initial state
            ta.Amend.Enabled.Should().BeTrue();
            ta.Amend.Checked.Should().BeFalse();
            ta.CommitAndPush.BackColor.ToArgb().Should().Be(defaultBackColor);
            ta.ResetSoft.Visible.Should().BeFalse();
            ta.Message.Text.Should().BeEmpty();

            // amend needs to be activated first
            ta.Amend.Checked = true;

            ta.Amend.Enabled.Should().BeTrue();
            ta.Amend.Checked.Should().BeTrue();
            ta.CommitAndPush.BackColor.ToArgb().Should().Be(forceBackColor);
            ta.ResetSoft.Visible.Should().BeTrue();
            ta.ResetSoft.Enabled.Should().BeTrue();
            ta.Message.Text.Should().Be(originalCommitMessage);

            // reset soft
            ta.Message.Text = amendedCommitMessage;
            ta.ResetSoft.PerformClick();

            // update the form
            Application.DoEvents();
            AsyncTestHelper.JoinPendingOperations();

            _commands.Module.RevParse("HEAD").Should().Be(previousCommitId);
            ta.Amend.Enabled.Should().BeFalse();
            ta.Amend.Checked.Should().BeFalse();
            ta.CommitAndPush.BackColor.ToArgb().Should().Be(forceBackColor);
            ta.CommitAndPush.Text.Should().Be(ta.CommitAndForcePushText);
            ta.ResetSoft.Visible.Should().BeFalse();
            ta.Message.Text.Should().Be(amendedCommitMessage);

            // commit
            ta.Commit.PerformClick();

            // update the form
            Application.DoEvents();
            AsyncTestHelper.JoinPendingOperations();

            ta.Amend.Enabled.Should().BeTrue();
            ta.Amend.Checked.Should().BeFalse();
            ta.CommitAndPush.BackColor.ToArgb().Should().Be(forceBackColor);
            ta.CommitAndPush.Text.Should().Be(TranslatedStrings.ButtonPush);
            ta.ResetSoft.Visible.Should().BeFalse();
            ta.Message.Text.Should().BeEmpty();
        });
    }

    [WinFormsFact]
    public void Dialog_remembers_window_geometry()
    {
        RunGeometryMemoryTest(
            form => form.GetTestAccessor().Bounds,
            (bounds1, bounds2) => bounds2.Should().Be(bounds1));
    }

    [WinFormsFact]
    public void MessageEdit_remembers_geometry()
    {
        RunGeometryMemoryTest(
            form => form.GetTestAccessor().Message.Bounds,
            (bounds1, bounds2) => bounds2.Should().Be(bounds1));
    }

    [WinFormsFact]
    public void UnstagedList_remembers_geometry()
    {
        RunGeometryMemoryTest(
            form => form.GetTestAccessor().UnstagedList.Bounds,
            (bounds1, bounds2) =>
            {
                bounds2.Width.Should().Be(bounds1.Width);

                // The method to determine the height is prone to rounding errors.
                // This seems not to affect the user experience, because
                // - the rounding error is only +- 1 pixel
                // - if the user does not change the geometry, the height will oscillate to a constant value
                var height1 = bounds1.Height;
                var height2 = bounds2.Height;
                (height1 >= height2 - 1 && height1 <= height2 + 1).Should().BeTrue();
            });
    }

    [WinFormsTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Lists_must_not_select_an_item_when_a_click_gives_the_focus_back(bool byMouse, bool staged)
    {
        var expectedHasSelection = !byMouse;

        // Modify tracked files rather than adding new ones, so that the list is filled no matter
        // how the settings of the dialog are left behind by another test.
        _referenceRepository.CreateCommit("Add the files to modify", "Test", "file1.txt", "Test", "file2.txt");
        _referenceRepository.CreateRepoFile("file1.txt", "Changed");
        _referenceRepository.CreateRepoFile("file2.txt", "Changed");

        RunFormTest(async form =>
        {
            var ta = form.GetTestAccessor();

            if (staged)
            {
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);
                ta.StageAllToolItem.PerformClick();
                await AsyncTestHelper.JoinPendingOperationsAsync(AsyncTestHelper.UnexpectedTimeout);
            }

            var list = staged ? ta.StagedList : ta.UnstagedList;
            list.AllItemsCount.Should().BeGreaterThan(1, "the list must be able to scroll away from its first item");

            // The state a list is left in as soon as the other one has taken the selection over.
            // Note that this keeps the focused item, which is the one that would be scrolled to.
            list.ClearSelected();
            list.HasSelection.Should().BeFalse();
            list.FocusedItem.Should().NotBeNull();

            if (staged)
            {
                ta.RaiseStagedEnter(byMouse);
            }
            else
            {
                ta.RaiseUnstagedEnter(byMouse);
            }

            list.HasSelection.Should().Be(expectedHasSelection,
                "the click which gives the focus back selects the item it hits by itself");
        });
    }

    [WinFormsFact]
    public void SelectedDiff_remembers_geometry()
    {
        RunGeometryMemoryTest(
            form => form.GetTestAccessor().SelectedDiff.Bounds,
            (bounds1, bounds2) => bounds2.Should().Be(bounds1));
    }

    [WinFormsTheory]
    [InlineData("", 0, "feat: ", 6)]
    [InlineData("text", 3, "feat: text", 9)]
    public void ConventionalCommit_keyword_is_prefixed_when_none(string initialText, int initialPosition,
        string expectedText, int expectedPosition)
    {
        RunFormTest(form =>
        {
            var testForm = form.GetTestAccessor();
            testForm.SetMessageState(initialText, initialPosition);
            testForm.IncludeFeatureParentheses = false;
            (var message, var selectionStart) = testForm.PrefixOrReplaceKeyword("feat");
            message.Should().Be(expectedText);
            selectionStart.Should().Be(expectedPosition);
        });
    }

    [WinFormsTheory]
    [InlineData("", 0, "feat(): ", 5)]
    [InlineData("text", 3, "feat(): text", 5)]
    public void ConventionalCommit_keyword_is_prefixed_when_none_with_scope(string initialText, int initialPosition,
        string expectedText, int expectedPosition)
    {
        RunFormTest(form =>
        {
            var testForm = form.GetTestAccessor();
            testForm.SetMessageState(initialText, initialPosition);
            testForm.IncludeFeatureParentheses = true;
            (var message, var selectionStart) = testForm.PrefixOrReplaceKeyword("feat");
            message.Should().Be(expectedText);
            selectionStart.Should().Be(expectedPosition);
        });
    }

    [WinFormsTheory]
    [InlineData("fix: ", 0, "feat: ", 6)]
    [InlineData("fix: text", 3, "feat: text", 6)]
    public void ConventionalCommit_keyword_is_prefixed_when_already_typed(string initialText, int initialPosition,
        string expectedText, int expectedPosition)
    {
        RunFormTest(form =>
        {
            var testForm = form.GetTestAccessor();
            testForm.SetMessageState(initialText, initialPosition);
            testForm.IncludeFeatureParentheses = false;
            (var message, var selectionStart) = testForm.PrefixOrReplaceKeyword("feat");
            message.Should().Be(expectedText);
            selectionStart.Should().Be(expectedPosition);
        });
    }

    [WinFormsTheory]
    [InlineData("fix: ", 0, "feat(): ", 5)]
    [InlineData("fix: text", 3, "feat(): text", 5)]
    [InlineData("fix(scope): ", 0, "feat(scope): ", 13)]
    [InlineData("fix(scope): text", 14, "feat(scope): text", 15)]
    public void ConventionalCommit_keyword_is_prefixed_when_already_typed_with_scope(string initialText,
        int initialPosition,
        string expectedText, int expectedPosition)
    {
        RunFormTest(form =>
        {
            var testForm = form.GetTestAccessor();
            testForm.SetMessageState(initialText, initialPosition);
            testForm.IncludeFeatureParentheses = true;
            (var message, var selectionStart) = testForm.PrefixOrReplaceKeyword("feat");
            message.Should().Be(expectedText);
            selectionStart.Should().Be(expectedPosition);
        });
    }

    [WinFormsFact]
    public void MainSplitter_Remembers_Distance()
    {
        var splitterMoved = false;
        RunGeometryMemoryTest(
            form =>
            {
                if (!splitterMoved)
                {
                    form.GetTestAccessor().MainSplitter.SplitterDistance += 100;
                    splitterMoved = true;
                }

                return form.GetTestAccessor().UnstagedList.Bounds;
            },
            (bounds1, bounds2) => bounds2.Should().Be(bounds1));
    }

    private void TestAddSelectionToCommitMessage(
        bool focusSelectedDiff,
        string selectedText,
        string message,
        int selectionStart,
        int selectionLength,
        bool expectedResult,
        string expectedMessage,
        int expectedSelectionStart)
    {
        RunFormTest(form =>
        {
            var ta = form.GetTestAccessor();

            var selectedDiff = ta.SelectedDiff.GetTestAccessor().FileViewerInternal;
            selectedDiff.SetText(selectedText, openWithDifftool: null);
            selectedDiff.GetTestAccessor().TextEditor.ActiveTextAreaControl.SelectionManager.SetSelection(
                new TextLocation(0, 0), new TextLocation(selectedText.Length, 0));
            if (focusSelectedDiff)
            {
                selectedDiff.Focus();
            }

            ta.Message.Text = message;
            ta.Message.SelectionStart = selectionStart;
            ta.Message.SelectionLength = selectionLength;
            ta.ExecuteCommand(FormCommit.Command.AddSelectionToCommitMessage).Should().Be(expectedResult);
            ta.Message.Text.Should().Be(expectedMessage);
            ta.Message.SelectionStart.Should().Be(expectedSelectionStart);
            ta.Message.SelectionLength.Should().Be(expectedResult ? 0 : selectionLength);
        });
    }

    [WinFormsFact]
    public void ShouldNotUndoRenameFileWhenResettingStagedLines()
    {
        RunFormTest(form =>
        {
            var ta = form.GetTestAccessor();

            var selectedDiff = ta.SelectedDiff.GetTestAccessor();
            var selectedDiffInternal = selectedDiff.FileViewerInternal;

            // Commit a file, rename it and introduce a slight content change
            var contents = "this\nhas\nmany\nlines\nthis\nhas\nmany\nlines\nthis\nhas\nmany\nlines?\n";
            _referenceRepository.CreateCommit("commit", contents, "original.txt");
            _referenceRepository.DeleteRepoFile("original.txt");
            contents = contents.Replace("?", "!");
            _referenceRepository.CreateRepoFile("original2.txt", contents);

            ta.RescanChanges();
            AsyncTestHelper.JoinPendingOperations();

            ta.UnstagedList.SelectedItems = ta.UnstagedList.AllItems;
            ta.UnstagedList.Focus();
            ta.ExecuteCommand(RevisionDiffControl.Command.StageSelectedFile);

            ta.StagedList.SelectedGitItem =
                ta.StagedList.AllItems.Single(i => i.Item.Name.Contains("original2.txt")).Item;

            selectedDiffInternal.Focus();
            AsyncTestHelper.JoinPendingOperations();

            selectedDiffInternal.GetTestAccessor().TextEditor.ActiveTextAreaControl.SelectionManager.SetSelection(
                new TextLocation(2, 11), new TextLocation(5, 12));

            var textLengthBeforeReset = selectedDiffInternal.GetTestAccessor().TextEditor.ActiveTextAreaControl.Document
                .TextLength;

            selectedDiff.ResetSelectedLinesConfirmationDialog.Created += (s, e) =>
            {
                // Auto-press `Yes`
                selectedDiff.ResetSelectedLinesConfirmationDialog.Buttons[0].PerformClick();
            };
            selectedDiff.ExecuteCommand(FileViewer.Command.ResetLines);

            ta.RescanChanges();
            AsyncTestHelper.JoinPendingOperations();

            var textLengthAfterReset = selectedDiffInternal.GetTestAccessor().TextEditor.ActiveTextAreaControl.Document
                .TextLength;

            textLengthBeforeReset.Should().BeGreaterThan(0);
            textLengthAfterReset.Should().BeGreaterThan(0);
            textLengthAfterReset.Should().BeLessThan(textLengthBeforeReset);
            var stagedAndRenamed =
                ta.StagedList.AllItems.FirstOrDefault(i => i.Item.Name.Contains("original2.txt"));
            stagedAndRenamed.Should().NotBeNull();
            stagedAndRenamed!.Item.IsRenamed.Should().BeTrue();
        });
    }

    private void RunGeometryMemoryTest(Func<FormCommit, Rectangle> boundsAccessor,
        Action<Rectangle, Rectangle> testDriver)
    {
        var bounds1 = Rectangle.Empty;
        var bounds2 = Rectangle.Empty;
        RunFormTest(form => bounds1 = boundsAccessor(form));
        RunFormTest(form => bounds2 = boundsAccessor(form));
        testDriver(bounds1, bounds2);
    }

    private void RunFormTest(Action<FormCommit> testDriver, CommitKind commitKind = CommitKind.Normal)
    {
        RunFormTest(
            form =>
            {
                testDriver(form);
                return Task.CompletedTask;
            },
            commitKind);
    }

    private void RunFormTest(Func<FormCommit, Task> testDriverAsync, CommitKind commitKind = CommitKind.Normal)
    {
        UiTest.RunForm(
            () =>
            {
                (commitKind switch
                {
                    CommitKind.Normal => _commands.StartCommitDialog(owner: null),
                    CommitKind.Squash => _commands.StartSquashCommitDialog(owner: null,
                        _referenceRepository.Module.GetRevision()),
                    CommitKind.Fixup => _commands.StartFixupCommitDialog(owner: null,
                        _referenceRepository.Module.GetRevision()),
                    CommitKind.Amend => _commands.StartAmendCommitDialog(owner: null,
                        _referenceRepository.Module.GetRevision()),
                    _ => throw new ArgumentException($"Unsupported commit kind: {commitKind}", nameof(commitKind))
                }).Should().BeTrue();

                // Await updated FileViewer
                AsyncTestHelper.JoinPendingOperations();
            },
            testDriverAsync);
    }
}

public class CommitMessageTestData
{
    internal const string SelectedText = "selection";
    private const string _selectedTextWithNewLine = SelectedText + "\n";

    public static IEnumerable<object[]> TestCases
    {
        get
        {
            yield return ["", 0, 0, _selectedTextWithNewLine, _selectedTextWithNewLine.Length];
            yield return ["msg", 0, 0, _selectedTextWithNewLine + "msg", _selectedTextWithNewLine.Length];
            yield return ["msg", 1, 0, "m" + _selectedTextWithNewLine + "sg", 1 + _selectedTextWithNewLine.Length];
            yield return ["msg", 2, 0, "ms" + _selectedTextWithNewLine + "g", 2 + _selectedTextWithNewLine.Length];
            yield return ["msg", 3, 0, "msg" + _selectedTextWithNewLine, 3 + _selectedTextWithNewLine.Length];
            yield return ["msg", 0, 1, "" + SelectedText + "sg", SelectedText.Length];
            yield return ["msg", 1, 1, "m" + SelectedText + "g", 1 + SelectedText.Length];
            yield return ["msg", 2, 1, "ms" + SelectedText, 2 + SelectedText.Length];
        }
    }
}

public sealed class FormCommitTestsFixture : IDisposable
{
    // Track the original setting value
    private readonly bool _provideAutocompletion;
    private readonly bool _showAvailableDiffTools;

    public FormCommitTestsFixture()
    {
        // Remember the current setting...
        _provideAutocompletion = AppSettings.ProvideAutocompletion;
        _showAvailableDiffTools = AppSettings.ShowAvailableDiffTools;

        // ...and stop loading auto completion and custom diff tools
        AppSettings.ProvideAutocompletion = false;
        AppSettings.ShowAvailableDiffTools = false;
    }

    void IDisposable.Dispose()
    {
        AppSettings.ProvideAutocompletion = _provideAutocompletion;
        AppSettings.ShowAvailableDiffTools = _showAvailableDiffTools;
    }
}
