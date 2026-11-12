using GitCommands.UserRepositoryHistory;
using NSubstitute;
using AppSettings = GitCommands.Settings.AppSettings;

namespace GitCommandsTests.UserRepositoryHistory;

public class RemoteRepositoryManagerTests : IDisposable
{
    private const string _key = "history remote";
    private readonly RemoteRepositoryManager _manager = null!;
    private readonly IRepositoryStorage _repositoryStorage = null!;
    private readonly int _userSetting;

    public RemoteRepositoryManagerTests()
    {
        // backup the user setting, will restore it at the end of the test run
        _userSetting = AppSettings.RecentRepositoriesHistorySize;
        AppSettings.RecentRepositoriesHistorySize = 30;

        _repositoryStorage = Substitute.For<IRepositoryStorage>();
        _manager = new RemoteRepositoryManager(_repositoryStorage);
    }

    void IDisposable.Dispose()
    {
        AppSettings.RecentRepositoriesHistorySize = _userSetting;
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_add_new_path_as_top_entry()
    {
        const string repoToAdd = "https://path.to/add";
        List<Repository> history =
        [
            new("http://path1/"),
            new("http://path3/"),
            new("http://path4/"),
            new("http://path5/")
        ];
        _repositoryStorage.Load(_key).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(repoToAdd);
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_move_existing_path_as_top_entry()
    {
        const string repoToAdd = "https://path.to/add";
        List<Repository> history =
        [
            new("git://path1/"),
            new("git://path3/"),
            new("git://path4/"),
            new(repoToAdd),
            new("git://path5/")
        ];
        _repositoryStorage.Load(_key).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(repoToAdd);
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_move_only_first_existing_path_as_top_entry()
    {
        const string repoToAdd = "https://path.to/add";
        List<Repository> history =
        [
            new("ssh://path1/"),
            new("ssh://path3/"),
            new(repoToAdd),
            new("ssh://path4/"),
            new(repoToAdd),
            new("http://path5/")
        ];
        _repositoryStorage.Load(_key).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(6);
        newHistory[0].Path.Should().Be(repoToAdd);
        newHistory[4].Path.Should().Be(repoToAdd);
    }

    [Fact]
    public async Task AddAsMostRecentAsync_should_not_move_if_path_already_as_top_entry()
    {
        const string repoToAdd = "https://path.to/add";
        List<Repository> history =
        [
            new(repoToAdd),
            new("http://path1/"),
            new("http://path3/"),
            new("http://path4/"),
            new("http://path5/")
        ];
        _repositoryStorage.Load(_key).Returns(x => history);

        var newHistory = await _manager.AddAsMostRecentAsync(repoToAdd);

        newHistory.Should().HaveCount(5);
        newHistory[0].Path.Should().Be(repoToAdd);
        _repositoryStorage.DidNotReceive().Save(_key, Arg.Any<IList<Repository>>());
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
        _repositoryStorage.Load(_key).Returns(x => history);

        var newHistory = await _manager.RemoveRecentAsync(repoToDelete);

        newHistory.Should().HaveCount(4);
        newHistory.Should().NotContain(r => r.Path == repoToDelete);

        _repositoryStorage.Received(1).Load(_key);
        _repositoryStorage.Received(1)
            .Save(_key, Arg.Is<IEnumerable<Repository>>(h => h.All(r => r.Path != repoToDelete)));
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
        _repositoryStorage.Load(_key).Returns(x => history);

        var newHistory = await _manager.RemoveRecentAsync(repoToDelete);

        newHistory.Should().HaveCount(5);
        newHistory.Should().NotContain(r => r.Path == repoToDelete);

        _repositoryStorage.Received(1).Load(_key);
        _repositoryStorage.DidNotReceive().Save(_key, Arg.Any<IEnumerable<Repository>>());
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

        _repositoryStorage.Received(1).Save(_key, Arg.Is<IEnumerable<Repository>>(h => h.Count() == size));
    }
}
