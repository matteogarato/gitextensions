using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;

namespace GitCommandsTests.UserRepositoryHistory;

public class RecentRepoSplitterTests
{
    private const string _relativeLongRepoPath = @"this\is\a\very_very_very_very_very_very_very\long\repo_path";
    private static readonly string _repoPathInUserFolder = Path.Combine(Path.GetTempPath(), _relativeLongRepoPath);
    private static readonly string _repoAnchoredInTopPath1 = @"C:\this\is\a\repo_anchored_in_top_path1\";
    private static readonly string _repoAnchoredInTopPath2 = @"C:\this\is\a\repo_anchored_in_top_path2\";
    private static readonly string _repoAnchoredInRecentPath = @"C:\this\is\a\repo_anchored_in_recent_path\";
    private static readonly string _repoNotAnchoredPath = @"C:\this\is\a\repo_not_anchored_path\";

    #region Shortening strategy

    [Fact]
    public void SplitRecentRepos_Should_use_most_significant_folder_as_caption()
    {
        List<Repository> history =
        [
            new(_repoAnchoredInTopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];

        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        recentRepoList.Should().ContainSingle();
    }

    [Fact]
    public void SplitRecentRepos_Should_not_shorten_as_caption()
    {
        List<Repository> history =
        [
            new(_repoAnchoredInTopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];

        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.None };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().Be(_repoAnchoredInTopPath1);
        recentRepoList.Should().ContainSingle();
    }

    [Fact]
    public void SplitRecentRepos_Should_not_shorten_but_handle_user_folder_as_caption()
    {
        List<Repository> history =
        [
            new(_repoPathInUserFolder) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];

        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.None };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().StartWith(@"~\AppData").And.EndWith(_relativeLongRepoPath);
        recentRepoList.Should().ContainSingle();
    }

    [Fact]
    public void SplitRecentRepos_Should_display_middle_dots_in_caption()
    {
        // Warning: Able to shorten only an existing folder path
        Directory.CreateDirectory(_repoPathInUserFolder);

        List<Repository> history =
        [
            new(_repoPathInUserFolder) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];

        RecentRepoSplitter sut = new() { ShorteningStrategy = ShorteningRecentRepoPathStrategy.MiddleDots };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().ContainSingle();
        topRepoList[0].Caption.Should().Be(@"~\AppData\..\long\repo_path");
        recentRepoList.Should().ContainSingle();
    }

    #endregion

    #region Split repositories

    [Fact]
    public void SplitRecentRepos_Should_split_depending_anchor()
    {
        List<Repository> history =
        [
            new(_repoAnchoredInTopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new(_repoAnchoredInTopPath2) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new(_repoAnchoredInRecentPath) { Anchor = Repository.RepositoryAnchor.AnchoredInRecent },
            new(_repoNotAnchoredPath) { Anchor = Repository.RepositoryAnchor.None }
        ];

        RecentRepoSplitter sut = new()
        {
            ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir,
            SortTopRepos = false,
            SortRecentRepos = false
        };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().HaveCount(2);
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        topRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList.Should().HaveCount(4);
        recentRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        recentRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList[2].Caption.Should().Be("repo_anchored_in_recent_path");
        recentRepoList[3].Caption.Should().Be("repo_not_anchored_path");
    }

    [Fact]
    public void SplitRecentRepos_Should_split_depending_anchor_and_sort_alphabetically()
    {
        List<Repository> history =
        [
            // Unsorted!
            new(_repoNotAnchoredPath) { Anchor = Repository.RepositoryAnchor.None },
            new(_repoAnchoredInRecentPath) { Anchor = Repository.RepositoryAnchor.AnchoredInRecent },
            new(_repoAnchoredInTopPath2) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new(_repoAnchoredInTopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];

        RecentRepoSplitter sut = new()
        {
            ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir,
            SortTopRepos = true,
            SortRecentRepos = true
        };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().HaveCount(2);
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        topRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList.Should().HaveCount(4);
        recentRepoList[0].Caption.Should().Be("repo_anchored_in_recent_path");
        recentRepoList[1].Caption.Should().Be("repo_anchored_in_top_path1");
        recentRepoList[2].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList[3].Caption.Should().Be("repo_not_anchored_path");
    }

    [Fact]
    public void SplitRecentRepos_Should_split_depending_anchor_and_sort_alphabetically_Hiding_Top_Repo_In_Recent_list()
    {
        List<Repository> history =
        [
            // Unsorted!
            new(_repoNotAnchoredPath) { Anchor = Repository.RepositoryAnchor.None },
            new(_repoAnchoredInRecentPath) { Anchor = Repository.RepositoryAnchor.AnchoredInRecent },
            new(_repoAnchoredInTopPath2) { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new(_repoAnchoredInTopPath1) { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];

        RecentRepoSplitter sut = new()
        {
            ShorteningStrategy = ShorteningRecentRepoPathStrategy.MostSignDir,
            SortTopRepos = true,
            SortRecentRepos = true,
            HideTopRepositoriesFromRecentList = true
        };
        List<RecentRepoInfo> topRepoList = [];
        List<RecentRepoInfo> recentRepoList = [];

        sut.SplitRecentRepos(history, topRepoList, recentRepoList);

        topRepoList.Should().HaveCount(2);
        topRepoList[0].Caption.Should().Be("repo_anchored_in_top_path1");
        topRepoList[1].Caption.Should().Be("repo_anchored_in_top_path2");
        recentRepoList.Should().HaveCount(2);
        recentRepoList[0].Caption.Should().Be("repo_anchored_in_recent_path");
        recentRepoList[1].Caption.Should().Be("repo_not_anchored_path");
    }

    #endregion
}
