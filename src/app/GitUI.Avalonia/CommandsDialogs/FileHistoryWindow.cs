using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUIPluginInterfaces;

namespace GitUI.CommandsDialogs;

/// <summary>
///  Shows the history, revision diff, and line blame for a repository file.
/// </summary>
public sealed class FileHistoryWindow : Window
{
    private const string _revisionIdPrefix = "file-history-revision:";

    private readonly IGitModule _module;
    private readonly string _fileName;
    private readonly ObjectId? _initialRevision;
    private readonly bool _showBlameWhenLoaded;
    private readonly CancellationTokenSequence _historySequence = new();
    private readonly CancellationTokenSequence _detailsSequence = new();
    private readonly List<GitRevision> _revisions = [];
    private Dictionary<ObjectId, string>? _fileNamesByRevision;
    private bool _detailsStatusActive;
    private readonly ListBox _history = new() { Name = "lstFileHistoryRevisions" };
    private readonly TextBox _details = new()
    {
        Name = "txtFileHistoryDetails",
        AcceptsReturn = true,
        IsReadOnly = true,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        FontFamily = "monospace",
    };
    private readonly TextBlock _status = new()
    {
        Name = "lblFileHistoryStatus",
        TextWrapping = Avalonia.Media.TextWrapping.Wrap
    };
    private readonly Button _showBlame = new()
    {
        Name = "btnShowFileBlame",
        Content = GitUI.TranslatedStrings.BlameSelectedRevision
    };
    private readonly Button _showDiff = new()
    {
        Name = "btnShowFileHistoryDiff",
        Content = GitUI.TranslatedStrings.DiffSelectedRevision
    };

    /// <summary>
    ///  Initializes a new instance of the <see cref="FileHistoryWindow"/> class.
    /// </summary>
    /// <param name="module">The repository containing the file.</param>
    /// <param name="fileName">The repository-relative file path.</param>
    /// <param name="initialRevision">The revision to select when it appears in the history.</param>
    /// <param name="showBlame">Whether to load blame after the history is ready.</param>
    public FileHistoryWindow(IGitModule module, string fileName, ObjectId? initialRevision = null, bool showBlame = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        _module = module;
        _fileName = fileName.ToPosixPath();
        _initialRevision = initialRevision;
        _showBlameWhenLoaded = showBlame;
        Title = string.Format(GitUI.TranslatedStrings.FileHistoryWindowTitle, _fileName);
        Width = 900;
        Height = 680;
        MinWidth = 620;
        MinHeight = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _history.ItemTemplate = new FuncDataTemplate<GitRevision>((revision, _) => new TextBlock
        {
            Text = revision is null
                ? string.Empty
                : $"{revision.ObjectId.ToShortString()}  {revision.Author}  {revision.AuthorDate:yyyy-MM-dd HH:mm}  {revision.Subject}",
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(4, 2),
        });
        _history.SelectionChanged += (_, _) =>
        {
            bool hadDetailsStatus = _detailsStatusActive;
            _detailsSequence.Next();
            _detailsStatusActive = false;
            _details.Text = string.Empty;
            _showBlame.IsEnabled = _history.SelectedItem is GitRevision;
            _showDiff.IsEnabled = _history.SelectedItem is GitRevision;
            if (_history.SelectedItem is GitRevision && hadDetailsStatus)
            {
                _status.Text = GitUI.TranslatedStrings.FileHistoryLoaded;
            }
        };
        _showBlame.IsEnabled = false;
        _showBlame.Click += (_, _) => ThreadHelper.FileAndForget(LoadSelectedBlameAsync);
        _showDiff.IsEnabled = false;
        _showDiff.Click += (_, _) => ThreadHelper.FileAndForget(LoadSelectedDiffAsync);
        Button close = new() { Content = GitUI.TranslatedStrings.Close, IsCancel = true };
        close.Click += (_, _) => Close();

        Grid content = new()
        {
            Margin = new Avalonia.Thickness(12),
            RowDefinitions = new RowDefinitions("2*,Auto,3*,Auto"),
            Children =
            {
                _history,
                _status,
                _details,
            },
        };
        Grid.SetRow(_status, 1);
        Grid.SetRow(_details, 2);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { _showBlame, _showDiff, close },
        };
        Grid.SetRow(actions, 3);
        content.Children.Add(actions);
        Content = content;

        Opened += (_, _) => ThreadHelper.FileAndForget(LoadHistoryAsync);
        Closed += (_, _) =>
        {
            _historySequence.Dispose();
            _detailsSequence.Dispose();
        };
    }

    private async Task LoadHistoryAsync()
    {
        CancellationToken cancellationToken = _historySequence.Next();
        _revisions.Clear();
        _history.ItemsSource = null;
        _details.Text = string.Empty;
        _detailsStatusActive = false;
        _status.Text = GitUI.TranslatedStrings.LoadingData;

        try
        {
            RevisionReader reader = new(_module);
            RevisionObserver observer = new(this, cancellationToken);
            await Task.Run(
                () => reader.GetLog(
                    observer,
                    revisionFilter: "--all --follow",
                    pathFilter: _fileName.Quote(),
                    hasNotes: false,
                    autostashLabel: GitUI.TranslatedStrings.AutostashRevision,
                    cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!cancellationToken.IsCancellationRequested)
            {
                _status.Text = exception.ToStringWithData();
            }
        }
    }

    private async Task LoadSelectedBlameAsync()
    {
        if (_history.SelectedItem is not GitRevision revision)
        {
            return;
        }

        CancellationToken cancellationToken = _detailsSequence.Next();
        _detailsStatusActive = true;
        _details.Text = string.Empty;
        _status.Text = GitUI.TranslatedStrings.LoadingData;

        try
        {
            GitBlame blame = await Task.Run(
                () => LoadBlameForRevision(revision, cancellationToken),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _details.Text = string.Join(
                Environment.NewLine,
                blame.Lines.Select(line => $"{line.FinalLineNumber,5} {line.Commit.Author} [{line.Commit.ObjectId.ToShortString()}] {line.Commit.Summary} | {line.Text}"));
            _status.Text = blame.Lines.Count == 0
                ? GitUI.TranslatedStrings.NoResultsFound
                : GitUI.TranslatedStrings.BlameLoaded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!cancellationToken.IsCancellationRequested)
            {
                _status.Text = exception.ToStringWithData();
            }
        }
    }

    private async Task LoadSelectedDiffAsync()
    {
        if (_history.SelectedItem is not GitRevision revision)
        {
            return;
        }

        CancellationToken cancellationToken = _detailsSequence.Next();
        _detailsStatusActive = true;
        _details.Text = string.Empty;
        _status.Text = GitUI.TranslatedStrings.LoadingData;

        try
        {
            (Patch? patch, string? errorMessage) = await Task.Run(
                () => LoadDiffForRevisionAsync(revision, cancellationToken),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (errorMessage is not null)
            {
                _status.Text = errorMessage;
                return;
            }

            _details.Text = patch?.Text ?? patch?.Header ?? string.Empty;
            _status.Text = patch is null
                ? GitUI.TranslatedStrings.NoResultsFound
                : GitUI.TranslatedStrings.DiffLoaded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!cancellationToken.IsCancellationRequested)
            {
                _status.Text = exception.ToStringWithData();
            }
        }
    }

    private async Task<(Patch? Patch, string? ErrorMessage)> LoadDiffForRevisionAsync(GitRevision revision, CancellationToken cancellationToken)
    {
        Dictionary<ObjectId, string> fileNamesByRevision = _fileNamesByRevision ??= LoadFileNamesByRevision(cancellationToken);
        if (!fileNamesByRevision.TryGetValue(revision.ObjectId, out string? fileName))
        {
            throw new FileNotFoundException(
                $"Could not find '{_fileName}' at revision '{revision.ObjectId}'.",
                _fileName);
        }

        ObjectId parentId = revision.FirstParentId;
        string? oldFileName = parentId.IsZero
            ? null
            : fileNamesByRevision.TryGetValue(parentId, out string? previousFileName)
                ? previousFileName
                : null;

        return await _module.GetSingleDiffAsync(
            parentId,
            revision.ObjectId,
            fileName,
            oldFileName,
            extraDiffArguments: string.Empty,
            _module.FilesEncoding,
            cacheResult: false,
            isTracked: true,
            useGitColoring: false,
            GitCommandConfiguration.Default,
            cancellationToken);
    }

    private GitBlame LoadBlameForRevision(GitRevision revision, CancellationToken cancellationToken)
    {
        Dictionary<ObjectId, string> fileNamesByRevision = _fileNamesByRevision ??= LoadFileNamesByRevision(cancellationToken);
        if (!fileNamesByRevision.TryGetValue(revision.ObjectId, out string? fileName))
        {
            throw new FileNotFoundException(
                $"Could not find '{_fileName}' at revision '{revision.ObjectId}'.",
                _fileName);
        }

        return _module.Blame(fileName, revision.ObjectId.ToString(), _module.FilesEncoding, lines: null, cancellationToken);
    }

    private Dictionary<ObjectId, string> LoadFileNamesByRevision(CancellationToken cancellationToken)
    {
        bool exactRenameMatch = AppSettings.FollowRenamesInFileHistoryExactOnly;
        GitArgumentBuilder arguments = new("log")
        {
            "--all",
            "-z",
            $"--format=\"{_revisionIdPrefix}%H\"",
            "--name-only",
            "--follow",
            "--diff-merges=separate",
            { exactRenameMatch, "--find-renames=100%" },
            { !exactRenameMatch, "--find-renames" },
            { exactRenameMatch, "--find-copies=100%" },
            { !exactRenameMatch, "--find-copies" },
            "--",
            _fileName.Quote(),
        };
        ExecutionResult result = _module.GitExecutable.Execute(
            arguments,
            outputEncoding: GitModule.LosslessEncoding,
            throwOnErrorExit: true,
            cancellationToken: cancellationToken);

        Dictionary<ObjectId, string> fileNamesByRevision = [];
        ObjectId currentObjectId = default;
        bool firstPathForRevision = false;
        foreach (string token in result.StandardOutput.Split('\0'))
        {
            if (token.StartsWith(_revisionIdPrefix, StringComparison.Ordinal))
            {
                currentObjectId = ObjectId.TryParse(token, offset: _revisionIdPrefix.Length, out ObjectId parsedId)
                    ? parsedId
                    : default;
                firstPathForRevision = true;
                continue;
            }

            if (!string.IsNullOrEmpty(token) && !currentObjectId.IsZero)
            {
                string path = firstPathForRevision && token.StartsWith('\n')
                    ? token[1..]
                    : token;
                path = GitModule.ReEncodeFileNameFromLossless(path) ?? string.Empty;
                if (!string.IsNullOrEmpty(path))
                {
                    fileNamesByRevision.TryAdd(currentObjectId, path);
                }

                firstPathForRevision = false;
            }
        }

        return fileNamesByRevision;
    }

    private sealed class RevisionObserver(FileHistoryWindow owner, CancellationToken cancellationToken)
        : IObserver<IReadOnlyList<GitRevision>>
    {
        public void OnNext(IReadOnlyList<GitRevision> value)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                owner._revisions.AddRange(value);
                owner._history.ItemsSource = owner._revisions.ToArray();
            });
        }

        public void OnCompleted()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                owner._status.Text = owner._revisions.Count == 0
                    ? GitUI.TranslatedStrings.NoResultsFound
                    : GitUI.TranslatedStrings.FileHistoryLoaded;

                int selectedIndex = owner._initialRevision is ObjectId initialRevision
                    ? owner._revisions.FindIndex(revision => revision.ObjectId == initialRevision)
                    : -1;
                owner._history.SelectedIndex = selectedIndex >= 0
                    ? selectedIndex
                    : owner._revisions.Count > 0 ? 0 : -1;

                if (owner._showBlameWhenLoaded && owner._history.SelectedItem is GitRevision)
                {
                    ThreadHelper.FileAndForget(owner.LoadSelectedBlameAsync);
                }
            });
        }

        public void OnError(Exception error)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    owner._status.Text = error.ToStringWithData();
                }
            });
        }
    }
}
