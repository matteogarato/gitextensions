using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUITests.CommandsDialogs;

public sealed class FormDeleteBranchTests
{
    private const string _mainWorktreePath = @"C:\repos\main";
    private const string _linkedWorktreePath = @"C:\repos\feature";
    private const string _secondLinkedWorktreePath = @"C:\repos\hotfix";

    [Fact]
    public void ClassifyWorktreeBranches_should_return_empty_when_no_branches_match()
    {
        IGitRef[] branches = [CreateBranch("unrelated")];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "feature", false)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, _mainWorktreePath);

        result.HasDeletedWorktrees.Should().BeFalse();
        result.MainWorktreeBranches.Should().BeEmpty();
        result.LinkedWorktreeBranches.Should().BeEmpty();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_classify_branch_in_main_worktree()
    {
        var masterRef = CreateBranch("master");
        IGitRef[] branches = [masterRef];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "feature", false)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, _linkedWorktreePath);

        result.MainWorktreeBranches.Should().ContainSingle()
            .Which.Branch.Name.Should().Be("master");
        result.LinkedWorktreeBranches.Should().BeEmpty();
        result.HasDeletedWorktrees.Should().BeFalse();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_classify_branch_in_linked_worktree()
    {
        var featureRef = CreateBranch("feature");
        IGitRef[] branches = [featureRef];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "feature", false)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, _mainWorktreePath);

        result.LinkedWorktreeBranches.Should().ContainSingle()
            .Which.Branch.Name.Should().Be("feature");
        result.MainWorktreeBranches.Should().BeEmpty();
        result.HasDeletedWorktrees.Should().BeFalse();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_skip_current_worktree()
    {
        var featureRef = CreateBranch("feature");
        IGitRef[] branches = [featureRef];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "feature", false)
        ];

        // Current dir matches the linked worktree — should be skipped
        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, _linkedWorktreePath);

        result.LinkedWorktreeBranches.Should().BeEmpty();
        result.MainWorktreeBranches.Should().BeEmpty();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_detect_deleted_worktrees()
    {
        var staleRef = CreateBranch("stale-branch");
        IGitRef[] branches = [staleRef];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "stale-branch", true)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, _mainWorktreePath);

        result.HasDeletedWorktrees.Should().BeTrue();
        result.LinkedWorktreeBranches.Should().BeEmpty();
        result.MainWorktreeBranches.Should().BeEmpty();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_not_flag_deleted_worktrees_for_unrelated_branches()
    {
        IGitRef[] branches = [CreateBranch("unrelated")];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "stale-branch", true)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, _mainWorktreePath);

        result.HasDeletedWorktrees.Should().BeFalse();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_skip_detached_and_bare_worktrees()
    {
        IGitRef[] branches = [CreateBranch("any")];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Bare, null, null, false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Detached, "ccc", null, false)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, @"C:\repos\other");

        result.MainWorktreeBranches.Should().BeEmpty();
        result.LinkedWorktreeBranches.Should().BeEmpty();
        result.HasDeletedWorktrees.Should().BeFalse();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_handle_mixed_scenario()
    {
        var masterRef = CreateBranch("master");
        var featureRef = CreateBranch("feature");
        var hotfixRef = CreateBranch("hotfix");
        var safeRef = CreateBranch("safe-to-delete");
        IGitRef[] branches = [masterRef, featureRef, hotfixRef, safeRef];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(_linkedWorktreePath, GitWorktreeHeadType.Branch, "bbb", "feature", false),
            new(_secondLinkedWorktreePath, GitWorktreeHeadType.Branch, "ccc", "hotfix", false)
        ];

        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, @"C:\repos\other");

        result.MainWorktreeBranches.Should().ContainSingle()
            .Which.Branch.Name.Should().Be("master");
        result.LinkedWorktreeBranches.Should().HaveCount(2);
        result.LinkedWorktreeBranches.Select(x => x.Branch.Name)
            .Should().BeEquivalentTo("feature", "hotfix");
        result.HasDeletedWorktrees.Should().BeFalse();
    }

    [Fact]
    public void ClassifyWorktreeBranches_should_be_case_insensitive_on_paths()
    {
        var featureRef = CreateBranch("feature");
        IGitRef[] branches = [featureRef];
        List<GitWorktree> worktrees =
        [
            new(_mainWorktreePath, GitWorktreeHeadType.Branch, "aaa", "master", false),
            new(@"C:\Repos\Feature", GitWorktreeHeadType.Branch, "bbb", "feature", false)
        ];

        // Current dir is lowercase version of the linked worktree path
        var result =
            FormDeleteBranch.ClassifyWorktreeBranches(branches, worktrees, @"C:\repos\feature");

        result.LinkedWorktreeBranches.Should().BeEmpty();
    }

    private static IGitRef CreateBranch(string name)
    {
        var gitRef = Substitute.For<IGitRef>();
        gitRef.Name.Returns(name);
        gitRef.CompleteName.Returns($"refs/heads/{name}");
        gitRef.IsHead.Returns(true);
        return gitRef;
    }
}
