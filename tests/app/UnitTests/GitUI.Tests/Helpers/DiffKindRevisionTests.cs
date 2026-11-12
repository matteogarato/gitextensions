using GitExtensions.Extensibility.Git;
using GitUI;

namespace GitUITests.Helpers;

public class DiffKindRevisionTests
{
    [Fact]
    public void DiffKindRevisionTests_error()
    {
        IReadOnlyList<GitRevision>? revisions = null;
        RevisionDiffInfoProvider.TryGet(revisions, RevisionDiffKind.DiffAb, out _, out _, out _).Should()
            .BeFalse("null rev");

        revisions = new List<GitRevision> { null! };
        RevisionDiffInfoProvider.TryGet(revisions, RevisionDiffKind.DiffAb, out _, out _, out _).Should()
            .BeFalse("1 null rev");

        revisions = new List<GitRevision> { null!, null! };
        RevisionDiffInfoProvider.TryGet(revisions, RevisionDiffKind.DiffAb, out _, out _, out _).Should()
            .BeFalse("2 null rev");

        var head = ObjectId.Random();
        revisions = new List<GitRevision> { new(head), null! };
        RevisionDiffInfoProvider.TryGet(revisions, RevisionDiffKind.DiffBLocal, out _, out _, out _).Should()
            .BeFalse("2nd null rev DiffBLocal");
    }

    [Fact]
    public void DiffKindRevisionTests_AB_1p()
    {
        var parent = ObjectId.Random();
        var head = ObjectId.Random();
        GitRevision[] revisions = [new(head) { ParentIds = new[] { parent } }];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffAb, out var firstRevision, out var secondRevision, out _)
            .Should().BeTrue("null rev");
        firstRevision.Should().Be(parent.ToString(), "first");
        secondRevision.Should().Be(head.ToString(), "second");
    }

    [Fact]
    public void DiffKindRevisionTests_AB_1h()
    {
        var head = ObjectId.Random();
        GitRevision[] revisions = [new(head)];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffAb, out var firstRevision, out var secondRevision, out _)
            .Should().BeTrue("null rev");
        firstRevision.Should().Be($"{head}^", "first");
        secondRevision.Should().Be(head.ToString(), "second");
    }

    [Fact]
    public void DiffKindRevisionTests_AB_2()
    {
        var head = ObjectId.Random();
        var headParent = ObjectId.Random();
        GitRevision[] revisions = [new(head), new(headParent)];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffAb, out var firstRevision, out var secondRevision, out _)
            .Should().BeTrue("null rev");
        firstRevision.Should().Be(headParent.ToString(), "first");
        secondRevision.Should().Be(head.ToString(), "second");
    }

    [Fact]
    public void DiffKindRevisionTests_AB_null()
    {
        var head = ObjectId.Random();
        GitRevision[] revisions = [new(head), null!];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffAb, out var firstRevision, out var secondRevision, out _)
            .Should().BeTrue("null rev");
        firstRevision.Should().Be("--root", "first");
        secondRevision.Should().Be(head.ToString(), "second");
    }

    [Fact]
    public void DiffKindRevisionTests_AL_1()
    {
        var head = ObjectId.Random();
        GitRevision[] revisions = [new(head)];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffALocal, out var firstRevision, out var secondRevision,
                out _).Should().BeTrue("null rev");
        firstRevision.Should().Be($"{head}^", "first");
        secondRevision.Should().Be(null, "second");
    }

    [Fact]
    public void DiffKindRevisionTests_AL_2()
    {
        var head = ObjectId.Random();
        var headParent = ObjectId.Random();
        GitRevision[] revisions = [new(head), new(headParent)];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffALocal, out var firstRevision, out var secondRevision,
                out _).Should().BeTrue("null rev");
        firstRevision.Should().Be(headParent.ToString(), "first");
        secondRevision.Should().Be(null, "second");
    }

    [Fact]
    public void DiffKindRevisionTests_AL_3()
    {
        var head = ObjectId.Random();
        GitRevision[] revisions = [new(head), null!];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffALocal, out var firstRevision, out var secondRevision,
                out _).Should().BeTrue("null rev");
        firstRevision.Should().Be("--root", "first");
        secondRevision.Should().Be(null, "second");
    }

    [Fact]
    public void DiffKindRevisionTests_BL_1()
    {
        var head = ObjectId.Random();
        GitRevision[] revisions = [new(head)];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffBLocal, out var firstRevision, out var secondRevision,
                out _).Should().BeTrue("null rev");
        firstRevision.Should().Be(head.ToString(), "first");
        secondRevision.Should().Be(null, "second");
    }

    [Fact]
    public void DiffKindRevisionTests_BL_2()
    {
        var head = ObjectId.Random();
        var headParent = ObjectId.Random();
        GitRevision[] revisions = [new(head), new(headParent)];

        RevisionDiffInfoProvider
            .TryGet(revisions, RevisionDiffKind.DiffBLocal, out var firstRevision, out var secondRevision,
                out _).Should().BeTrue("null rev");
        firstRevision.Should().Be(head.ToString(), "first");
        secondRevision.Should().Be(null, "second");
    }
}
