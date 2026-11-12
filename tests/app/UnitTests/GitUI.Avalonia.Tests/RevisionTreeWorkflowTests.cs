using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.AvaloniaTests;

public sealed class RevisionTreeWorkflowTests
{
    [Fact]
    public void Sort_should_list_directories_first_and_strip_the_directory_prefix()
    {
        var file = Item("src/b.cs", GitObjectType.Blob);
        var folder = Item("src/z", GitObjectType.Tree);
        var other = Item("src/A.cs", GitObjectType.Blob);

        var entries = RevisionTreeWorkflow.Sort([file, folder, other], "src");

        entries.Select(entry => entry.DisplayName).Should().Equal("z", "A.cs", "b.cs");
        entries[0].Path.Should().Be("src/z");
        entries[0].IsDirectory.Should().BeTrue();
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("src", "")]
    [InlineData("src/app/ui", "src/app")]
    public void GetParentDirectory_should_return_the_enclosing_directory(string directory, string? expected)
    {
        RevisionTreeWorkflow.GetParentDirectory(directory).Should().Be(expected);
    }

    private static IObjectGitItem Item(string name, GitObjectType type)
    {
        var item = Substitute.For<IObjectGitItem>();
        item.Name.Returns(name);
        item.ObjectType.Returns(type);
        return item;
    }
}
