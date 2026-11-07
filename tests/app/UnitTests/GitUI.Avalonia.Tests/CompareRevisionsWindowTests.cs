using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;

namespace GitUI.Avalonia.Tests;

public sealed class CompareRevisionsWindowTests
{
    [Fact]
    public void FormatFile_should_prefix_status_letter_and_show_rename()
    {
        CompareRevisionsWindow.FormatFile(new GitItemStatus(name: "a.txt") { IsNew = true }).Should().Be("A  a.txt");
        CompareRevisionsWindow.FormatFile(new GitItemStatus(name: "b.txt") { IsChanged = true }).Should().Be("M  b.txt");
        CompareRevisionsWindow.FormatFile(new GitItemStatus(name: "new.txt") { IsRenamed = true, OldName = "old.txt" })
            .Should().Be("R  old.txt -> new.txt");
    }
}
