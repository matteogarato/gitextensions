using GitUI.LeftPanel;

namespace GitUITests.LeftPanel;

public sealed class WorktreeTreeTests
{
    private static readonly char _sep = Path.DirectorySeparatorChar;

    [Fact]
    public void GetCommonPrefix_should_return_empty_for_no_paths()
    {
        var result = WorktreeTree.GetCommonPrefix([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCommonPrefix_should_return_empty_for_single_path()
    {
        // A single path has nothing to compare against — no common prefix to strip.
        string[] paths = [$"worktrees{_sep}feature-a"];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCommonPrefix_should_return_common_prefix_for_sibling_paths()
    {
        string[] paths =
        [
            $"my-repo.worktrees{_sep}feature-a",
            $"my-repo.worktrees{_sep}feature-b"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be($"my-repo.worktrees{_sep}");
    }

    [Fact]
    public void GetCommonPrefix_should_return_empty_when_paths_share_no_directory_prefix()
    {
        string[] paths =
        [
            $"folder-a{_sep}feature-a",
            $"folder-b{_sep}feature-b"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCommonPrefix_should_return_partial_prefix_for_mixed_depth()
    {
        string[] paths =
        [
            $"root{_sep}team-a{_sep}feature-1",
            $"root{_sep}team-a{_sep}feature-2",
            $"root{_sep}team-b{_sep}feature-3"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be($"root{_sep}");
    }

    [Fact]
    public void GetCommonPrefix_should_handle_nested_common_prefix()
    {
        string[] paths =
        [
            $"root{_sep}sub{_sep}feature-1",
            $"root{_sep}sub{_sep}feature-2"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be($"root{_sep}sub{_sep}");
    }

    [Fact]
    public void GetCommonPrefix_should_be_case_insensitive()
    {
        string[] paths =
        [
            $"Worktrees{_sep}feature-a",
            $"worktrees{_sep}feature-b"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be($"Worktrees{_sep}");
    }

    [Fact]
    public void GetCommonPrefix_should_strip_underscore_delimited_prefix()
    {
        string[] paths =
        [
            "gitextensions3_configdata",
            "gitextensions3_dev",
            "gitextensions3_test"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be("gitextensions3_");
    }

    [Fact]
    public void GetCommonPrefix_should_strip_hyphen_delimited_prefix()
    {
        string[] paths =
        [
            "my-repo-feature-a",
            "my-repo-feature-b"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be("my-repo-feature-");
    }

    [Fact]
    public void GetCommonPrefix_should_strip_dot_delimited_prefix()
    {
        string[] paths =
        [
            "repo.worktrees.alpha",
            "repo.worktrees.beta"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be("repo.worktrees.");
    }

    [Fact]
    public void GetCommonPrefix_should_strip_space_delimited_prefix()
    {
        string[] paths =
        [
            "my repo feature-a",
            "my repo feature-b"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be("my repo feature-");
    }

    [Fact]
    public void GetCommonPrefix_should_not_split_mid_word()
    {
        string[] paths = ["apricot", "apple"];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCommonPrefix_should_return_empty_for_single_path_without_boundary()
    {
        string[] paths = ["worktree"];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetCommonPrefix_should_snap_to_directory_separator_over_word_boundary()
    {
        // When paths contain directory separators, the prefix snaps to the last
        // directory separator even when word boundaries exist beyond it.
        string[] paths =
        [
            $"shared{_sep}gitextensions3_configdata",
            $"shared{_sep}gitextensions3_dev"
        ];

        var result = WorktreeTree.GetCommonPrefix(paths);

        result.Should().Be($"shared{_sep}");
    }
}
