using CommonTestUtils;
using GitExtensions.Extensibility.Git;
using NSubstitute;

namespace GitCommandsTests.Git;

public partial class CommandsTests : IDisposable
{
    private readonly string _gitDir = null!;
    private readonly string _tempDir = null!;

    public CommandsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        _gitDir = Path.Combine(_tempDir, ".git");
        Directory.CreateDirectory(_gitDir);
    }

    void IDisposable.Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public void GetSelectedBranch_should_return_branch_name_from_HEAD_file()
    {
        var gitExecutor = Substitute.For<IGitExecutor>();
        gitExecutor.IsReftableRepo.Returns(false);
        gitExecutor.WorkingDir.Returns(_tempDir);
        gitExecutor.GetGitDirectory().Returns(_gitDir);

        File.WriteAllText(Path.Combine(_gitDir, "HEAD"), "ref: refs/heads/main");

        var branch = GitCommands.Git.Commands.GetSelectedBranch(gitExecutor);
        branch.Should().Be("main");
    }

    [Fact]
    public void GetSelectedBranch_should_return_detached_branch_when_HEAD_is_detached()
    {
        var gitExecutor = Substitute.For<IGitExecutor>();
        gitExecutor.IsReftableRepo.Returns(false);
        gitExecutor.WorkingDir.Returns(_tempDir);
        gitExecutor.GetGitDirectory().Returns(_gitDir);

        File.WriteAllText(Path.Combine(_gitDir, "HEAD"), "9601551c564b48208bccd50b705264e9bd68140d");

        var branch = GitCommands.Git.Commands.GetSelectedBranch(gitExecutor);
        branch.Should().Be("(no branch)");
    }

    [Fact]
    public void GetSelectedBranch_should_return_empty_if_HEAD_file_missing()
    {
        MockExecutable executable = new();
        var gitExecutor = Substitute.For<IGitExecutor>();
        gitExecutor.IsReftableRepo.Returns(false);
        gitExecutor.WorkingDir.Returns(_tempDir);
        gitExecutor.GetGitDirectory().Returns(_gitDir);
        gitExecutor.GitExecutable.Returns(executable);

        using (executable.StageOutput("symbolic-ref --quiet HEAD", string.Empty, 1))
        {
            var branch = GitCommands.Git.Commands.GetSelectedBranch(gitExecutor, true);
            branch.Should().BeEmpty();
        }
    }

    [Fact]
    public void GetSelectedBranch_should_fallback_to_git_command_when_reftable_repo()
    {
        MockExecutable executable = new();
        var gitExecutor = Substitute.For<IGitExecutor>();
        gitExecutor.IsReftableRepo.Returns(true);
        gitExecutor.WorkingDir.Returns(_tempDir);
        gitExecutor.GitExecutable.Returns(executable);

        using (executable.StageOutput("symbolic-ref --quiet HEAD", "refs/heads/feature\n"))
        {
            var branch = GitCommands.Git.Commands.GetSelectedBranch(gitExecutor);
            branch.Should().Be("feature");
        }
    }

    [Fact]
    public void GetSelectedBranch_should_return_detached_branch_when_command_fails_and_emptyIfDetached_false()
    {
        MockExecutable executable = new();
        var gitExecutor = Substitute.For<IGitExecutor>();
        gitExecutor.IsReftableRepo.Returns(true);
        gitExecutor.WorkingDir.Returns(_tempDir);
        gitExecutor.GitExecutable.Returns(executable);

        using (executable.StageOutput("symbolic-ref --quiet HEAD", string.Empty, 1))
        {
            var branch = GitCommands.Git.Commands.GetSelectedBranch(gitExecutor);
            branch.Should().Be("(no branch)");
        }
    }

    [Fact]
    public void GetSelectedBranch_should_return_empty_when_command_fails_and_emptyIfDetached_true()
    {
        MockExecutable executable = new();
        var gitExecutor = Substitute.For<IGitExecutor>();
        gitExecutor.IsReftableRepo.Returns(true);
        gitExecutor.WorkingDir.Returns(_tempDir);
        gitExecutor.GitExecutable.Returns(executable);

        using (executable.StageOutput("symbolic-ref --quiet HEAD", string.Empty, 1))
        {
            var branch = GitCommands.Git.Commands.GetSelectedBranch(gitExecutor, true);
            branch.Should().BeEmpty();
        }
    }
}
