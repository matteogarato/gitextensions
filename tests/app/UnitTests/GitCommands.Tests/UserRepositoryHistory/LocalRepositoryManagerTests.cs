using GitCommands.UserRepositoryHistory;
using GitCommands.UserRepositoryHistory.Legacy;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;
using IRepositoryStorage = GitCommands.UserRepositoryHistory.IRepositoryStorage;
using Repository = GitCommands.UserRepositoryHistory.Repository;

namespace GitCommandsTests.UserRepositoryHistory;

public class LocalRepositoryManagerTests : IDisposable
{
    private const string _keyRecentHistory = "history";
    private const string _keyFavouriteHistory = "history-favourite";
    private readonly LocalRepositoryManager _manager = null!;
    private readonly IRepositoryHistoryMigrator _repositoryHistoryMigrator = null!;
    private readonly IRepositoryStorage _repositoryStorage = null!;
    private readonly int _userSetting;

    public LocalRepositoryManagerTests()
    {
        // backup the user setting, will restore it at the end of the test run
        _userSetting = AppSettings.RecentRepositoriesHistorySize;
        AppSettings.RecentRepositoriesHistorySize = 30;

        _repositoryStorage = Substitute.For<IRepositoryStorage>();
        _repositoryHistoryMigrator = Substitute.For<IRepositoryHistoryMigrator>();
        _manager = new LocalRepositoryManager(_repositoryStorage, _repositoryHistoryMigrator);
    }

    void IDisposable.Dispose()
    {
        AppSettings.RecentRepositoriesHistorySize = _userSetting;
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_add_new_path_as_top_entry()
    {
        const string repoToAdd = "path to add\\";
        List<Repository> history =
        [
            new("path1\\"),
            new("path3\\"),
            new("path4\\"),
            new("path5\\")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(repoToAdd);
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_move_existing_path_as_top_entry()
    {
        const string repoToAdd = "path to add\\";
        List<Repository> history =
        [
            new("path1\\"),
            new("path3\\"),
            new("path4\\"),
            new(repoToAdd),
            new("path5\\")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(repoToAdd);
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_move_only_first_existing_path_as_top_entry()
    {
        const string repoToAdd = "path to add\\";
        List<Repository> history =
        [
            new("path1\\"),
            new("path3\\"),
            new(repoToAdd),
            new("path4\\"),
            new(repoToAdd),
            new("path5\\")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(6);
        newHistory[0].Path.Should().Be(repoToAdd);
        newHistory[4].Path.Should().Be(repoToAdd);
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_not_move_if_path_already_as_top_entry()
    {
        const string repoToAdd = "path to add\\";
        List<Repository> history =
        [
            new(repoToAdd),
            new("path1\\"),
            new("path3\\"),
            new("path4\\"),
            new("path5\\")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(repoToAdd);
        _repositoryStorage.DidNotReceive().Save(_keyRecentHistory, Arg.Any<IList<Repository>>());
    }

    [Fact]
    public void AssignCategoryAsync_should_throw_if_key_null()
    {
        var f = async () => { await _manager.AssignCategoryAsync(null!, null); };
        f.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoadFavouriteHistoryAsync_should_return_empty_list_if_nothing_loaded()
    {
        _repositoryStorage.Load(_keyFavouriteHistory).Returns(x => null!);

        var history = await _manager.LoadFavouriteHistoryAsync();

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadFavouriteHistoryAsync_should_migrate_old_categorised_repositories()
    {
        _repositoryStorage.Load(_keyFavouriteHistory).Returns(x => new List<Repository>());

        await _manager.LoadFavouriteHistoryAsync();

#pragma warning disable 4014
        _repositoryHistoryMigrator.Received(1).MigrateAsync(Arg.Any<IList<Repository>>());
#pragma warning restore 4014
    }

    [Fact]
    public async Task LoadRecentHistoryAsync_should_return_empty_list_if_nothing_loaded()
    {
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => null!);

        var history = await _manager.LoadRecentHistoryAsync();

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadRecentHistoryAsync_should_trim_history_per_settings()
    {
        const int size = 3;
        AppSettings.RecentRepositoriesHistorySize = size;
        List<Repository> history =
        [
            new("path1") { Category = "my" },
            new("path2"),
            new("path3"),
            new("path4") { Category = "another" },
            new("path5"),
            new("path6"),
            new("path7")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var repositories = await _manager.LoadRecentHistoryAsync();

        repositories.Should().HaveCount(size);
        repositories.Select(r => r.Path).Should().ContainInOrder("path1", "path2", "path3");
    }

    [Fact]
    public async Task LoadRecentHistoryAsync_should_keep_anchored_repositories_when_trimming()
    {
        const int size = 3;
        AppSettings.RecentRepositoriesHistorySize = size;
        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new("path3"),
            new("path4"),
            new("path5") { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new("path6") { Anchor = Repository.RepositoryAnchor.AnchoredInRecent }
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var repositories = await _manager.LoadRecentHistoryAsync();

        repositories.Should().HaveCount(size);
        repositories.Select(r => r.Path).Should().ContainInOrder("path1", "path5", "path6");
        repositories.Select(r => r.Path).Should().NotContain(["path2", "path3", "path4"]);
    }

    [Fact]
    public async Task LoadRecentHistoryAsync_should_keep_all_anchored_repositories_when_they_exceed_size()
    {
        const int size = 2;
        AppSettings.RecentRepositoriesHistorySize = size;
        List<Repository> history =
        [
            new("path1"),
            new("path2") { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new("path3") { Anchor = Repository.RepositoryAnchor.AnchoredInTop },
            new("path4") { Anchor = Repository.RepositoryAnchor.AnchoredInTop }
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var repositories = await _manager.LoadRecentHistoryAsync();

        repositories.Select(r => r.Path).Should().ContainInOrder("path2", "path3", "path4");
        repositories.Select(r => r.Path).Should().NotContain("path1");
    }

    [Fact]
    public async Task RemoveRecentAsync_should_remove_if_exists()
    {
        const string repoToDelete = "path to delete";
        List<Repository> history =
        [
            new("path1"),
            new(repoToDelete),
            new("path3"),
            new("path4"),
            new("path5")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var newHistory = await _manager.RemoveRecentAsync(repoToDelete);

        newHistory.Should().HaveCount(4);
        newHistory.Should().NotContain(r => r.Path == repoToDelete);

        _repositoryStorage.Received(1).Load(_keyRecentHistory);
        _repositoryStorage.Received(1).Save(_keyRecentHistory,
            Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path != repoToDelete)));
    }

    [Fact]
    public async Task RemoveFavouriteAsync_should_not_crash_if_not_exists()
    {
        const string repoToDelete = "path to delete";
        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new("path3"),
            new("path4"),
            new("path5")
        ];
        _repositoryStorage.Load(_keyFavouriteHistory).Returns(x => history);
        _repositoryHistoryMigrator.MigrateAsync(Arg.Any<List<Repository>>()).Returns(x => (history, false));

        var newHistory = await _manager.RemoveFavouriteAsync(repoToDelete);

        newHistory.Should().HaveCount(5);
        newHistory.Should().NotContain(r => r.Path == repoToDelete);

        _repositoryStorage.Received(1).Load(_keyFavouriteHistory);
        _repositoryStorage.DidNotReceive().Save(_keyFavouriteHistory, Arg.Any<IEnumerable<Repository>>());
#pragma warning disable 4014
        _repositoryHistoryMigrator.Received(1).MigrateAsync(history);
#pragma warning restore 4014
    }

    [Fact]
    public async Task RemoveFavouriteAsync_should_remove_if_exists()
    {
        const string repoToDelete = "path to delete";
        List<Repository> history =
        [
            new("path1"),
            new(repoToDelete),
            new("path3"),
            new("path4"),
            new("path5")
        ];
        _repositoryStorage.Load(_keyFavouriteHistory).Returns(x => history);
        _repositoryHistoryMigrator.MigrateAsync(Arg.Any<List<Repository>>()).Returns(x => (history, false));

        var newHistory = await _manager.RemoveFavouriteAsync(repoToDelete);

        newHistory.Should().HaveCount(4);
        newHistory.Should().NotContain(r => r.Path == repoToDelete);

        _repositoryStorage.Received(1).Load(_keyFavouriteHistory);
        _repositoryStorage.Received(1).Save(_keyFavouriteHistory,
            Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path != repoToDelete)));
#pragma warning disable 4014
        _repositoryHistoryMigrator.Received(1).MigrateAsync(history);
#pragma warning restore 4014
    }

    [Fact]
    public async Task RemoveRecentAsync_should_not_crash_if_not_exists()
    {
        const string repoToDelete = "path to delete";
        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new("path3"),
            new("path4"),
            new("path5")
        ];
        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        var newHistory = await _manager.RemoveRecentAsync(repoToDelete);

        newHistory.Should().HaveCount(5);
        newHistory.Should().NotContain(r => r.Path == repoToDelete);

        _repositoryStorage.Received(1).Load(_keyRecentHistory);
        _repositoryStorage.DidNotReceive().Save(_keyRecentHistory, Arg.Any<IEnumerable<Repository>>());
    }

    [Fact]
    public void SaveFavouriteHistoryAsync_should_throw_if_repositories_null()
    {
        var action = async () => await _manager.SaveFavouriteHistoryAsync(null!);
        action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveFavouriteHistoryAsync_should_save()
    {
        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new("path3"),
            new("path4"),
            new("path5")
        ];

        await _manager.SaveFavouriteHistoryAsync(history);

        _repositoryStorage.Received(1).Save(_keyFavouriteHistory, history);
    }

    [Fact]
    public void SaveRecentHistoryAsync_should_throw_if_repositories_null()
    {
        var action = async () => await _manager.SaveRecentHistoryAsync(null!);
        action.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveRecentHistoryAsync_should_trim_history_size()
    {
        const int size = 3;
        AppSettings.RecentRepositoriesHistorySize = size;
        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new("path3"),
            new("path4"),
            new("path5")
        ];

        await _manager.SaveRecentHistoryAsync(history);

        _repositoryStorage.Received(1).Save(_keyRecentHistory, Arg.Is<IEnumerable<Repository>>(h => h.Count() == size));
    }

    [Fact]
    public async Task RemoveInvalidRepositoriesAsync_should_apply_predicate()
    {
        List<Repository> history =
        [
            new("Even"),
            new("Odd"),
            new("EvenEven"),
            new("AlsoOdd")
        ];

        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        await _manager.RemoveInvalidRepositoriesAsync(repoPath => repoPath.Length % 2 == 0);

        _repositoryStorage.Received(1).Load(_keyRecentHistory);
        _repositoryStorage.Received(1).Save(_keyRecentHistory,
            Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path.Length % 2 == 0)));
        _repositoryStorage.Received(1).Save(_keyRecentHistory, Arg.Is<IEnumerable<Repository>>(h => h.Count() == 2));
    }

    [Fact]
    public async Task RemoveInvalidRepositoriesAsync_should_not_apply_predicate()
    {
        List<Repository> history =
        [
            new("Even"),
            new("Odd"),
            new("EvenEven"),
            new("AlsoOdd")
        ];

        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        await _manager.RemoveInvalidRepositoriesAsync(repoPath => repoPath.Length < 20);

        _repositoryStorage.Received(1).Load(_keyRecentHistory);
        _repositoryStorage.DidNotReceive().Save(_keyRecentHistory, Arg.Any<IEnumerable<Repository>>());
    }

    [Fact]
    public async Task RemoveInvalidRepositoriesAsync_should_remove_recent_by_predicate()
    {
        const string repoToDelete = "different path";

        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new(repoToDelete)
        ];

        _repositoryStorage.Load(_keyRecentHistory).Returns(x => history);

        await _manager.RemoveInvalidRepositoriesAsync(repoPath => repoPath.StartsWith('p'));

        _repositoryStorage.Received(1).Load(_keyRecentHistory);
        _repositoryStorage.Received(1).Save(_keyRecentHistory, Arg.Is<IEnumerable<Repository>>(h => h.Count() == 2));
        _repositoryStorage.Received(1).Save(_keyRecentHistory,
            Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path != repoToDelete)));
    }

    [Fact]
    public async Task RemoveInvalidRepositoriesAsync_should_remove_favourite_by_predicate()
    {
        const string repoToDelete = "different path";

        List<Repository> history =
        [
            new("path1"),
            new("path2"),
            new(repoToDelete)
        ];

        _repositoryStorage.Load(_keyFavouriteHistory).Returns(x => history);
        _repositoryHistoryMigrator.MigrateAsync(Arg.Any<List<Repository>>()).Returns(x => (history, false));

        await _manager.RemoveInvalidRepositoriesAsync(repoPath => repoPath.StartsWith('p'));

        _repositoryStorage.Received(1).Load(_keyFavouriteHistory);
        _repositoryStorage.Received(1).Save(_keyFavouriteHistory,
            Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path != repoToDelete)));
        _repositoryStorage.Received(1).Save(_keyFavouriteHistory, Arg.Is<IEnumerable<Repository>>(h => h.Count() == 2));
    }
}
