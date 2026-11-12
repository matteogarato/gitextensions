using System.Diagnostics;
using System.Timers;
using GitCommands.Utils;
using Timer = System.Timers.Timer;

namespace GitCommands.Settings;

[DebuggerDisplay("{_byNameMap.Count} {" + nameof(SettingsFilePath) + ",nq}")]
public abstract class FileSettingsCache : SettingsCache
{
    private const double _saveTime = 2000;
    private const string _backupFileExtension = ".backup";
    private const string _unreadableFileExtension = ".unreadable";
    private readonly bool _autoSave;
    private readonly FileSystemWatcher _fileWatcher = new();

    // The FileSystemWatcher is not reporting changes for WSL
    // https://github.com/microsoft/WSL/issues/4581
    private readonly bool _forceFileChangeChecks;
    private DateTime _lastFileModificationDate;
    private DateTime? _lastFileRead;
    private DateTime? _lastModificationDate;
    private bool _lastReadFailed;

    private Timer? _saveTimer;

    protected FileSettingsCache(string settingsFilePath, bool autoSave = true)
    {
        SettingsFilePath = settingsFilePath;
        _autoSave = autoSave;

        _saveTimer = new Timer(_saveTime) { Enabled = false, AutoReset = false };
        _saveTimer.Elapsed += OnSaveTimer;

        _fileWatcher.IncludeSubdirectories = false;
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Changed += _fileWatcher_Changed;
        _fileWatcher.Created += _fileWatcher_Changed;
        _fileWatcher.Deleted += _fileWatcher_Changed;
        _fileWatcher.Renamed += _fileWatcher_Changed;

        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (Directory.Exists(dir))
            {
                // Notifications may not fire
                _forceFileChangeChecks = PathUtil.IsWslPath(SettingsFilePath);

                _fileWatcher.Path = dir;
                _fileWatcher.Filter = Path.GetFileName(SettingsFilePath);
                _fileWatcher.EnableRaisingEvents = true;
            }
        }
        catch
        {
            _fileWatcher.EnableRaisingEvents = false;
        }

        FileChanged();
    }

    public string SettingsFilePath { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            LockedAction(() =>
            {
                if (_saveTimer is not null)
                {
                    _saveTimer.Dispose();
                    _saveTimer = null;
                    _fileWatcher.Changed -= _fileWatcher_Changed;
                    _fileWatcher.Created -= _fileWatcher_Changed;
                    _fileWatcher.Deleted -= _fileWatcher_Changed;
                    _fileWatcher.Renamed -= _fileWatcher_Changed;
                    _fileWatcher.Dispose();

                    if (_autoSave)
                    {
                        Save();
                    }
                }
            });
        }

        base.Dispose(disposing);
    }

    public static T FromCache<T>(string settingsFilePath, Lazy<T> createSettingsCache)
        where T : FileSettingsCache
    {
        return WeakRefCache.Default.Get(settingsFilePath + ":" + typeof(T).FullName, createSettingsCache);
    }

    private void _fileWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        FileChanged();
    }

    private void FileChanged()
    {
        _lastFileModificationDate = GetLastFileModificationUtc();
    }

    private DateTime GetLastFileModificationUtc()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                return File.GetLastWriteTimeUtc(SettingsFilePath);
            }
        }
        catch
        {
            // no-op
        }

        return DateTime.MaxValue;
    }

    protected abstract void WriteSettings(string fileName);
    protected abstract void ReadSettings(string fileName);

    protected override void SaveImpl()
    {
        try
        {
            if (!_lastModificationDate.HasValue ||
                (_lastFileRead.HasValue && _lastModificationDate.Value < _lastFileRead.Value))
            {
                return;
            }

            var tmpFile = Path.GetTempFileName();
            WriteSettings(tmpFile);

            if (File.Exists(SettingsFilePath))
            {
                // The file exists but could not be read, so the settings about to be written are only
                // the ones which were parsed before the error. Keep the unreadable file under its own
                // name and leave the last backup which could be read alone, so that both remain
                // available for recovery.
                var copyName =
                    SettingsFilePath + (_lastReadFailed ? _unreadableFileExtension : _backupFileExtension);
                try
                {
                    File.Copy(SettingsFilePath, copyName, true);
                }
                catch (Exception)
                {
                    // Ignore errors for the backup file
                }
            }

            try
            {
                // ensure the directory structure exists
                var parentFolder = Path.GetDirectoryName(SettingsFilePath);
                if (!Directory.Exists(parentFolder))
                {
                    Directory.CreateDirectory(parentFolder!);
                }

                File.Copy(tmpFile, SettingsFilePath, true);
                File.Delete(tmpFile);
            }
            catch (Exception ex)
            {
                _lastFileRead = null;
                throw new SaveSettingsException(ex);
            }

            FileChanged();
            _lastFileRead = DateTime.UtcNow;

            // The file matches the settings in memory now, so the next save backs it up as usual
            // instead of preserving it again as unreadable.
            _lastReadFailed = false;
        }
        catch (IOException ex)
        {
            throw new SaveSettingsException(ex);
        }
    }

    protected override void LoadImpl()
    {
        if (File.Exists(SettingsFilePath))
        {
            try
            {
                if (_forceFileChangeChecks)
                {
                    FileChanged();
                }

                ReadSettings(SettingsFilePath);
                _lastFileRead = DateTime.UtcNow;
                _lastReadFailed = false;
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Failed to load {SettingsFilePath}: {e.Message}");

                // Reading may have stopped at a damaged entry, in which case the settings in memory
                // are only the part which was parsed until then
                _lastReadFailed = true;
            }
        }
        else
        {
            _lastFileModificationDate = DateTime.UtcNow;
            _lastFileRead = _lastFileModificationDate;
            _lastReadFailed = false;
        }
    }

    protected override bool NeedRefresh()
    {
        if (_forceFileChangeChecks)
        {
            FileChanged();
        }

        return !_lastFileRead.HasValue || (_lastFileModificationDate > _lastFileRead.Value &&
                                           !(_lastModificationDate.HasValue &&
                                             _lastModificationDate >= _lastFileModificationDate));
    }

    protected override void SettingsChanged()
    {
        base.SettingsChanged();

        _lastModificationDate = DateTime.UtcNow;

        if (_autoSave)
        {
            StartSaveTimer();
        }
    }

    // Used to eliminate multiple settings file open and close to save multiple values.  Settings will be saved SAVETIME milliseconds after the last setvalue is called
    private void OnSaveTimer(object? source, ElapsedEventArgs e)
    {
        ((Timer)source!).Stop();
        Save();
    }

    private void StartSaveTimer()
    {
        // Resets timer so that the last call will let the timer event run and will cause the settings to be saved.
        if (_saveTimer is not null)
        {
            _saveTimer.Stop();
            _saveTimer.AutoReset = true;
            _saveTimer.Interval = _saveTime;
            _saveTimer.AutoReset = false;

            _saveTimer.Start();
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(FileSettingsCache fileSettingsCache)
    {
        public FileSystemWatcher FileSystemWatcher
        {
            get => fileSettingsCache._fileWatcher;
        }

        public void SaveImpl()
        {
            fileSettingsCache.SaveImpl();
        }

        public void SetLastModificationDate(DateTime date)
        {
            fileSettingsCache._lastModificationDate = date;
        }
    }
}
