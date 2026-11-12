using GitCommands.Settings;
using GitExtensions.Extensibility.Git;

namespace GitUI.UserControls.RevisionGrid;

public class IndexChangedEventArgs(bool isIndexChanged) : EventArgs
{
    public bool IsIndexChanged { get; } = isIndexChanged;
}

public sealed class IndexWatcher : IDisposable
{
    private readonly IGitUiCommandsSource _uICommandsSource;

    private bool _enabled;
    private string? _gitDirPath;

    private bool _indexChanged;

    public IndexWatcher(IGitUiCommandsSource uiCommandsSource)
    {
        _uICommandsSource = uiCommandsSource;
        _uICommandsSource.UiCommandsChanged += OnUICommandsChanged;
        GitIndexWatcher = new FileSystemWatcher();
        RefsWatcher = new FileSystemWatcher();

        GitIndexWatcher.Changed += fileSystemWatcher_Changed;
        RefsWatcher.Changed += fileSystemWatcher_Changed;
    }

    private IGitModule Module
    {
        get => _uICommandsSource.UiCommands.Module;
    }

    private bool IndexChanged
    {
        get
        {
            if (!_enabled)
            {
                return true;
            }

            if (_gitDirPath != Module.WorkingDirGitDir)
            {
                return true;
            }

            return _indexChanged;
        }
        set
        {
            _indexChanged = value;
            try
            {
                GitIndexWatcher.EnableRaisingEvents = !IndexChanged
                                                      && Directory.Exists(GitIndexWatcher.Path);
            }
            catch
            {
                GitIndexWatcher.EnableRaisingEvents = false;
            }

            Changed?.Invoke(this, new IndexChangedEventArgs(IndexChanged));
        }
    }

    private FileSystemWatcher GitIndexWatcher { get; }
    private FileSystemWatcher RefsWatcher { get; }

    void IDisposable.Dispose()
    {
        _enabled = false;
        GitIndexWatcher.EnableRaisingEvents = false;
        RefsWatcher.EnableRaisingEvents = false;
        GitIndexWatcher.Changed -= fileSystemWatcher_Changed;
        RefsWatcher.Changed -= fileSystemWatcher_Changed;
        GitIndexWatcher.Dispose();
        RefsWatcher.Dispose();
    }

    public event EventHandler<IndexChangedEventArgs>? Changed;

    private void OnUICommandsChanged(object? sender, GitUiCommandsChangedEventArgs e)
    {
        Clear();
    }

    private void SetFileSystemWatcher()
    {
        if (!Module.IsValidGitWorkingDir())
        {
            GitIndexWatcher.EnableRaisingEvents = false;
            RefsWatcher.EnableRaisingEvents = false;
        }
        else
        {
            try
            {
                _enabled = AppSettings.ShowGitStatusInBrowseToolbar;

                _gitDirPath = Module.WorkingDirGitDir;

                GitIndexWatcher.Path = _gitDirPath;
                GitIndexWatcher.Filter = "index";
                GitIndexWatcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite;
                GitIndexWatcher.IncludeSubdirectories = false;
                GitIndexWatcher.EnableRaisingEvents = _enabled;

                RefsWatcher.Path = Path.Join(Module.GitCommonDirectory, "refs");
                RefsWatcher.IncludeSubdirectories = true;
                RefsWatcher.EnableRaisingEvents = _enabled;
            }
            catch
            {
                _enabled = false;
            }
        }
    }

    private void fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        IndexChanged = true;
    }

    /// <summary>
    ///     Reenable indexwatcher (if disabled), reset icon.
    /// </summary>
    public void Reset()
    {
        RefreshWatcher();
        IndexChanged = false;
    }

    /// <summary>
    ///     Disable events, reset icon.
    /// </summary>
    public void Clear()
    {
        GitIndexWatcher.EnableRaisingEvents = false;
        RefsWatcher.EnableRaisingEvents = false;
        IndexChanged = false;
    }

    private void RefreshWatcher()
    {
        if (_gitDirPath != Module.WorkingDirGitDir || _enabled != AppSettings.ShowGitStatusInBrowseToolbar)
        {
            SetFileSystemWatcher();
        }
    }
}
