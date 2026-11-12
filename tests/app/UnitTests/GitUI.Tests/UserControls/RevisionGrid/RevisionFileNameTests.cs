using System.ComponentModel.Design;
using CommonTestUtils;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI;
using GitUI.Hotkey;
using GitUI.UserControls.RevisionGrid;
using NSubstitute;
using ResourceManager;
using AppSettings = GitCommands.Settings.AppSettings;
using GitModule = GitCommands.Git.GitModule;

namespace GitUITests.UserControls.RevisionGrid;

public class RevisionFileNameTests : IDisposable
{
    private readonly IGitCommandRunner _commandRunner = null!;
    private readonly MockExecutable _executable = null!;
    private readonly RevisionGridControl _revisionGridControl = null!;

    public RevisionFileNameTests()
    {
        _revisionGridControl = new RevisionGridControl();
        _executable = new MockExecutable();
        _commandRunner = new GitCommandRunner(_executable, () => GitModule.SystemEncoding);

        ServiceContainer serviceContainer = new();
        serviceContainer.AddService(Substitute.For<IHotkeySettingsManager>());
        serviceContainer.AddService(Substitute.For<IHotkeySettingsLoader>());

        var gitModule = Substitute.For<IGitModule>();
        gitModule.GitExecutable.Returns(_ => _executable);
        gitModule.GitCommandRunner.Returns(_ => _commandRunner);

        GitUiCommands uiCommands = new(serviceContainer, gitModule);

        var uiCommandsSource = Substitute.For<IGitUiCommandsSource>();
        uiCommandsSource.UiCommands.Returns(_ => uiCommands);

        _revisionGridControl.UiCommandsSource = uiCommandsSource;
        _revisionGridControl.FilePathByObjectId = [];
    }

    void IDisposable.Dispose()
    {
        _revisionGridControl.Dispose();
    }

    [StaTheory]
    [InlineData("17b2a8777e43dff588284c9661206c97ccb6cf8e", "a.txt")]
    [InlineData("83ec7c0a516f00b67aa9915d8e673a26510c2eed", "b.txt")]
    public void GetRevisionFileName_should_query_git_and_return_file_name(string objectId, string expectedFileName)
    {
        const string path = "a.txt";
        string actualFileName;
        var line =
            $"-c log.showsignature=false log --format=\"????%H\" --name-only --follow --diff-merges=separate  --find-renames --find-copies {objectId} --max-count=1 -- \"a.txt\"";

        var originalFollowRenamesInFileHistoryExactOnly = AppSettings.FollowRenamesInFileHistoryExactOnly;
        AppSettings.FollowRenamesInFileHistoryExactOnly = false;

        try
        {
            using (_executable.StageOutput(line, $"????{objectId}\n{expectedFileName}"))
            {
                actualFileName = _revisionGridControl.GetRevisionFileName(path, ObjectId.Parse(objectId))!;
            }
        }
        finally
        {
            AppSettings.FollowRenamesInFileHistoryExactOnly = originalFollowRenamesInFileHistoryExactOnly;
        }

        actualFileName.Should().Be(expectedFileName);
    }
}
