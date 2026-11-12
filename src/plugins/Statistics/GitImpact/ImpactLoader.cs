using System.Globalization;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using Microsoft.VisualStudio.Threading;

namespace GitExtensions.Plugins.GitImpact;

public sealed class ImpactLoader : IDisposable
{
    private readonly CancellationTokenSequence _cancellationTokenSequence = new();
    private readonly CancellationTokenSource _closingPluginCancellationToken = new();
    private readonly int _firstDayOfWeek = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
    private readonly JoinableTask _mainModuleLoadingTask;
    private readonly IGitModule _module;
    private readonly Dictionary<string, List<Commit>> _modulesCommits = new(1);

    private bool _showSubmodules;

    public ImpactLoader(IGitModule module)
    {
        _module = module;
        _mainModuleLoadingTask = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await TaskScheduler.Default.SwitchTo(true);
            LoadModuleInfoData(_module, _closingPluginCancellationToken.Token);
        });
    }

    /// <summary>
    ///     property to enable mailmap respectfulness
    /// </summary>
    public bool RespectMailmap { get; set; }

    public bool ShowSubmodules
    {
        get => _showSubmodules;
        set
        {
            Stop();
            _showSubmodules = value;
        }
    }

    void IDisposable.Dispose()
    {
        _closingPluginCancellationToken.Cancel();
        Stop();
        _cancellationTokenSequence.Dispose();
        _closingPluginCancellationToken.Dispose();
    }

    public event EventHandler? Exited;
    public event Action<IList<Commit>>? CommitLoaded;

    public void Stop()
    {
        _cancellationTokenSequence.CancelCurrent();
    }

    public void Execute()
    {
        var token = _cancellationTokenSequence.Next();

        var tasks = GetTasks(token);

        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                await Task.WhenAll(tasks.Select(joinableTask => joinableTask.Task));
            }
            finally
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
                if (!token.IsCancellationRequested)
                {
                    Exited?.Invoke(this, EventArgs.Empty);
                }
            }
        });
    }

    private IReadOnlyList<JoinableTask> GetTasks(CancellationToken token)
    {
        List<JoinableTask> tasks =
        [
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await _mainModuleLoadingTask.JoinAsync(token);
                LoadModuleInfo(_module, token);
            })
        ];

        if (ShowSubmodules)
        {
            tasks.AddRange(
                from submoduleName in _module.GetSubmodulesLocalPaths()
                select _module.GetSubmodule(submoduleName)
                into submodule
                where submodule.IsValidGitWorkingDir()
                select ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await TaskScheduler.Default.SwitchTo(true);
                    LoadModuleInfo(submodule, token);
                }));
        }

        return tasks;
    }

    private void LoadModuleInfo(IGitModule module, CancellationToken token)
    {
        if (_modulesCommits.TryGetValue(module.WorkingDir, out var commitsBatch))
        {
            CommitLoaded?.Invoke(commitsBatch);
            return;
        }

        commitsBatch = LoadModuleInfoData(module, token);
        if (!token.IsCancellationRequested)
        {
            CommitLoaded?.Invoke(commitsBatch);
        }
    }

    private List<Commit> LoadModuleInfoData(IGitModule module, CancellationToken token)
    {
        var authorName = RespectMailmap ? "%aN" : "%an";
        var command =
            $"log --pretty=tformat:\"--- %ad --- {authorName}\" --numstat --date=short --find-copies --all --no-merges";
        var result = module.GitExecutable.Execute(command, cancellationToken: token);
        List<string> lines = [.. result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

        const int
            linePerCommitEstimationInGitLogOutput =
                6; // chosen by fair dice roll, guaranted to be random ;) ( https://xkcd.com/221/ )
        var estimatedCommitCount = lines.Count / linePerCommitEstimationInGitLogOutput;
        List<Commit> commitsBatch = new(estimatedCommitCount);

        using var lineEnumerator = lines.GetEnumerator();

        // Analyze commit listing
        while (!token.IsCancellationRequested && lineEnumerator.MoveNext())
        {
            // Read line
            var line = lineEnumerator.Current;

            // Reached the end ?
            if (line is null)
            {
                break;
            }

            // Look for commit delimiters
            if (!line.StartsWith("--- "))
            {
                continue;
            }

            // Strip "--- "
            line = line[4..];

            // Split date and author
            var header = line.Split([" --- "], 2, StringSplitOptions.RemoveEmptyEntries);

            if (header.Length != 2)
            {
                continue;
            }

            // Save author in variable
            var author = header[1];

            // Parse commit date
            var date = DateOnly.Parse(header[0]);

            // Calculate first day of the commit week
            var week = date.AddDays(_firstDayOfWeek - (int)date.DayOfWeek);

            // Reset commit data
            var commits = 1;
            var added = 0;
            var deleted = 0;

            // Parse commit lines
            while (lineEnumerator.MoveNext() && (line = lineEnumerator.Current) is not null &&
                   !line.StartsWith("--- ") && !token.IsCancellationRequested)
            {
                // Skip empty line
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var fileLine = line.Split(Delimiters.Tab);
                if (fileLine.Length >= 2)
                {
                    if (fileLine[0] != "-")
                    {
                        added += int.Parse(fileLine[0]);
                    }

                    if (fileLine[1] != "-")
                    {
                        deleted += int.Parse(fileLine[1]);
                    }
                }
            }

            if (!token.IsCancellationRequested)
            {
                commitsBatch.Add(new Commit(week, author, new DataPoint(commits, added, deleted)));
            }
        }

        _modulesCommits.Add(module.WorkingDir, commitsBatch);

        return commitsBatch;
    }

    public static void AddIntermediateEmptyWeeks(
        ref SortedDictionary<DateOnly, Dictionary<string, DataPoint>> impact,
        IEnumerable<string> authors)
    {
        foreach (var author in authors)
        {
            // Determine first and last commit week of each author
            var start = DateOnly.MinValue;
            var end = DateOnly.MinValue;
            var startFound = false;

            foreach ((var weekDate, var weekDataByAuthor) in impact)
            {
                if (weekDataByAuthor.ContainsKey(author))
                {
                    if (!startFound)
                    {
                        start = weekDate;
                        startFound = true;
                    }

                    end = weekDate;
                }
            }

            if (!startFound)
            {
                continue;
            }

            // Add 0 commits weeks in between
            foreach ((var weekDate, var weekDataByAuthor) in impact)
            {
                if (!weekDataByAuthor.ContainsKey(author) &&
                    weekDate > start && weekDate < end)
                {
                    weekDataByAuthor.Add(author, new DataPoint(0, 0, 0));
                }
            }
        }
    }

    public readonly struct Commit(DateOnly week, string author, DataPoint data)
    {
        public DateOnly Week { get; } = week;
        public string Author { get; } = author;
        public DataPoint Data { get; } = data;
    }

    public readonly struct DataPoint(int commits, int added, int deleted)
    {
        public int Commits { get; } = commits;
        public int AddedLines { get; } = added;
        public int DeletedLines { get; } = deleted;

        public int ChangedLines
        {
            get => AddedLines + DeletedLines;
        }

        public static DataPoint operator +(DataPoint left, DataPoint right)
        {
            return new DataPoint(
                left.Commits + right.Commits,
                left.AddedLines + right.AddedLines,
                left.DeletedLines + right.DeletedLines);
        }
    }
}
