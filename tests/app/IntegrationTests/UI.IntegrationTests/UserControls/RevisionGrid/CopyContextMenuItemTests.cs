using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid;
using ResourceManager;
using AppSettings = GitCommands.Settings.AppSettings;

namespace UITests.UserControls.RevisionGrid;

public class CopyContextMenuItemTests : IDisposable, IClassFixture<CopyContextMenuItemTestsFixture>
{
    private readonly CopyContextMenuItem _copyContextMenuItem = null!;

    public CopyContextMenuItemTests(CopyContextMenuItemTestsFixture fixture)
    {
        _copyContextMenuItem = new CopyContextMenuItem { Owner = new ToolStrip() };
    }

    public static IEnumerable<object[]> GetArtificialCommits
    {
        get
        {
            yield return [ObjectId.WorkTreeId];
            yield return [ObjectId.IndexId];
            yield return [ObjectId.CombinedDiffId];
        }
    }

    void IDisposable.Dispose()
    {
        DisposeTest();
    }

    private void DisposeTest()
    {
        _copyContextMenuItem.Owner!.Dispose();
        _copyContextMenuItem.Dispose();
    }

    [Fact]
    public void Should_should_contain_single_item_if_no_revision_supplied()
    {
        _copyContextMenuItem.SetRevisionFunc(() => null!);

        _copyContextMenuItem.ShowDropDown();

        _copyContextMenuItem.DropDownItems.Count.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(GetArtificialCommits))]
    public void Should_should_show_minimum_info_for_artificial_commits(ObjectId objectId)
    {
        GitRevision[] revisions = [new(objectId)];
        _copyContextMenuItem.SetRevisionFunc(() => revisions);

        _copyContextMenuItem.ShowDropDown();

        _copyContextMenuItem.DropDownItems.Count.Should().Be(4);

        _copyContextMenuItem.DropDownItems[0].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetCommitHash(1), 'C'));
        _copyContextMenuItem.DropDownItems[1].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetMessage(1), 'M'));
        _copyContextMenuItem.DropDownItems[2].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetAuthor(1), 'A'));
        _copyContextMenuItem.DropDownItems[3].Text.Should().StartWith(AddHotKey(TranslatedStrings.Date, 'D'));
    }

    [Fact]
    public void Should_show_info_if_commit_has_defined_branches()
    {
        GitRevision revision = new(ObjectId.Random());
        List<IGitRef> refs =
        [
            new GitRef(null!, revision.ObjectId, "refs/heads/branch1"),
            new GitRef(null!, revision.ObjectId, "refs/heads/branch2")
        ];
        revision.Refs = refs;
        GitRevision[] revisions = [revision];
        _copyContextMenuItem.SetRevisionFunc(() => revisions);

        _copyContextMenuItem.ShowDropDown();

        _copyContextMenuItem.DropDownItems.Count.Should().Be(8);
        _copyContextMenuItem.DropDownItems[0].Text.Should().Be(GitUI.TranslatedStrings.Branches);
        _copyContextMenuItem.DropDownItems[1].Text.Should().EndWith("branch1");
        _copyContextMenuItem.DropDownItems[2].Text.Should().EndWith("branch2");
        _copyContextMenuItem.DropDownItems[3].Should().BeOfType<ToolStripSeparator>();
        _copyContextMenuItem.DropDownItems[4].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetCommitHash(1), 'C'));
        _copyContextMenuItem.DropDownItems[5].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetMessage(1), 'M'));
        _copyContextMenuItem.DropDownItems[6].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetAuthor(1), 'A'));
        _copyContextMenuItem.DropDownItems[7].Text.Should().StartWith(AddHotKey(TranslatedStrings.Date, 'D'));
    }

    [Fact]
    public void Should_show_info_if_commit_has_defined_tags()
    {
        GitRevision revision = new(ObjectId.Random());
        List<IGitRef> refs =
        [
            new GitRef(null!, revision.ObjectId, "refs/tags/tag1"),
            new GitRef(null!, revision.ObjectId, "refs/tags/tag2")
        ];
        revision.Refs = refs;
        GitRevision[] revisions = [revision];
        _copyContextMenuItem.SetRevisionFunc(() => revisions);

        _copyContextMenuItem.ShowDropDown();

        _copyContextMenuItem.DropDownItems.Count.Should().Be(8);
        _copyContextMenuItem.DropDownItems[0].Text.Should().Be(GitUI.TranslatedStrings.Tags);
        _copyContextMenuItem.DropDownItems[1].Text.Should().EndWith("tag1");
        _copyContextMenuItem.DropDownItems[2].Text.Should().EndWith("tag2");
        _copyContextMenuItem.DropDownItems[3].Should().BeOfType<ToolStripSeparator>();
        _copyContextMenuItem.DropDownItems[4].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetCommitHash(1), 'C'));
        _copyContextMenuItem.DropDownItems[5].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetMessage(1), 'M'));
        _copyContextMenuItem.DropDownItems[6].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetAuthor(1), 'A'));
        _copyContextMenuItem.DropDownItems[7].Text.Should().StartWith(AddHotKey(TranslatedStrings.Date, 'D'));
    }

    [Fact]
    public void Should_show_info_if_commit_has_defined_branches_and_tags()
    {
        GitRevision revision = new(ObjectId.Random());
        List<IGitRef> refs =
        [
            new GitRef(null!, revision.ObjectId, "refs/tags/tag1"),
            new GitRef(null!, revision.ObjectId, "refs/heads/branch1"),
            new GitRef(null!, revision.ObjectId, "refs/tags/tag2"),
            new GitRef(null!, revision.ObjectId, "refs/heads/branch2")
        ];
        revision.Refs = refs;
        GitRevision[] revisions = [revision];
        _copyContextMenuItem.SetRevisionFunc(() => revisions);

        _copyContextMenuItem.ShowDropDown();

        _copyContextMenuItem.DropDownItems.Count.Should().Be(12);
        _copyContextMenuItem.DropDownItems[0].Text.Should().Be(GitUI.TranslatedStrings.Branches);
        _copyContextMenuItem.DropDownItems[1].Text.Should().EndWith("branch1");
        _copyContextMenuItem.DropDownItems[2].Text.Should().EndWith("branch2");
        _copyContextMenuItem.DropDownItems[3].Should().BeOfType<ToolStripSeparator>();
        _copyContextMenuItem.DropDownItems[4].Text.Should().Be(GitUI.TranslatedStrings.Tags);
        _copyContextMenuItem.DropDownItems[5].Text.Should().EndWith("tag1");
        _copyContextMenuItem.DropDownItems[6].Text.Should().EndWith("tag2");
        _copyContextMenuItem.DropDownItems[7].Should().BeOfType<ToolStripSeparator>();
        _copyContextMenuItem.DropDownItems[8].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetCommitHash(1), 'C'));
        _copyContextMenuItem.DropDownItems[9].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetMessage(1), 'M'));
        _copyContextMenuItem.DropDownItems[10].Text.Should().StartWith(AddHotKey(TranslatedStrings.GetAuthor(1), 'A'));
        _copyContextMenuItem.DropDownItems[11].Text.Should().StartWith(AddHotKey(TranslatedStrings.Date, 'D'));
    }

    [Fact]
    public void Should_should_show_info_for_multiple_commits()
    {
        GitRevision rev1 = new(ObjectId.Random())
        {
            Author = "Author1",
            AuthorEmail = "author1@foo.bla",
            AuthorUnixTime = DateTimeUtils.ToUnixTime(new DateTime(2018, 10, 23, 11, 34, 21))
        };
        GitRevision rev2 = new(ObjectId.Random())
        {
            Author = "Author2",
            AuthorEmail = "author2@foo.bla",
            Committer = "Committer2",
            CommitterEmail = "committer2@foo.bar",
            CommitUnixTime = DateTimeUtils.ToUnixTime(new DateTime(2018, 10, 23, 11, 34, 21))
        };
        GitRevision rev3 = new(ObjectId.Random()) { Author = "Author3", AuthorEmail = "author3@foo.bla" };
        GitRevision[] revisions = [rev1, rev2, rev3];
        _copyContextMenuItem.SetRevisionFunc(() => revisions);

        _copyContextMenuItem.ShowDropDown();

        _copyContextMenuItem.DropDownItems.Count.Should().Be(5);

        _copyContextMenuItem.DropDownItems[0].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetCommitHash(revisions.Length), 'C'));
        _copyContextMenuItem.DropDownItems[1].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetMessage(revisions.Length), 'M'));
        _copyContextMenuItem.DropDownItems[2].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetAuthor(revisions.Length), 'A'));
        _copyContextMenuItem.DropDownItems[3].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetAuthorDate(revisions.Length), 'T'));
        _copyContextMenuItem.DropDownItems[4].Text.Should()
            .StartWith(AddHotKey(TranslatedStrings.GetCommitDate(revisions.Length), 'D'));
    }

    private static string AddHotKey(string label, char? hotkey)
    {
        if (!hotkey.HasValue)
        {
            return label;
        }

        var position = label.IndexOf(hotkey.Value.ToString(), StringComparison.InvariantCultureIgnoreCase);
        if (position >= 0)
        {
            label = label.Insert(position, "&");
        }

        return label;
    }
}

public sealed class CopyContextMenuItemTestsFixture : IDisposable
{
    private readonly string? _originalTranslation;

    public CopyContextMenuItemTestsFixture()
    {
        _originalTranslation = AppSettings.CurrentTranslation;
        AppSettings.CurrentTranslation = "en";
    }

    void IDisposable.Dispose()
    {
        AppSettings.CurrentTranslation = _originalTranslation;
    }
}
