using AwesomeAssertions;
using GitExtensions.Extensibility.Git;
using GitUI.CommandsDialogs;
using GitUIPluginInterfaces;

namespace GitUI.Avalonia.Tests;

public sealed class RevisionCopyWorkflowTests
{
    private static readonly ObjectId _id = ObjectId.Parse("0123456789abcdef0123456789abcdef01234567");

    [Fact]
    public void Format_should_return_full_and_short_hash()
    {
        GitRevision revision = new(_id) { Subject = "Fix" };

        RevisionCopyWorkflow.Format([revision], RevisionCopyKind.FullHash).Should().Be(_id.ToString());
        RevisionCopyWorkflow.Format([revision], RevisionCopyKind.ShortHash).Should().Be(_id.ToShortString());
        RevisionCopyWorkflow.Format([revision], RevisionCopyKind.HashAndSubject).Should().Be($"{_id.ToShortString()} Fix");
    }

    [Fact]
    public void Format_should_join_multiple_revisions_one_per_line_and_include_author_email()
    {
        GitRevision first = new(_id) { Subject = "A", Author = "Ann", AuthorEmail = "ann@x.org" };
        GitRevision second = new(_id) { Subject = "B", Author = "Bob" };

        RevisionCopyWorkflow.Format([first, second], RevisionCopyKind.Subject).Should().Be("A" + Environment.NewLine + "B");
        RevisionCopyWorkflow.Format([first, second], RevisionCopyKind.Author).Should().Be("Ann <ann@x.org>" + Environment.NewLine + "Bob");
    }
}
