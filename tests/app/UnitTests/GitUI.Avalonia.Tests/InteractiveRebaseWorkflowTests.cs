using AwesomeAssertions;
using GitUI.CommandsDialogs;

namespace GitUI.Avalonia.Tests;

public sealed class InteractiveRebaseWorkflowTests
{
    [Fact]
    public void ParseCommits_should_split_hash_and_subject()
    {
        IReadOnlyList<RebaseTodoEntry> entries = InteractiveRebaseWorkflow.ParseCommits("aaa\tFirst\nbbb\tSecond with\ttab\n");

        entries.Select(entry => entry.CommitId).Should().Equal("aaa", "bbb");
        entries[1].Subject.Should().Be("Second with\ttab");
    }

    [Fact]
    public void BuildTodo_should_write_one_lowercase_action_line_per_entry()
    {
        RebaseTodoEntry[] entries = [new("aaa", "A"), new("bbb", "B", RebaseTodoAction.Fixup), new("ccc", "C", RebaseTodoAction.Drop)];

        InteractiveRebaseWorkflow.BuildTodo(entries).Should().Be("pick aaa A\nfixup bbb B\ndrop ccc C\n");
    }

    [Fact]
    public void Validate_should_reject_a_leading_squash_or_fixup_ignoring_dropped_commits()
    {
        InteractiveRebaseWorkflow.Validate([new("a", "A", RebaseTodoAction.Drop), new("b", "B", RebaseTodoAction.Squash)]).Should().NotBeNull();
        InteractiveRebaseWorkflow.Validate([new("a", "A"), new("b", "B", RebaseTodoAction.Squash)]).Should().BeNull();
    }

    [Fact]
    public void BuildCommand_should_replace_the_sequence_editor_with_a_copy_of_the_todo_file()
    {
        string command = InteractiveRebaseWorkflow.BuildCommand(new RebaseConfiguration("main", false, true, false, null), "/tmp/todo.txt");

        command.Should().Contain("sequence.editor=").And.Contain("cp").And.Contain("/tmp/todo.txt").And.Contain("rebase");
    }

    [Fact]
    public void GetRange_should_use_the_upstream_of_a_range_rebase()
    {
        InteractiveRebaseWorkflow.GetRange(new RebaseConfiguration("main", false, true, false, null)).Should().Be("main..HEAD");
        InteractiveRebaseWorkflow.GetRange(new RebaseConfiguration("main", false, true, false, "abc")).Should().Be("abc..HEAD");
    }
}
