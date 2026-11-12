using CommonTestUtils;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitModule = GitCommands.Git.GitModule;

namespace GitCommandsTests.Git;

public sealed class GitModuleWorktreeTests : IDisposable
{
    private readonly MockExecutable _executable = null!;
    private readonly GitModule _gitModule = null!;

    public GitModuleWorktreeTests()
    {
        // Clear the static GitVersion cache so the mock executable is always queried for --version.
        GitVersion.ResetVersion();
        _executable = new MockExecutable();
        _executable.StageOutput("--version", $"git version {GitVersion.LastRecommendedVersion}");
        _gitModule = GetGitModuleWithExecutable(_executable);
    }

    void IDisposable.Dispose()
    {
        _executable.Verify();
    }

    [Fact]
    public void GetWorktrees_should_parse_single_worktree_with_branch()
    {
        var output = string.Join('\0',
            "worktree C:/repos/main",
            "HEAD abc1234abc1234abc1234abc1234abc1234abc12",
            "branch refs/heads/master",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].Path.Should().Be("C:\\repos\\main");
            worktrees[0].HeadType.Should().Be(GitWorktreeHeadType.Branch);
            worktrees[0].Sha1.Should().Be("abc1234abc1234abc1234abc1234abc1234abc12");
            worktrees[0].Branch.Should().Be("master");
            worktrees[0].IsDeleted.Should().BeTrue();
        }
    }

    [Fact]
    public void GetWorktrees_should_parse_detached_head()
    {
        var output = string.Join('\0',
            "worktree C:/repos/detached",
            "HEAD def5678def5678def5678def5678def5678def56",
            "detached",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].HeadType.Should().Be(GitWorktreeHeadType.Detached);
            worktrees[0].Sha1.Should().Be("def5678def5678def5678def5678def5678def56");
            worktrees[0].Branch.Should().BeNull();
        }
    }

    [Fact]
    public void GetWorktrees_should_parse_bare_repo()
    {
        var output = string.Join('\0',
            "worktree C:/repos/bare",
            "bare",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].HeadType.Should().Be(GitWorktreeHeadType.Bare);
            worktrees[0].Sha1.Should().BeNull();
            worktrees[0].Branch.Should().BeNull();
        }
    }

    [Fact]
    public void GetWorktrees_should_parse_multiple_worktrees()
    {
        var output = string.Join('\0',
            "worktree C:/repos/main",
            "HEAD aaaa1234aaaa1234aaaa1234aaaa1234aaaa1234",
            "branch refs/heads/master",
            "", // end of first record
            "worktree C:/repos/feature",
            "HEAD bbbb5678bbbb5678bbbb5678bbbb5678bbbb5678",
            "branch refs/heads/feature/my-feature",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(2);

            worktrees[0].Path.Should().Be("C:\\repos\\main");
            worktrees[0].Branch.Should().Be("master");

            worktrees[1].Path.Should().Be("C:\\repos\\feature");
            worktrees[1].Branch.Should().Be("feature/my-feature");
        }
    }

    [Fact]
    public void GetWorktrees_should_mark_only_the_first_worktree_as_the_main_one()
    {
        // git always reports the main worktree first, and refuses to remove it. Deleting its
        // directory would take the repository with it, so it has to be told apart from the rest.
        var output = string.Join('\0',
            "worktree C:/repos/main",
            "HEAD aaaa1234aaaa1234aaaa1234aaaa1234aaaa1234",
            "branch refs/heads/master",
            "",
            "worktree C:/repos/feature",
            "HEAD bbbb5678bbbb5678bbbb5678bbbb5678bbbb5678",
            "branch refs/heads/feature/my-feature",
            "",
            "worktree C:/repos/hotfix",
            "HEAD cccc9012cccc9012cccc9012cccc9012cccc9012",
            "branch refs/heads/hotfix",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Select(worktree => worktree.IsMain).Should().Equal(true, false, false);
        }
    }

    [Fact]
    public void GetWorktrees_should_handle_path_with_spaces()
    {
        var output = string.Join('\0',
            "worktree C:/my repos/work tree",
            "HEAD abc1234abc1234abc1234abc1234abc1234abc12",
            "branch refs/heads/main",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Should().HaveCount(1);
            worktrees[0].Path.Should().Be("C:\\my repos\\work tree");
            worktrees[0].Branch.Should().Be("main");
        }
    }

    [Fact]
    public void GetWorktrees_should_strip_refs_heads_prefix_from_branch()
    {
        var output = string.Join('\0',
            "worktree C:/repos/main",
            "HEAD abc1234abc1234abc1234abc1234abc1234abc12",
            "branch refs/heads/feature/nested/branch",
            "", "");

        using (_executable.StageOutput("worktree list --porcelain -z", output))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees[0].Branch.Should().Be("feature/nested/branch");
        }
    }

    [Fact]
    public void GetWorktrees_should_return_empty_list_for_empty_output()
    {
        using (_executable.StageOutput("worktree list --porcelain -z", ""))
        {
            var worktrees = _gitModule.GetWorktrees();

            worktrees.Should().BeEmpty();
        }
    }

    private static GitModule GetGitModuleWithExecutable(IExecutable executable)
    {
        GitModule module = new(new GitExecutorProvider(new GitDirectoryResolver()), "");

        var executorAccessor = module.GetTestAccessor().Executor;
        executorAccessor.GitExecutable = executable;
        executorAccessor.GitWindowsExecutable = executable;
        executorAccessor.GitCommandRunner = new GitCommandRunner(executable, () => GitModule.SystemEncoding);

        return module;
    }
}
