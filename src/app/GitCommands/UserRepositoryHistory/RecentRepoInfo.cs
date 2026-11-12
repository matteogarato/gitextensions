using GitCommands.Settings;

namespace GitCommands.UserRepositoryHistory;

public class RecentRepoInfo
{
    public RecentRepoInfo(Repository repo, bool topRepo, bool anchored)
    {
        Repo = repo;
        TopRepo = topRepo;
        Anchored = anchored;
        try
        {
            DirInfo = new DirectoryInfo(Repo.Path);
        }
        catch (SystemException)
        {
            DirInfo = null;
            Caption = Repo.Path;
        }

        if (DirInfo is not null)
        {
            ShortName = DirInfo.Name;
            DirInfo = DirInfo.Parent;
        }

        DirName = DirInfo?.FullName ?? "";
    }

    public Repository Repo { get; }
    public string? Caption { get; set; }
    public bool TopRepo { get; set; }
    public bool Anchored { get; set; }
    public DirectoryInfo? DirInfo { get; set; }
    public string? ShortName { get; }
    public string DirName { get; }

    public bool FullPath
    {
        get => DirInfo is null;
    }

    public override string ToString()
    {
        return Repo.ToString();
    }
}

public class RecentRepoSplitter
{
    public int MaxTopRepositories { get; set; } = AppSettings.MaxTopRepositories;
    public bool HideTopRepositoriesFromRecentList { get; set; } = AppSettings.HideTopRepositoriesFromRecentList.Value;

    public ShorteningRecentRepoPathStrategy ShorteningStrategy { get; set; } =
        AppSettings.ShorteningRecentRepoPathStrategy;

    public bool SortTopRepos { get; set; } = AppSettings.SortTopRepos;
    public bool SortRecentRepos { get; set; } = AppSettings.SortRecentRepos;
    public int RecentReposComboMinWidth { get; set; } = AppSettings.RecentReposComboMinWidth;

    public Font? MeasureFont { get; set; }

    public void SplitRecentRepos(IList<Repository> repositories, List<RecentRepoInfo> topRepoList,
        List<RecentRepoInfo> recentRepoList)
    {
        SortedList<string, List<RecentRepoInfo>> orderedRepos = [];
        List<RecentRepoInfo> topRepos = [];
        List<RecentRepoInfo> recentRepos = [];

        var middleDot = ShorteningStrategy == ShorteningRecentRepoPathStrategy.MiddleDots;
        var signDir = ShorteningStrategy == ShorteningRecentRepoPathStrategy.MostSignDir;

        var n = Math.Min(MaxTopRepositories, repositories.Count);

        // the topRepositories repositories will be added at beginning
        // rest will be added in alphabetical order
        foreach (var repository in repositories)
        {
            var topRepo = (topRepos.Count < n && repository.Anchor == Repository.RepositoryAnchor.None) ||
                          repository.Anchor == Repository.RepositoryAnchor.AnchoredInTop;
            RecentRepoInfo ri = new(repository, topRepo,
                repository.Anchor is Repository.RepositoryAnchor.AnchoredInTop
                    or Repository.RepositoryAnchor.AnchoredInRecent);
            if (ri.TopRepo)
            {
                topRepos.Add(ri);
            }

            if (!HideTopRepositoriesFromRecentList || !ri.TopRepo)
            {
                recentRepos.Add(ri);
            }

            if (middleDot)
            {
                AddToOrderedMiddleDots(orderedRepos, ri);
            }
            else
            {
                AddToOrderedSignDir(orderedRepos, ri, signDir);
            }

            if (ri.Caption is not null)
            {
                ri.Caption = PathUtil.GetDisplayPath(ri.Caption);
            }
        }

        var r = topRepos.Count - 1;

        // remove not anchored repos if there is more than maxRecentRepositories repos
        while (topRepos.Count > n && r >= 0)
        {
            var repo = topRepos[r];
            if (repo.Repo.Anchor == Repository.RepositoryAnchor.AnchoredInTop)
            {
                r--;
            }
            else
            {
                repo.TopRepo = false;
                topRepos.RemoveAt(r);
            }
        }

        void AddSortedRepos(bool topRepo, List<RecentRepoInfo> addToList)
        {
            addToList.AddRange(
                from caption in orderedRepos.Keys
                from repo in orderedRepos[caption]
                where repo.TopRepo == topRepo || (!topRepo && !HideTopRepositoriesFromRecentList)
                select repo);
        }

        void AddNotSortedRepos(List<RecentRepoInfo> list, List<RecentRepoInfo> addToList)
        {
            addToList.AddRange(list);
        }

        if (SortTopRepos)
        {
            AddSortedRepos(true, topRepoList);
        }
        else
        {
            AddNotSortedRepos(topRepos, topRepoList);
        }

        if (SortRecentRepos)
        {
            AddSortedRepos(false, recentRepoList);
        }
        else
        {
            AddNotSortedRepos(recentRepos, recentRepoList);
        }
    }

    private static void AddToOrderedSignDir(SortedList<string, List<RecentRepoInfo>> orderedRepos,
        RecentRepoInfo repoInfo, bool shortenPath)
    {
        // if there is no short name for a repo, then try to find unique caption extending short directory path
        if (shortenPath && repoInfo.DirInfo is not null)
        {
            var s = repoInfo.DirName[repoInfo.DirInfo.FullName.Length..];
            if (!string.IsNullOrEmpty(s))
            {
                s = s.Trim(Path.DirectorySeparatorChar);
            }

            // candidate for short name
            repoInfo.Caption = repoInfo.ShortName;
            if (!string.IsNullOrEmpty(s))
            {
                repoInfo.Caption += " (" + s + ")";
            }

            repoInfo.DirInfo = repoInfo.DirInfo.Parent;
        }
        else
        {
            repoInfo.Caption = repoInfo.Repo.Path;
        }

        Validates.NotNull(repoInfo.Caption);

        var existsShortName = orderedRepos.TryGetValue(repoInfo.Caption, out var list);
        if (!existsShortName)
        {
            list = [];
            orderedRepos.Add(repoInfo.Caption, list);
        }

        List<RecentRepoInfo> tmpList = [];
        if (existsShortName)
        {
            for (var i = list!.Count - 1; i >= 0; i--)
            {
                var r = list[i];
                if (!r.FullPath)
                {
                    tmpList.Add(r);
                    list.RemoveAt(i);
                }
            }
        }

        if (repoInfo.FullPath || !existsShortName)
        {
            list!.Add(repoInfo);
        }
        else
        {
            tmpList.Add(repoInfo);
        }

        // find unique caption for repos with no title
        foreach (var r in tmpList)
        {
            AddToOrderedSignDir(orderedRepos, r, shortenPath);
        }
    }

    private static string MakePath(string? l, string r)
    {
        if (l is null)
        {
            return r;
        }

        return Path.Combine(l, r);
    }

    private void AddToOrderedMiddleDots(SortedList<string, List<RecentRepoInfo>> orderedRepos, RecentRepoInfo repoInfo)
    {
        DirectoryInfo? dirInfo;
        try
        {
            dirInfo = new DirectoryInfo(repoInfo.Repo.Path);
            if (!string.Equals(dirInfo.FullName, repoInfo.Repo.Path, StringComparison.OrdinalIgnoreCase))
            {
                // this is likely to happen when attempting to interpret windows paths on linux
                // e.g. dirInfo = DirectoryInfo("c:\\temp") -> dirInfo => /usr/home/temp
                dirInfo = null;
            }
        }
        catch
        {
            dirInfo = null;
        }

        if (dirInfo is null)
        {
            repoInfo.Caption = repoInfo.Repo.Path;
        }
        else
        {
            string? root = null;
            string? company = null;
            string? repository = null;
            var workingDir = dirInfo.Name;
            dirInfo = dirInfo.Parent;

            if (dirInfo is not null)
            {
                repository = dirInfo.Name;
                dirInfo = dirInfo.Parent;
            }

            var addDots = false;

            var isInUserProfile = PathUtil.IsInUserProfile(repoInfo.Repo.Path);

            if (dirInfo is not null)
            {
                if (dirInfo.FullName != PathUtil.UserProfilePath)
                {
                    while (dirInfo!.Parent?.Parent is not null && isInUserProfile &&
                           dirInfo.Parent?.FullName != PathUtil.UserProfilePath)
                    {
                        dirInfo = dirInfo.Parent;
                        addDots = true;
                    }

                    company = dirInfo.Name;
                }

                if (isInUserProfile)
                {
                    root = "~" + Path.DirectorySeparatorChar;
                }
                else
                {
                    if (dirInfo.Parent is not null)
                    {
                        root = dirInfo.Parent.Name;
                    }
                }
            }

            void ShortenPathWithCompany(int skipCount)
            {
                string? c = null;
                string? r = null;

                if (company?.Length > skipCount)
                {
                    c = company[..^skipCount];
                }

                if (repository?.Length > skipCount)
                {
                    r = repository[skipCount..];
                }

                repoInfo.Caption = c is null ? root : MakePath(root, c!);

                if (addDots)
                {
                    repoInfo.Caption = MakePath(repoInfo.Caption, "..");
                }

                repoInfo.Caption = MakePath(repoInfo.Caption, r!);
                repoInfo.Caption = MakePath(repoInfo.Caption, workingDir);
            }

            bool ShortenPath(int skipCount)
            {
                var path = repoInfo.Repo.Path;
                var fistDir = (root ?? company) ?? repository;
                var lastDir = workingDir;
                if (fistDir is not null && path.Length - lastDir.Length - fistDir.Length - skipCount > 0)
                {
                    var middle = ((path.Length - lastDir.Length) / 2) + ((path.Length - lastDir.Length) % 2);
                    var leftEnd = middle - (skipCount / 2);
                    var rightStart = middle + (skipCount / 2) + (skipCount % 2);

                    if (leftEnd == rightStart)
                    {
                        repoInfo.Caption = path;
                    }
                    else
                    {
                        repoInfo.Caption = path[..leftEnd] + ".." + path[rightStart..];
                    }

                    return true;
                }

                return false;
            }

            // if fixed width is not set then short as in pull request vccp's example
            // full "E:\CompanyName\Projects\git\ProductName\Sources\RepositoryName\WorkingDirName"
            // short "E:\CompanyName\...\RepositoryName\WorkingDirName"
            if (RecentReposComboMinWidth == 0)
            {
                ShortenPathWithCompany(0);
            }

            // else skip symbols beginning from the middle to both sides,
            // so we'll see "E:\Compa...toryName\WorkingDirName" and "E:\...\WorkingDirName" at the end.
            else
            {
                SizeF captionSize;
                bool canShorten;
                var skipCount = 0;
                do
                {
                    canShorten = ShortenPath(skipCount);
                    skipCount++;
                    captionSize = TextRenderer.MeasureText(repoInfo.Caption, MeasureFont);
                } while (captionSize.Width > RecentReposComboMinWidth - 10 && canShorten);
            }
        }

        Validates.NotNull(repoInfo.Caption);

        if (!orderedRepos.TryGetValue(repoInfo.Caption, out var list))
        {
            list = [];
            orderedRepos.Add(repoInfo.Caption, list);
        }

        list.Add(repoInfo);
    }
}
