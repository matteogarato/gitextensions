using Avalonia.Controls;
using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Shims.WinForms;
using GitExtUtils;
using GitExtUtils.GitUI.Theming;
using GitUIPluginInterfaces;

using ResourceManager;

namespace GitUI.CommandsDialogs;

// Reduced shell: the read-only browse panels and first repository commands are functional;
// the remaining upstream menus and toolbars arrive with their corresponding dialogs.
public sealed partial class FormBrowse : GitModuleForm
{
    private readonly CancellationTokenSequence _repositoryDataRequestSequence = new();
    private readonly CancellationTokenSequence _revisionFilesRequestSequence = new();
    private readonly CancellationTokenSequence _fileDiffRequestSequence = new();
    private readonly CancellationTokenSequence _workingTreeStatusRequestSequence = new();
    private readonly CancellationTokenSequence _workingTreeDiffRequestSequence = new();
    private long _repositoryLoadVersion;
    private bool _workingTreeOperationInProgress;
    private bool _isClosed;
    private IGitModule? _lastRepositoryModule;

    public FormBrowse()
    {
        InitializeComponent();
    }

    public FormBrowse(IServiceProvider serviceProvider, GitModule module)
        : this(new GitUICommands(serviceProvider, module))
    {
    }

    public FormBrowse(IGitUICommands commands)
        : base(commands, enablePositionRestore: true)
    {
        InitializeComponent();

        RevisionGrid.SelectionChanged += RevisionGrid_SelectionChanged;
        fileStatusList.SelectedIndexChanged += FileStatusList_SelectedIndexChanged;
        workingTreePanel.SelectionChanged += WorkingTreePanel_SelectionChanged;
        workingTreePanel.StageRequested += (_, _) => ApplyWorkingTreeChanges(stage: true);
        workingTreePanel.UnstageRequested += (_, _) => ApplyWorkingTreeChanges(stage: false);
        fileListTabs.SelectionChanged += FileListTabs_SelectionChanged;
        repoObjectsTree.SelectionChanged += RepoObjectsTree_SelectionChanged;
        openToolStripMenuItem.Click += OpenToolStripMenuItemClick;
        refreshToolStripMenuItem.Click += RefreshToolStripMenuItemClick;
        fetchAllToolStripMenuItem.Click += fetchAllToolStripMenuItem_Click;
        viewToolStripMenuItem.Header = GitUI.TranslatedStrings.View;
        lightThemeMenuItem.Header = GitUI.TranslatedStrings.LightTheme;
        darkThemeMenuItem.Header = GitUI.TranslatedStrings.DarkTheme;
        lightThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.DefaultLight);
        darkThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.DefaultDark);
        btnContinueRebase.Content = GitUI.TranslatedStrings.ContinueRebase;
        btnContinueMerge.Content = GitUI.TranslatedStrings.ButtonContinue;
        btnContinuePatch.Content = GitUI.TranslatedStrings.ButtonContinue;
        btnSkipRebase.Content = GitUI.TranslatedStrings.SkipRebase;
        btnAbortRebase.Content = GitUI.TranslatedStrings.AbortRebase;
        btnAbortMerge.Content = GitUI.TranslatedStrings.AbortMerge;
        btnAbortPatch.Content = GitUI.TranslatedStrings.AbortPatch;
        btnContinueRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.ContinueRebase(), requireResolvedConflicts: true);
        btnContinueMerge.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Merge, Commands.ContinueMerge(), requireResolvedConflicts: true);
        btnContinuePatch.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Patch, Commands.Resolved(), requireResolvedConflicts: true);
        btnSkipRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.SkipRebase());
        btnAbortRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.AbortRebase());
        btnAbortMerge.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Merge, Commands.AbortMerge());
        btnAbortPatch.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Patch, Commands.Abort());
        UICommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        Closed += FormBrowse_Closed;

        ReloadRepository();

        InitializeComplete();
    }

    private void ReloadRepository()
    {
        if (_isClosed)
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = Interlocked.Increment(ref _repositoryLoadVersion);
        CancellationToken dataRequestCancellationToken = _repositoryDataRequestSequence.Next();
        _workingTreeStatusRequestSequence.Next();
        _revisionFilesRequestSequence.Next();
        _fileDiffRequestSequence.Next();
        _workingTreeDiffRequestSequence.Next();
        SetOperationStatus(default);

        if (!ReferenceEquals(_lastRepositoryModule, module))
        {
            _lastRepositoryModule = module;
            _workingTreeOperationInProgress = false;
            workingTreePanel.SetOperationInProgress(isInProgress: false);
        }

        bool isValidWorkingDir = module.IsValidGitWorkingDir();
        IAppTitleGenerator appTitleGenerator = UICommands.GetRequiredService<IAppTitleGenerator>();
        Title = appTitleGenerator.Generate(module.WorkingDir, isValidWorkingDir, string.Empty);

        refreshToolStripMenuItem.IsEnabled = isValidWorkingDir;
        fetchAllToolStripMenuItem.IsEnabled = false;
        openToolStripMenuItem.IsEnabled = true;
        UpdateThemeMenu();
        fileStatusList.Clear();
        fileViewer.ViewPatch(string.Empty);
        CommitInfo.Revision = null;

        if (isValidWorkingDir)
        {
            lblRepoPath.Text = module.WorkingDir;
            lblStatus.Text = GitUI.TranslatedStrings.LoadingData;
            RevisionGrid.ReloadRevisions(module);
            repoObjectsTree.SetRefs([]);
            commitFilesTab.Header = GitUI.TranslatedStrings.CommitFiles;
            workingTreeTab.Header = GitUI.TranslatedStrings.WorkingTree;
            ThreadHelper.FileAndForget(async () =>
            {
                try
                {
                    string branchName = module.GetSelectedBranch();
                    IReadOnlyList<IGitRef> refs = module.GetRefs(RefsFilter.NoFilter);

                    if (dataRequestCancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(dataRequestCancellationToken);
                    if (!IsCurrentRepositoryLoad(module, loadVersion))
                    {
                        return;
                    }

                    foreach (IGitRef gitRef in refs.Where(gitRef => gitRef.IsHead))
                    {
                        gitRef.IsSelected = gitRef.Name == branchName;
                    }

                    lblRepoPath.Text = branchName.Length > 0
                        ? $"{module.WorkingDir}  —  {branchName}"
                        : module.WorkingDir;
                    Title = appTitleGenerator.Generate(module.WorkingDir, isValidWorkingDir, branchName);
                    repoObjectsTree.SetRefs(refs);
                    LoadWorkingTreeStatus(module, loadVersion);
                }
                catch (OperationCanceledException) when (dataRequestCancellationToken.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    if (IsCurrentRepositoryLoad(module, loadVersion))
                    {
                        lblStatus.Text = exception.ToStringWithData();
                    }
                }
            });
        }
        else
        {
            lblRepoPath.Text = module.WorkingDir ?? string.Empty;
            lblStatus.Text = string.IsNullOrWhiteSpace(module.WorkingDir)
                ? string.Empty
                : GitUI.TranslatedStrings.DirectoryInvalidRepository;
            workingTreePanel.Clear();
            repoObjectsTree.SetRefs([]);
        }
    }

    internal bool OpenRepository(string path)
    {
        if (_isClosed)
        {
            return false;
        }

        string? workingDir = GitModule.TryFindGitWorkingDir(path);
        if (workingDir is null)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.DirectoryInvalidRepository);
            return false;
        }

        UICommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
        UICommands = UICommands.WithWorkingDirectory(workingDir);
        UICommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        ReloadRepository();
        return true;
    }

    private bool IsCurrentRepositoryLoad(IGitModule module, long loadVersion)
    {
        return loadVersion == _repositoryLoadVersion && ReferenceEquals(Module, module);
    }

    private void LoadWorkingTreeStatus(IGitModule module, long loadVersion)
    {
        if (_isClosed)
        {
            return;
        }

        CancellationToken cancellationToken = _workingTreeStatusRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                IReadOnlyList<GitItemStatus> status = module.GetAllChangedFilesWithSubmodulesStatus(cancellationToken);
                RepositoryOperationState operationState = GetRepositoryOperationState(module);
                string statusText = FormatWorkingTreeStatus(status);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                if (!IsCurrentRepositoryLoad(module, loadVersion))
                {
                    return;
                }

                workingTreePanel.SetStatus(status);
                SetOperationStatus(operationState);
                fetchAllToolStripMenuItem.IsEnabled = !operationState.IsActive;
                lblStatus.Text = statusText;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (IsCurrentRepositoryLoad(module, loadVersion))
                {
                    SetOperationStatus(default);
                    lblStatus.Text = exception.ToStringWithData();
                }
            }
        });
    }

    private static RepositoryOperationState GetRepositoryOperationState(IGitModule module)
    {
        // A stopped rebase may also leave MERGE_HEAD, so classify it before merge.
        bool isRebaseInProgress = module.InTheMiddleOfRebase();
        bool isMergeInProgress = module.InTheMiddleOfMerge();
        bool isPatchInProgress = module.InTheMiddleOfPatch();
        bool isBisectInProgress = module.InTheMiddleOfBisect();
        bool hasUnresolvedConflicts = module.InTheMiddleOfConflictedMerge();
        RepositoryOperation operation = isRebaseInProgress ? RepositoryOperation.Rebase
            : isMergeInProgress ? RepositoryOperation.Merge
            : isPatchInProgress ? RepositoryOperation.Patch
            : isBisectInProgress ? RepositoryOperation.Bisect
            : hasUnresolvedConflicts ? RepositoryOperation.Unknown
            : RepositoryOperation.None;

        return new RepositoryOperationState(operation, hasUnresolvedConflicts);
    }

    private void SetOperationStatus(RepositoryOperationState state)
    {
        operationStatusPanel.IsVisible = state.IsActive;
        btnContinueRebase.IsVisible = state.Operation == RepositoryOperation.Rebase && !state.HasUnresolvedConflicts;
        btnContinueMerge.IsVisible = state.Operation == RepositoryOperation.Merge && !state.HasUnresolvedConflicts;
        btnContinuePatch.IsVisible = state.Operation == RepositoryOperation.Patch && !state.HasUnresolvedConflicts;
        btnSkipRebase.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnAbortRebase.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnAbortMerge.IsVisible = state.Operation == RepositoryOperation.Merge;
        btnAbortPatch.IsVisible = state.Operation == RepositoryOperation.Patch;

        string operationText = state.Operation switch
        {
            RepositoryOperation.Rebase => GitUI.TranslatedStrings.RebaseInProgress,
            RepositoryOperation.Merge => GitUI.TranslatedStrings.MergeInProgress,
            RepositoryOperation.Patch => GitUI.TranslatedStrings.PatchInProgress,
            RepositoryOperation.Bisect => GitUI.TranslatedStrings.BisectInProgress,
            RepositoryOperation.Unknown when state.HasUnresolvedConflicts => GitUI.TranslatedStrings.UnrecognizedConflictOperation,
            RepositoryOperation.Unknown => GitUI.TranslatedStrings.UnrecognizedOperationInProgress,
            _ => string.Empty,
        };

        lblOperationStatus.Text = AppendConflictStatus(operationText);

        return;

        string AppendConflictStatus(string text)
        {
            return state.HasUnresolvedConflicts
                ? $"{text}{Environment.NewLine}{GitUI.TranslatedStrings.UnresolvedConflicts}"
                : text;
        }
    }

    private void RunOperationRecoveryCommand(RepositoryOperation expectedOperation, ArgumentString arguments, bool requireResolvedConflicts = false)
    {
        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        RepositoryOperationState currentState;
        try
        {
            currentState = GetRepositoryOperationState(module);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
            return;
        }

        if (!IsCurrentRepositoryLoad(module, loadVersion)
            || currentState.Operation != expectedOperation
            || (requireResolvedConflicts && currentState.HasUnresolvedConflicts))
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.RecoveryActionStateChanged);
            UICommands.RepoChangedNotifier.Notify();
            return;
        }

        UICommands.StartGitCommandProcessDialog(this, arguments);

        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private void ApplyWorkingTreeChanges(bool stage)
    {
        if (_workingTreeOperationInProgress || workingTreePanel.SelectedItems.Count == 0)
        {
            return;
        }

        IGitModule module = Module;
        GitItemStatus[] selectedItems = [.. workingTreePanel.SelectedItems];
        long loadVersion = _repositoryLoadVersion;
        _workingTreeOperationInProgress = true;
        workingTreePanel.SetOperationInProgress(isInProgress: true);

        ThreadHelper.FileAndForget(async () =>
        {
            bool succeeded = false;
            string output;
            try
            {
                succeeded = stage
                    ? module.StageFiles(selectedItems, out output)
                    : module.UnstageFiles(selectedItems, out output);
            }
            catch (Exception exception)
            {
                output = exception.ToStringWithData();
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!IsCurrentRepositoryLoad(module, loadVersion))
            {
                return;
            }

            _workingTreeOperationInProgress = false;
            workingTreePanel.SetOperationInProgress(isInProgress: false);
            LoadWorkingTreeStatus(module, loadVersion);

            if (!succeeded)
            {
                string errorMessage = string.IsNullOrWhiteSpace(output) ? GitUI.TranslatedStrings.Error : output;
                lblStatus.Text = errorMessage;
                MessageBoxes.ShowError(this, errorMessage);
            }
        });
    }

    private static string FormatWorkingTreeStatus(IReadOnlyList<GitItemStatus> status)
    {
        GitItemStatus? error = status.FirstOrDefault(item => item.ErrorMessage is not null);
        if (error is not null)
        {
            throw new InvalidOperationException(error.ErrorMessage);
        }

        string[] lines = [.. status
            .Where(item => !item.IsStatusOnly)
            .Select(FormatStatusLine)];

        return lines.Length > 0
            ? string.Join(Environment.NewLine, lines)
            : GitUI.TranslatedStrings.NoResultsFound;

        static string FormatStatusLine(GitItemStatus item)
        {
            if (!item.IsTracked && item.IsNew)
            {
                return $"?? {item.Name}";
            }

            char statusCode = item.IsUnmerged ? GitItemStatusConverter.UnmergedStatus
                : item.IsNew ? GitItemStatusConverter.AddedStatus
                : item.IsDeleted ? GitItemStatusConverter.DeletedStatus
                : item.IsRenamed ? GitItemStatusConverter.RenamedStatus
                : item.IsCopied ? GitItemStatusConverter.CopiedStatus
                : item.IsChanged ? GitItemStatusConverter.ModifiedStatus
                : GitItemStatusConverter.UnmodifiedStatus_v1;

            string statusMarker = item.IsUnmerged ? "UU"
                : item.Staged switch
                {
                    StagedStatus.Index => $"{statusCode} ",
                    StagedStatus.WorkTree => $" {statusCode}",
                    _ => $"{statusCode}{statusCode}"
                };

            string path = item.OldName is null
                ? item.Name
                : $"{item.OldName} -> {item.Name}";

            return $"{statusMarker} {path}";
        }
    }

    private void RepoObjectsTree_SelectionChanged(object? sender, EventArgs e)
    {
        if (repoObjectsTree.SelectedRef is IGitRef gitRef)
        {
            RevisionGrid.SelectRevision(gitRef.ObjectId);
        }
    }

    private void RevisionGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        CancellationToken fileListCancellationToken = _revisionFilesRequestSequence.Next();
        _fileDiffRequestSequence.Next();
        fileStatusList.Clear();
        fileViewer.ViewPatch(string.Empty);
        CommitInfo.Revision = RevisionGrid.SelectedRevision;

        if (RevisionGrid.SelectedRevision is not GitRevision revision
            || !revision.HasParent)
        {
            return;
        }

        IGitModule module = Module;
        ObjectId parentId = revision.FirstParentId;
        ThreadHelper.FileAndForget(async () =>
        {
            IReadOnlyList<GitItemStatus> diffs = module.GetDiffFilesWithSubmodulesStatus(
                parentId,
                revision.ObjectId,
                parentToSecond: parentId,
                excludeSkipWorktreeFiles: false,
                UntrackedFilesMode.Default,
                fileListCancellationToken);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(fileListCancellationToken);

            if (!fileListCancellationToken.IsCancellationRequested
                && ReferenceEquals(Module, module)
                && ReferenceEquals(RevisionGrid.SelectedRevision, revision))
            {
                fileStatusList.SetDiffs(diffs);
            }
        });
    }

    private void FileStatusList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isClosed || fileListTabs.SelectedItem != commitFilesTab)
        {
            return;
        }

        if (RevisionGrid.SelectedRevision is not GitRevision revision
            || !revision.HasParent
            || fileStatusList.SelectedItem is not GitItemStatus item)
        {
            return;
        }

        IGitModule module = Module;
        ObjectId parentId = revision.FirstParentId;
        CancellationToken diffCancellationToken = _fileDiffRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            (Patch? patch, string? errorMessage) = await module.GetSingleDiffAsync(
                parentId,
                revision.ObjectId,
                item.Name,
                item.OldName,
                extraDiffArguments: "",
                module.FilesEncoding,
                cacheResult: true,
                isTracked: item.IsTracked,
                useGitColoring: false,
                GitCommandConfiguration.Default,
                diffCancellationToken);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(diffCancellationToken);

            if (!diffCancellationToken.IsCancellationRequested
                && ReferenceEquals(Module, module)
                && ReferenceEquals(fileStatusList.SelectedItem, item))
            {
                fileViewer.ViewPatch(patch?.Text ?? errorMessage);
            }
        });
    }

    private void WorkingTreePanel_SelectionChanged(object? sender, EventArgs e)
    {
        if (fileListTabs.SelectedItem == workingTreeTab)
        {
            LoadWorkingTreeFileDiff();
        }
    }

    private void FileListTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (fileListTabs.SelectedItem == workingTreeTab)
        {
            LoadWorkingTreeFileDiff();
        }
        else if (fileListTabs.SelectedItem == commitFilesTab)
        {
            FileStatusList_SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    private void LoadWorkingTreeFileDiff()
    {
        if (_isClosed)
        {
            return;
        }

        _fileDiffRequestSequence.Next();
        if (workingTreePanel.SelectedItem is not GitItemStatus item)
        {
            fileViewer.ViewPatch(string.Empty);
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        CancellationToken cancellationToken = _workingTreeDiffRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            ObjectId firstId = item.Staged == StagedStatus.Index ? module.GetCurrentCheckout() : ObjectId.IndexId;
            ObjectId secondId = item.Staged == StagedStatus.Index ? ObjectId.IndexId : ObjectId.WorkTreeId;
            (Patch? patch, string? errorMessage) = await module.GetSingleDiffAsync(
                firstId,
                secondId,
                item.Name,
                item.OldName,
                extraDiffArguments: "",
                module.FilesEncoding,
                cacheResult: false,
                isTracked: item.IsTracked,
                useGitColoring: false,
                GitCommandConfiguration.Default,
                cancellationToken);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!cancellationToken.IsCancellationRequested
                && IsCurrentRepositoryLoad(module, loadVersion)
                && fileListTabs.SelectedItem == workingTreeTab
                && ReferenceEquals(workingTreePanel.SelectedItem, item))
            {
                fileViewer.ViewPatch(patch?.Text ?? errorMessage);
            }
        });
    }

    private void UICommands_PostRepositoryChanged(object? sender, GitUIEventArgs e)
    {
        this.InvokeAndForget(ReloadRepository);
    }

    private void FormBrowse_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        UICommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
        RevisionGrid.SelectionChanged -= RevisionGrid_SelectionChanged;
        fileStatusList.SelectedIndexChanged -= FileStatusList_SelectedIndexChanged;
        workingTreePanel.SelectionChanged -= WorkingTreePanel_SelectionChanged;
        fileListTabs.SelectionChanged -= FileListTabs_SelectionChanged;
        repoObjectsTree.SelectionChanged -= RepoObjectsTree_SelectionChanged;
        Interlocked.Increment(ref _repositoryLoadVersion);
        _repositoryDataRequestSequence.Dispose();
        _revisionFilesRequestSequence.Dispose();
        _fileDiffRequestSequence.Dispose();
        _workingTreeStatusRequestSequence.Dispose();
        _workingTreeDiffRequestSequence.Dispose();
    }

    private void OpenToolStripMenuItemClick(object? sender, EventArgs e)
    {
        using FolderBrowserDialog dialog = new()
        {
            SelectedPath = string.IsNullOrWhiteSpace(Module.WorkingDir) ? null : Module.WorkingDir
        };
        if (dialog.ShowDialog(this) is DialogResult.OK && dialog.SelectedPath is string path)
        {
            OpenRepository(path);
        }
    }

    private void RefreshToolStripMenuItemClick(object? sender, EventArgs e)
    {
        UICommands.RepoChangedNotifier.Notify();
    }

    private void fetchAllToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        ArgumentString arguments = new GitArgumentBuilder("fetch")
        {
            "--all",
            "--progress",
        };

        if (UICommands.StartGitCommandProcessDialog(this, arguments))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private void SetTheme(ThemeId themeId)
    {
        AppSettings.ThemeId = themeId;
        ThemePreference.Apply(themeId);
        UpdateThemeMenu();
    }

    private void UpdateThemeMenu()
    {
        bool isDark = AppSettings.ThemeId == ThemeId.DefaultDark;
        lightThemeMenuItem.IsChecked = !isDark;
        darkThemeMenuItem.IsChecked = isDark;
    }

    private enum RepositoryOperation
    {
        None,
        Unknown,
        Merge,
        Rebase,
        Patch,
        Bisect,
    }

    private readonly record struct RepositoryOperationState(RepositoryOperation Operation, bool HasUnresolvedConflicts)
    {
        public bool IsActive => Operation != RepositoryOperation.None || HasUnresolvedConflicts;
    }
}
