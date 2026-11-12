using System.IO.Abstractions;
using GitCommands.Git;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Git.Tag;

public class GitTagControllerTest
{
    private readonly IGitTagController _controller = null!;
    private readonly IFileSystem _fileSystem = null!;
    private readonly string _tagMessageFile = null!;
    private readonly IGitUiCommands _uiCommands = null!;
    private readonly string _workingDir = AppContext.BaseDirectory;

    public GitTagControllerTest()
    {
        _tagMessageFile = Path.Combine(_workingDir, "TAGMESSAGE");

        _fileSystem = Substitute.For<IFileSystem>();
        _fileSystem.File.Returns(Substitute.For<FileBase>());

        _uiCommands = Substitute.For<IGitUiCommands>();
        _uiCommands.Module.WorkingDir.Returns(_workingDir);
        _uiCommands.Module.GetPathForGitExecution(_tagMessageFile).Returns(_tagMessageFile);

        _controller = new GitTagController(_uiCommands, _fileSystem);
    }

    [Fact]
    public void CreateTagWithMessageThrowsIfTheWindowIsNull()
    {
        var args = CreateAnnotatedTagArgs();
        ((Action)(() => _controller.CreateTag(args, null!))).Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateTagWithMessageWritesTagMessageFile()
    {
        var args = CreateAnnotatedTagArgs();

        _controller.CreateTag(args, CreateTestingWindow());

        _fileSystem.File.Received(1).WriteAllText(_tagMessageFile, "hello world");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateTagWithMessageDeletesTheTemporaryFileForUiResult(bool uiResult)
    {
        var args = CreateAnnotatedTagArgs();

        _fileSystem.File.Exists(Arg.Is<string>(s => s != null)).Returns(true);

        _uiCommands.StartCommandLineProcessDialog(Arg.Any<IWin32Window>(),
                Arg.Is<IGitCommand>(cmd => cmd.Arguments.StartsWith("tag")))
            .Returns(uiResult);

        _controller.CreateTag(args, CreateTestingWindow()).Should().Be(uiResult);

        _fileSystem.File.Received(1).Delete(_tagMessageFile);
    }

    [Fact]
    public void PassesCreatedArgsAndWindowToCommands()
    {
        var args = CreateAnnotatedTagArgs();
        var window = CreateTestingWindow();

        _controller.CreateTag(args, window);

        _uiCommands.Received(1)
            .StartCommandLineProcessDialog(window, Arg.Is<IGitCommand>(cmd => cmd.Arguments.StartsWith("tag")));
    }

    private static IWin32Window CreateTestingWindow()
    {
        return Substitute.For<IWin32Window>();
    }

    private static GitCreateTagArgs CreateAnnotatedTagArgs()
    {
        return new GitCreateTagArgs("tagname", ObjectId.Random(), TagOperation.Annotate, "hello world");
    }
}
