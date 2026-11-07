using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using NSubstitute;

namespace GitUI.Avalonia.Tests;

public sealed class RevisionTreeWorkflowTests
{
    [Fact]
    public void Sort_should_list_directories_first_and_strip_the_directory_prefix()
    {
        IObjectGitItem file = Item("src/b.cs", GitObjectType.Blob);
        IObjectGitItem folder = Item("src/z", GitObjectType.Tree);
        IObjectGitItem other = Item("src/A.cs", GitObjectType.Blob);

        IReadOnlyList<RevisionTreeEntry> entries = RevisionTreeWorkflow.Sort([file, folder, other], "src");

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
        IObjectGitItem item = Substitute.For<IObjectGitItem>();
        item.Name.Returns(name);
        item.ObjectType.Returns(type);
        return item;
    }
}
