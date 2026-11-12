using GitUI.CommandsDialogs;

namespace GitUI.AvaloniaTests;

public sealed class IgnoreWorkflowTests
{
    [Fact]
    public void BuildPattern_should_anchor_and_escape_special_characters()
    {
        IgnoreWorkflow.BuildPattern("dir\\my file#[1].txt").Should().Be("/dir/my\\ file\\#\\[1\\].txt");
    }

    [Fact]
    public void AddToGitIgnore_should_append_new_patterns_once()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, ".gitignore"), "/bin");

            IgnoreWorkflow.AddToGitIgnore(directory, ["a.txt", "bin"]).Should().Equal("/a.txt");
            IgnoreWorkflow.AddToGitIgnore(directory, ["a.txt"]).Should().BeEmpty();

            File.ReadAllText(Path.Combine(directory, ".gitignore"))
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Should().Equal("/bin", "/a.txt");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void AddToGitIgnore_should_create_the_file_when_missing()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            IgnoreWorkflow.AddToGitIgnore(directory, ["x/y.log"]);

            File.ReadAllText(Path.Combine(directory, ".gitignore")).Should().StartWith("/x/y.log");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
