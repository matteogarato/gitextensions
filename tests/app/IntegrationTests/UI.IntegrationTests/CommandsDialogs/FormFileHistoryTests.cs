using CommonTestUtils;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitUI;
using GitUI.CommandsDialogs;

namespace UITests.CommandsDialogs;

public class FormFileHistoryTests : IDisposable
{
    private readonly GitUiCommands _commands = null!;
    private readonly ReferenceRepository _referenceRepository = null!;

    public FormFileHistoryTests()
    {
        _referenceRepository = new ReferenceRepository();
        _commands = new GitUiCommands(GlobalServiceContainer.CreateDefaultMockServiceContainer(),
            _referenceRepository.Module);

        AppSettings.UseBrowseForFileHistory.Value = false;
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _referenceRepository.Dispose();
    }

    [WinFormsTheory]
    [InlineData("", "file.txt")]
    [InlineData("", "file with spaces.txt")]
    [InlineData("Dir with spaces", "file.txt")]
    [InlineData("Dir with spaces", "file with spaces.txt")]
    public void File_history_should_behave_as_expected(string fileRelativePath, string fileName)
    {
        var revision1 = _referenceRepository.CreateCommitRelative(fileRelativePath, fileName,
            $"Create '{fileName}' in directory '{fileRelativePath}'");
        var revision2 = _referenceRepository.CreateCommitRelative(fileRelativePath, fileName,
            $"Update '{fileName}' in directory '{fileRelativePath}'");
        var revision3 = _referenceRepository.CreateCommitRelative(fileRelativePath, fileName,
            $"Update '{fileName}' in directory '{fileRelativePath}' again");

        RunFormTest(
            form =>
            {
                WaitForRevisionsToBeLoaded(form);
                form.GetTestAccessor().RevisionGrid.GetRevision(ObjectId.Parse(revision1)).Should().NotBeNull();
                form.GetTestAccessor().RevisionGrid.GetRevision(ObjectId.Parse(revision2)).Should().NotBeNull();
                form.GetTestAccessor().RevisionGrid.GetRevision(ObjectId.Parse(revision3)).Should().NotBeNull();
            }, Path.Combine(fileRelativePath, fileName), _commands);
    }

    [WinFormsFact]
    public void File_history_should_display_contents_in_all_revisions()
    {
        const string text = "This text will appear in a file that shall be renamed";

        var initialRevision =
            _referenceRepository.CreateCommitRelative(string.Empty, "a.txt", "Create 'a.txt'", text);
        var renameRevision =
            _referenceRepository.RenameRepoFile(string.Empty, "a.txt", "b.txt", commitMessage: "Rename to 'b.txt'");
        var anotherRenameRevision =
            _referenceRepository.RenameRepoFile(string.Empty, "b.txt", "c.txt", commitMessage: "Rename to 'c.txt'");

        AppSettings.FollowRenamesInFileHistory = true;

        bool textLoaded;

        RunFormTest(
            form =>
            {
                WaitForRevisionsToBeLoaded(form);
                form.GetTestAccessor().SelectViewTab();
                form.GetTestAccessor().FileViewer.TextLoaded += (_, _) => textLoaded = true;

                AssertFileViewerTextIsCorrect(form, initialRevision);
                AssertFileViewerTextIsCorrect(form, renameRevision);
                AssertFileViewerTextIsCorrect(form, anotherRenameRevision);
            }, Path.Combine(string.Empty, "c.txt"), _commands);

        void AssertFileViewerTextIsCorrect(FormFileHistory form, string revision)
        {
            textLoaded = false;
            form.GetTestAccessor().RevisionGrid.SetSelectedRevision(ObjectId.Parse(revision)).Should().BeTrue();
            UiTest.ProcessUntil("Loading file text", () => textLoaded);
            form.GetTestAccessor().FileViewer.GetTestAccessor().FileViewerInternal.GetTestAccessor().TextEditor.Text
                .Should().Be(text);
        }
    }

    private static void RunFormTest(Action<FormFileHistory> testDriver, string fileName, GitUiCommands commands)
    {
        UiTest.RunForm(
            () => commands.GetTestAccessor().ShowFileHistoryDialog(fileName),
            (FormFileHistory form) =>
            {
                testDriver(form);

                return Task.CompletedTask;
            });
    }

    private static void WaitForRevisionsToBeLoaded(FormFileHistory form)
    {
        UiTest.ProcessUntil("Loading Revisions",
            () => form.GetTestAccessor().RevisionGrid.GetTestAccessor().IsDataLoadComplete);
    }
}
