using GitCommands.Git;
using GitCommands.Settings;
using GitCommands.UserRepositoryHistory;
using GitExtUtils.GitUI;

namespace GitUI.CommandsDialogs.BrowseDialog.DashboardControl;

public interface IUserRepositoriesListController
{
    Task AssignCategoryAsync(Repository repository, string? category);
    string GetCurrentBranchName(string path);
    bool IsValidGitWorkingDir(string path);

    (IReadOnlyList<RecentRepoInfo> recentRepositories, IReadOnlyList<RecentRepoInfo> favouriteRepositories)
        PreRenderRepositories(string filter);

    bool RemoveInvalidRepository(string path);
    void ClearCache();
}

public sealed class UserRepositoriesListController(
    ILocalRepositoryManager localRepositoryManager,
    IInvalidRepositoryRemover invalidRepositoryRemover,
    IRepositoryCurrentBranchNameCache branchNameCache)
    : IUserRepositoriesListController
{
    private IList<Repository>? _allFavoriteRepositories;

    // Holds the raw, unfiltered list of repositories.
    // This is done to allow fast filtering of all known repos.
    private IList<Repository>? _allRecentRepositories;

    async Task IUserRepositoriesListController.AssignCategoryAsync(Repository repository, string? category)
    {
        ArgumentNullException.ThrowIfNull(repository);

        await localRepositoryManager.AssignCategoryAsync(repository, category);
    }

    /// <summary>
    ///     Clears the repository cache. After this call the repository list will be loaded from disk.
    ///     Note: The info in _branchNameCache is updated by Dashboard (but not read), the data is shared
    ///     with the repo menus in both Dashboard and Browse.
    /// </summary>
    void IUserRepositoriesListController.ClearCache()
    {
        _allRecentRepositories = null;
        _allFavoriteRepositories = null;
        branchNameCache.InvalidateAll();
    }

    string IUserRepositoriesListController.GetCurrentBranchName(string path)
    {
        if (!AppSettings.ShowRepoCurrentBranch || GitModule.IsBareRepository(path))
        {
            return string.Empty;
        }

        return branchNameCache.GetCurrentBranchName(path);
    }

    bool IUserRepositoriesListController.IsValidGitWorkingDir(string path)
    {
        return GitModule.IsValidGitWorkingDir(path);
    }

    (IReadOnlyList<RecentRepoInfo> recentRepositories, IReadOnlyList<RecentRepoInfo> favouriteRepositories)
        IUserRepositoriesListController.PreRenderRepositories(string pattern)
    {
        List<RecentRepoInfo> topRepos = [];
        List<RecentRepoInfo> recentRepos = [];

        RecentRepoSplitter splitter = new()
        {
            MeasureFont = AppSettings.Font,
            MaxTopRepositories = AppSettings.MaxTopRepositories,
            RecentReposComboMinWidth = AppSettings.RecentReposComboMinWidth,
            ShorteningStrategy = AppSettings.ShorteningRecentRepoPathStrategy,
            SortRecentRepos = AppSettings.SortRecentRepos,
            SortTopRepos = AppSettings.SortTopRepos
        };

        _allRecentRepositories ??=
            ThreadHelper.JoinableTaskFactory.Run(RepositoryHistoryManager.Locals.LoadRecentHistoryAsync);
        var repositories = Filter(_allRecentRepositories, pattern);
        splitter.SplitRecentRepos(repositories, topRepos, recentRepos);
        List<RecentRepoInfo> recentRepositories = [.. topRepos.Union(recentRepos)];

        _allFavoriteRepositories ??=
            ThreadHelper.JoinableTaskFactory.Run(RepositoryHistoryManager.Locals.LoadFavouriteHistoryAsync);
        repositories = Filter(_allFavoriteRepositories, pattern);
        topRepos.Clear();
        recentRepos.Clear();
        splitter.SplitRecentRepos(repositories, topRepos, recentRepos);
        List<RecentRepoInfo> favouriteRepositories = [.. topRepos.Union(recentRepos)];

        return (recentRepositories, favouriteRepositories);
    }

    bool IUserRepositoriesListController.RemoveInvalidRepository(string path)
    {
        return invalidRepositoryRemover.ShowDeleteInvalidRepositoryDialog(path);
    }

    private static IList<Repository> Filter(IList<Repository> repositories, string pattern)
    {
        if (pattern.Length == 0)
        {
            return repositories;
        }

        return [.. repositories.Where(r => r.Path.Contains(pattern, StringComparison.CurrentCultureIgnoreCase))];
    }
}
