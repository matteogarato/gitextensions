using GitCommands.Git;
using GitUIPluginInterfaces;

namespace GitCommandsTests.Git;
public class RevisionDiffProviderTest
{
    // See RevisionDiffProvider.ArtificialToDiffOptions() for possible "aliases" for artificial commits
    // All variants are not tested in all situations

    private RevisionDiffProvider _revisionDiffProvider = null!;

    public RevisionDiffProviderTest()
    {
        _revisionDiffProvider = new RevisionDiffProvider();
    }

#if !DEBUG
    // Test cases that should assert in debug; should not occur but undefined behavior that should be blocked in GUI
    // Cannot compare worktree to worktree or index to index but give predictive output in release builds

    // Two empty parameters will compare working dir to index
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(GitRevision.WorkTreeGuid)]
    public void RevisionDiffProvider_should_return_empty_if_To_is_WorkTreeGuid(string? firstRevision)
    {
        _revisionDiffProvider.Get(firstRevision, GitRevision.WorkTreeGuid).Arguments.Should().BeEmpty();
    }

    // Two index revisions gives duplicated options, no reason to clean
    [Theory]
    [InlineData("^")]
    [InlineData(GitRevision.IndexGuid)]
    public void RevisionDiffProvider_should_return_cached_if_both_IndexGuid(string firstRevision)
    {
        _revisionDiffProvider.Get(firstRevision, GitRevision.IndexGuid).Arguments.Should().Be("--cached --cached");
    }
#endif

    // Combined Diff artificial commit should not be included in diffs
    [Theory]
    [InlineData(GitRevision.CombinedDiffGuid, "")]
    [InlineData("", GitRevision.CombinedDiffGuid)]
    public void RevisionDiffProvider_should_throw_if_any_combined_diff(string firstRevision, string? secondRevision)
    {
        ((Action)(() => _revisionDiffProvider.Get(firstRevision, secondRevision))).Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(GitRevision.IndexGuid, GitRevision.WorkTreeGuid)]
    [InlineData("^", "")]
    [InlineData(GitRevision.IndexGuid, null)]
    public void RevisionDiffProvider_index_to_worktree(string firstRevision, string? secondRevision)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision).Arguments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(GitRevision.WorkTreeGuid, GitRevision.IndexGuid)]
    [InlineData("", "^")]
    public void RevisionDiffProvider_worktree_to_index(string firstRevision, string? secondRevision)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision).Arguments.Should().Be("-R");
    }

    [Theory]
    [InlineData(GitRevision.WorkTreeGuid + "^^")]
    [InlineData(GitRevision.IndexGuid + "^")]
    [InlineData("HEAD")]
    public void RevisionDiffProvider_head_to_worktree(string firstRevision)
    {
        _revisionDiffProvider.Get(firstRevision, GitRevision.WorkTreeGuid).Arguments.Should().Be("\"HEAD\"");
    }

    [Theory]
    [InlineData(GitRevision.IndexGuid + "^", "^")]
    [InlineData("HEAD", GitRevision.IndexGuid)]
    public void RevisionDiffProvider_head_to_index(string firstRevision, string? secondRevision)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision).Arguments.Should().Be("--cached \"HEAD\"");
    }

    [Theory]
    [InlineData(GitRevision.IndexGuid, "HEAD")]
    public void RevisionDiffProvider_index_to_head(string firstRevision, string? secondRevision)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision).Arguments.Should().Be("-R --cached \"HEAD\"");
    }

    [Theory]
    [InlineData("HEAD", "123456789")]
    public void RevisionDiffProvider_normal1(string firstRevision, string? secondRevision)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision).Arguments.Should().Be("\"HEAD\" \"123456789\"");
    }

    [Theory]
    [InlineData("123456789", "HEAD")]
    public void RevisionDiffProvider_normal2(string firstRevision, string? secondRevision)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision).Arguments.Should().Be("\"123456789\" \"HEAD\"");
    }

    // Standard usage when filename is included
    [Theory]
    [InlineData("123456789", GitRevision.WorkTreeGuid, "a.txt", null, true)]
    public void RevisionDiffProvider_fileName_tracked(string firstRevision, string? secondRevision, string? fileName, string? oldFileName, bool isTracked)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked).Arguments.Should().Be("\"123456789\" -- \"a.txt\"");
    }

    // If fileName is null, ignore oldFileName and tracked
    [Theory]
    [InlineData("123456789", "HEAD", null, "b.txt", true)]
    public void RevisionDiffProvider_fileName_null_with_old_name(string firstRevision, string? secondRevision, string? fileName, string? oldFileName, bool isTracked)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked).Arguments.Should().Be("\"123456789\" \"HEAD\"");
    }

    // Include old filename if is included
    [Theory]
    [InlineData("123456789", "234567890", "a.txt", "b.txt", true)]
    public void RevisionDiffProvider_fileName_old_filename(string firstRevision, string? secondRevision, string? fileName, string? oldFileName, bool isTracked)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked).Arguments.Should().Be("\"123456789\" \"234567890\" -- \"a.txt\" \"b.txt\"");
    }

    // normal test case when untracked is set
    [Theory]
    [InlineData(GitRevision.IndexGuid, GitRevision.WorkTreeGuid, "a.txt", null, false)]
    public void RevisionDiffProvider_fileName_untracked(string firstRevision, string? secondRevision, string? fileName, string? oldFileName, bool isTracked)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked).Arguments.Should().Be("--no-index -- \"/dev/null\" \"a.txt\"");
    }

    // If fileName is null, ignore oldFileName and tracked
    [Theory]
    [InlineData(GitRevision.IndexGuid, GitRevision.WorkTreeGuid, null, "b.txt", false)]
    public void RevisionDiffProvider_fileName_null_Untracked(string firstRevision, string? secondRevision, string? fileName, string? oldFileName, bool isTracked)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked).Arguments.Should().BeEmpty();
    }

    // Ignore revisions for untracked
    [Theory]
    [InlineData("123456789", "234567890", "a.txt", "b.txt", false)]
    public void RevisionDiffProvider_fileName_old_filename_Untracked(string firstRevision, string? secondRevision, string? fileName, string? oldFileName, bool isTracked)
    {
        _revisionDiffProvider.Get(firstRevision, secondRevision, fileName, oldFileName, isTracked).Arguments.Should().Be("--no-index -- \"/dev/null\" \"a.txt\"");
    }
}
