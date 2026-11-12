using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;

namespace GitUI.AvaloniaTests;

public sealed class FilePathWorkflowTests
{
    [Fact]
    public void FormatPaths_should_return_relative_paths_one_per_line()
    {
        FilePathWorkflow.FormatPaths([new GitItemStatus("a/b.cs"), new GitItemStatus("c.txt")], "/repo", false)
            .Should().Be("a/b.cs" + Environment.NewLine + "c.txt");
    }

    [Fact]
    public void GetFullPath_should_root_the_git_path_in_the_working_directory()
    {
        FilePathWorkflow.GetFullPath("/repo", "a/b.cs")
            .Should().Be(Path.GetFullPath(Path.Combine("/repo", "a", "b.cs")));
    }
}
