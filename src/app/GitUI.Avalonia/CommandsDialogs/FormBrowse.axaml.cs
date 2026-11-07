using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitCommands.UserRepositoryHistory;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Shims.WinForms;
using GitExtUtils;
using GitExtUtils.GitUI.Theming;
using GitUI.Compat;
using GitUI.Hotkey;
using GitUI.ScriptsEngine;
using GitUIPluginInterfaces;

using ResourceManager;

namespace GitUI.CommandsDialogs;

// Reduced shell: the read-only browse panels and first repository commands are functional;
// the remaining upstream menus and toolbars arrive with their corresponding dialogs.
public sealed partial class FormBrowse : GitModuleForm, IBrowseRepo
{
    private readonly CancellationTokenSequence _repositoryDataRequestSequence = new();
    private readonly CancellationTokenSequence _revisionFilesRequestSequence = new();
    private readonly CancellationTokenSequence _fileDiffRequestSequence = new();
    private readonly CancellationTokenSequence _workingTreeStatusRequestSequence = new();
    private readonly CancellationTokenSequence _workingTreeDiffRequestSequence = new();
    private readonly CancellationTokenSequence _recentRepositoriesRequestSequence = new();
    private IReadOnlyList<HotkeyCommand> _browseHotkeys = [];
    private long _repositoryLoadVersion;
    private bool _workingTreeOperationInProgress;
    private bool _isClosed;
    private IGitModule? _lastRepositoryModule;
    private GitItemStatus? _workingTreeDiffItem;

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
        UICommands.BrowseRepo = this;

        RevisionGrid.SelectionChanged += RevisionGrid_SelectionChanged;
        fileStatusList.SelectedIndexChanged += FileStatusList_SelectedIndexChanged;
        fileStatusList.FileHistoryRequested += (_, _) => OpenSelectedFileHistory(showBlame: false);
        fileStatusList.PathActionRequested += (_, action) => ThreadHelper.FileAndForget(() => HandleFilePathActionAsync(action));
        fileStatusList.BlameRequested += (_, _) => OpenSelectedFileHistory(showBlame: true);
        workingTreePanel.SelectionChanged += WorkingTreePanel_SelectionChanged;
        workingTreePanel.StageRequested += (_, _) => ApplyWorkingTreeChanges(stage: true);
        workingTreePanel.StageSelectedLinesRequested += (_, _) => StageSelectedLines(unstage: false);
        workingTreePanel.UnstageSelectedLinesRequested += (_, _) => StageSelectedLines(unstage: true);
        workingTreePanel.DiscardSelectedLinesRequested += (_, _) => DiscardSelectedLines();
        workingTreePanel.UnstageRequested += (_, _) => ApplyWorkingTreeChanges(stage: false);
        workingTreePanel.DiscardRequested += (_, _) => DiscardSelectedChanges();
        fileViewer.SelectionChanged += FileViewer_SelectionChanged;
        _browseHotkeys = UICommands.GetRequiredService<IHotkeySettingsLoader>()
            .LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName);
        AddHandler(InputElement.KeyDownEvent, BrowseHotkeyKeyDown, RoutingStrategies.Tunnel);
        dashboardMenuItem.Header = ResourceManager.TranslatedStrings.RecentRepositoriesTitle;
        dashboardMenuItem.Click += (_, _) => ToggleDashboard();
        initializeRepositoryMenuItem.Header = GitUI.TranslatedStrings.InitializeRepository;
        initializeRepositoryMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(InitializeRepositoryAsync);
        cloneRepositoryMenuItem.Header = GitUI.TranslatedStrings.ForkCloneRepo;
        cloneRepositoryMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(CloneRepositoryAsync);
        gitMaintenanceToolStripMenuItem.Header = GitUI.TranslatedStrings.GitMaintenanceMenu;
        compressGitDatabaseToolStripMenuItem.Header = GitUI.TranslatedStrings.CompressGitDatabaseMenu;
        compressGitDatabaseToolStripMenuItem.Click += (_, _) => CompressGitDatabase();
        verifyRepositoryMenuItem.Header = GitUI.TranslatedStrings.VerifyRepositoryMenu;
        verifyRepositoryMenuItem.Click += (_, _) => UICommands.StartVerifyDatabaseDialog(this);
        deleteIndexLockToolStripMenuItem.Header = GitUI.TranslatedStrings.DeleteIndexLockMenu;
        deleteIndexLockToolStripMenuItem.Click += (_, _) => DeleteIndexLock();
        cleanupRepositoryMenuItem.Header = GitUI.TranslatedStrings.CleanupRepositoryMenu;
        cleanupRepositoryMenuItem.Click += (_, _) => UICommands.StartCleanupRepositoryDialog(this);
        reflogMenuItem.Header = GitUI.TranslatedStrings.ShowReflogMenu;
        reflogMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ShowReflogAsync);
        lblDashboardTitle.Text = ResourceManager.TranslatedStrings.RecentRepositoriesTitle;
        btnDashboardOpen.Content = ResourceManager.TranslatedStrings.OpenSelectedRepository;
        btnDashboardOpenFolder.Content = GitUI.TranslatedStrings.Open;
        btnDashboardRemove.Content = ResourceManager.TranslatedStrings.RemoveFromRecentList;
        btnDashboardClear.Content = ResourceManager.TranslatedStrings.ClearRecentRepositories;
        btnDashboardReturn.Content = ResourceManager.TranslatedStrings.ReturnToRepository;
        lstRecentRepositories.ItemTemplate = new FuncDataTemplate<Repository>(
            (repository, _) => new TextBlock
            {
                Text = repository.Path,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
            supportsRecycling: false);
        lstRecentRepositories.SelectionChanged += (_, _) => UpdateRecentRepositoryActionState();
        lstRecentRepositories.DoubleTapped += (_, _) => OpenSelectedRecentRepository();
        btnDashboardOpen.Click += (_, _) => OpenSelectedRecentRepository();
        btnDashboardOpenFolder.Click += OpenToolStripMenuItemClick;
        btnDashboardRemove.Click += (_, _) => RemoveSelectedRecentRepository();
        btnDashboardClear.Click += (_, _) => ClearRecentRepositories();
        btnDashboardReturn.Click += (_, _) => SetDashboardVisible(
            isVisible: false,
            isValidWorkingDir: Module.IsValidGitWorkingDir());
        resetAllMenuItem.Header = GitUI.TranslatedStrings.ResetAllChangesMenu;
        resetAllMenuItem.Click += (_, _) => ResetAllChanges();
        goToCommitMenuItem.Header = GitUI.TranslatedStrings.GoToCommitMenu;
        goToCommitMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(GoToCommitAsync);
        goToSuperprojectMenuItem.Header = GitUI.TranslatedStrings.GoToSuperprojectMenu;
        goToSuperprojectMenuItem.Click += (_, _) => GoToSuperproject();
        commitMenuItem.Header = GitUI.TranslatedStrings.CommitMenu;
        commitMenuItem.Click += (_, _) => ShowCommitPanel();
        undoLastCommitMenuItem.Header = GitUI.TranslatedStrings.UndoLastCommitMenu;
        undoLastCommitMenuItem.Click += (_, _) => UndoLastCommit();
        workingTreePanel.CommitRequested += (_, request) => CommitStagedChanges(request);
        workingTreePanel.AmendChanged += (_, isAmend) => LoadAmendMessage(isAmend);
        workingTreePanel.StashSelectedFilesRequested += (_, _) =>
        {
            string[] selectedFiles = [.. workingTreePanel.SelectedItems.Select(item => item.Name)];
            ThreadHelper.FileAndForget(() => StashChangesAsync(selectedFiles));
        };
        workingTreePanel.IgnoreSelectedFilesRequested += (_, _) => IgnoreSelectedFiles();
        fileListTabs.SelectionChanged += FileListTabs_SelectionChanged;
        revisionFileTree.SelectedFileChanged += (_, _) => ShowRevisionTreeFile();
        revisionFileTree.FileActivated += (_, _) => OpenRevisionTreeFileHistory();
        repoObjectsTree.SelectionChanged += RepoObjectsTree_SelectionChanged;
        InitializeBranchContextMenu();
        stashChangesMenuItem.Header = GitUI.TranslatedStrings.StashChangesMenu;
        stashChangesMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(() => StashChangesAsync());
        stashStagedMenuItem.Header = GitUI.TranslatedStrings.StashStagedMenu;
        stashStagedMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(StashStagedAsync);
        stashManagerMenuItem.Header = GitUI.TranslatedStrings.StashManagerMenu;
        stashManagerMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ShowStashManagerAsync);
        stashOptionsMenuItem.Header = GitUI.TranslatedStrings.StashOptionsMenu;
        stashOptionsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ConfigureStashOptionsAsync);
        pullMergeMenuItem.Header = GitUI.TranslatedStrings.PullMergeMenu;
        pullRebaseMenuItem.Header = GitUI.TranslatedStrings.PullRebaseMenu;
        pushMenuItem.Header = GitUI.TranslatedStrings.PushMenu;
        forcePushMenuItem.Header = GitUI.TranslatedStrings.ForcePushMenu;
        pullMergeMenuItem.Click += (_, _) => PullCurrentBranch(rebase: false);
        pullRebaseMenuItem.Click += (_, _) => PullCurrentBranch(rebase: true);
        pushMenuItem.Click += (_, _) => PushCurrentBranch(ForcePushOptions.DoNotForce);
        forcePushMenuItem.Click += (_, _) => PushCurrentBranch(ForcePushOptions.ForceWithLease);
        pullOptionsMenuItem.Header = GitUI.TranslatedStrings.PullOptionsTitle + "...";
        pushOptionsMenuItem.Header = GitUI.TranslatedStrings.PushOptionsTitle + "...";
        pullOptionsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(PullWithOptionsAsync);
        pushOptionsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(PushWithOptionsAsync);
        formatPatchMenuItem.Header = GitUI.TranslatedStrings.FormatPatchMenu;
        formatPatchMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(FormatPatchAsync);
        applyPatchMenuItem.Header = GitUI.TranslatedStrings.ApplyPatchMenu;
        applyPatchMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ApplyPatchAsync);
        viewPatchMenuItem.Header = GitUI.TranslatedStrings.ViewPatchMenu;
        viewPatchMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ViewPatchAsync);
        openToolStripMenuItem.Click += OpenToolStripMenuItemClick;
        refreshToolStripMenuItem.Click += RefreshToolStripMenuItemClick;
        remoteManagementMenuItem.Header = GitUI.TranslatedStrings.RemoteManagementMenu;
        remoteManagementMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ManageRemotesAsync);
        submoduleManagementMenuItem.Header = GitUI.TranslatedStrings.Submodules;
        submoduleManagementMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ManageSubmodulesAsync);
        pluginsMenuItem.Header = GitUI.TranslatedStrings.PluginsMenu;
        pluginsMenuItem.PointerEntered += (_, _) => RefreshRepositoryPluginMenu();
        fetchAllToolStripMenuItem.Click += fetchAllToolStripMenuItem_Click;
        viewToolStripMenuItem.Header = AvaloniaTranslationUtils.ToAvaloniaMnemonics(GitUI.TranslatedStrings.View);
        lightThemeMenuItem.Header = GitUI.TranslatedStrings.LightTheme;
        darkThemeMenuItem.Header = GitUI.TranslatedStrings.DarkTheme;
        systemThemeMenuItem.Header = GitUI.TranslatedStrings.SystemTheme;
        commandLogMenuItem.Header = GitUI.TranslatedStrings.CommandLogTitle;
        commandLogMenuItem.Click += (_, _) => CommandLogWindow.ShowOrActivate(this);
        keyboardShortcutsMenuItem.Header = ResourceManager.TranslatedStrings.KeyboardShortcutsMenu;
        keyboardShortcutsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ConfigureHotkeysAsync);
        diffToolWorkingTreeMenuItem.Header = GitUI.TranslatedStrings.DiffToolWorkingTreeMenu;
        diffToolWorkingTreeMenuItem.Click += (_, _) => OpenDiffTool(GitRevision.IndexGuid, GitRevision.WorkTreeGuid);
        settingsMenuItem.Header = GitUI.TranslatedStrings.SettingsMenu;
        settingsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ShowSettingsAsync);
        userScriptsMenuItem.Header = ResourceManager.TranslatedStrings.ScriptsMenu;
        ReloadUserScriptsMenu();
        lightThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.DefaultLight);
        darkThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.DefaultDark);
        systemThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.WindowsAppColorModeId);
        btnContinueRebase.Content = GitUI.TranslatedStrings.ContinueRebase;
        btnEditRebaseTodo.Content = GitUI.TranslatedStrings.EditRebaseTodo;
        btnContinueMerge.Content = GitUI.TranslatedStrings.ButtonContinue;
        btnContinuePatch.Content = GitUI.TranslatedStrings.ButtonContinue;
        btnResolveConflicts.Content = GitUI.TranslatedStrings.ResolveMergeConflicts;
        btnSkipRebase.Content = GitUI.TranslatedStrings.SkipRebase;
        btnAbortRebase.Content = GitUI.TranslatedStrings.AbortRebase;
        btnAbortMerge.Content = GitUI.TranslatedStrings.AbortMerge;
        btnAbortPatch.Content = GitUI.TranslatedStrings.AbortPatch;
        btnContinueCommitPick.Content = GitUI.TranslatedStrings.ButtonContinue;
        btnAbortCommitPick.Content = GitUI.TranslatedStrings.AbortOperation;
        btnContinueRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.ContinueRebase(), requireResolvedConflicts: true);
        btnEditRebaseTodo.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, RebaseWorkflow.BuildEditTodo());
        btnContinueMerge.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Merge, Commands.ContinueMerge(), requireResolvedConflicts: true);
        btnContinuePatch.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Patch, Commands.Resolved(), requireResolvedConflicts: true);
        btnResolveConflicts.Click += (_, _) => ResolveConflicts();
        btnSkipRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.SkipRebase());
        btnAbortRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.AbortRebase());
        btnAbortMerge.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Merge, Commands.AbortMerge());
        btnAbortPatch.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Patch, Commands.Abort());
        btnContinueCommitPick.Click += (_, _) => ContinueCommitPick();
        btnAbortCommitPick.Click += (_, _) => AbortCommitPick();
        UICommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        Closed += FormBrowse_Closed;

        ReloadRepository();

        InitializeComplete();
    }

    private void ReloadUserScriptsMenu()
    {
        userScriptsMenuItem.Items.Clear();

        List<ScriptInfo> scripts = [.. UICommands.GetRequiredService<IScriptsManager>()
            .GetScripts()
            .Where(script => script.Enabled && script.OnEvent == ScriptEvent.ShowInUserMenuBar)];

        foreach (ScriptInfo script in scripts)
        {
            MenuItem menuItem = new()
            {
                Header = string.IsNullOrWhiteSpace(script.Name)
                    ? GitUI.TranslatedStrings.ScriptText
                    : script.GetDisplayName(),
                IsEnabled = !string.IsNullOrWhiteSpace(script.Command),
            };
            menuItem.Click += (_, _) => ExecuteUserScript(script);
            userScriptsMenuItem.Items.Add(menuItem);
        }

        userScriptsMenuItem.IsEnabled = scripts.Count > 0;
    }

    private void ExecuteUserScript(ScriptInfo script)
    {
        if (IsPluginCommand(script.Command))
        {
            PluginRegistry.InitializeAll(this);
            PluginRegistry.Register(UICommands, this);
        }

        UICommands.GetRequiredService<IScriptsRunner>()
            .RunScript(script, this, UICommands, ScriptOptionsProviderBase.Default);
    }

    private static bool IsPluginCommand(string? command)
        => command is not null
            && (command.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase)
                || command.StartsWith("{plugin", StringComparison.OrdinalIgnoreCase));

    private void RefreshRepositoryPluginMenu()
    {
        pluginsMenuItem.Items.Clear();
        if (!Module.IsValidGitWorkingDir())
        {
            return;
        }

        try
        {
            PluginRegistry.InitializeRepositoryCommandPlugins(this);
            foreach (IRepositoryCommandPlugin plugin in PluginRegistry.RepositoryCommandPlugins)
            {
                MenuItem pluginMenuItem = new() { Header = plugin.Name };
                foreach (IRepositoryPluginCommand command in plugin.Commands)
                {
                    bool canExecute;
                    try
                    {
                        canExecute = command.CanExecute(Module);
                    }
                    catch (Exception exception)
                    {
                        MessageBoxes.ShowError(this, exception.ToStringWithData());
                        continue;
                    }

                    MenuItem commandMenuItem = new()
                    {
                        Header = command.Name,
                        IsEnabled = canExecute,
                    };
                    commandMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(
                        () => ExecuteRepositoryPluginCommandAsync(command));
                    pluginMenuItem.Items.Add(commandMenuItem);
                }

                if (pluginMenuItem.Items.Count > 0)
                {
                    pluginsMenuItem.Items.Add(pluginMenuItem);
                }
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private void CompressGitDatabase()
    {
        UICommands.StartCommandLineProcessDialog(this, Commands.GarbageCollect());
    }

    private void DeleteIndexLock()
    {
        try
        {
            Module.UnlockIndex(includeSubmodules: true);
        }
        catch (FileDeleteException exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private async Task ExecuteRepositoryPluginCommandAsync(IRepositoryPluginCommand command)
    {
        try
        {
            await RepositoryPluginCommandRunner.ExecuteAsync(UICommands, command);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
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
        _recentRepositoriesRequestSequence.Next();
        SetOperationStatus(default);

        bool repositoryChanged = !ReferenceEquals(_lastRepositoryModule, module);
        if (repositoryChanged)
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
        gitMaintenanceToolStripMenuItem.IsEnabled = isValidWorkingDir;
        verifyRepositoryMenuItem.IsEnabled = isValidWorkingDir;
        cleanupRepositoryMenuItem.IsEnabled = isValidWorkingDir;
        reflogMenuItem.IsEnabled = isValidWorkingDir;
        applyPatchMenuItem.IsEnabled = isValidWorkingDir;
        viewPatchMenuItem.IsEnabled = isValidWorkingDir;
        remoteManagementMenuItem.IsEnabled = isValidWorkingDir;
        submoduleManagementMenuItem.IsEnabled = isValidWorkingDir;
        openToolStripMenuItem.IsEnabled = true;
        UpdateThemeMenu();
        fileStatusList.Clear();
        fileViewer.ViewPatch(string.Empty);
        _workingTreeDiffItem = null;
        workingTreePanel.SetCanStageSelectedLines(false);
        CommitInfo.Revision = null;

        if (isValidWorkingDir)
        {
            SetDashboardVisible(isVisible: false, isValidWorkingDir: isValidWorkingDir);
            lblRepoPath.Text = module.WorkingDir;
            lblStatus.Text = GitUI.TranslatedStrings.LoadingData;
            LoadCommitOptions(module, loadVersion, loadTemplates: repositoryChanged);
            RevisionGrid.ReloadRevisions(module);
            repoObjectsTree.SetRefs([]);
            commitFilesTab.Header = GitUI.TranslatedStrings.CommitFiles;
            revisionTreeTab.Header = GitUI.TranslatedStrings.FileTree;
            workingTreeTab.Header = GitUI.TranslatedStrings.WorkingTree;
            ThreadHelper.FileAndForget(async () =>
            {
                try
                {
                    string branchName = module.GetSelectedBranch();
                    IReadOnlyList<IGitRef> refs = module.GetRefs(RefsFilter.NoFilter);
                    IReadOnlyList<GitStash> stashes = module.GetStashes(noLocks: true);

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
                    repoObjectsTree.SetRefs(refs, stashes);
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
            SetDashboardVisible(isVisible: true, isValidWorkingDir: isValidWorkingDir);
            lblRepoPath.Text = module.WorkingDir ?? string.Empty;
            lblStatus.Text = string.IsNullOrWhiteSpace(module.WorkingDir)
                ? string.Empty
                : GitUI.TranslatedStrings.DirectoryInvalidRepository;
            workingTreePanel.Clear();
            repoObjectsTree.SetRefs([]);
            LoadRecentRepositories();
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
        if (PluginRegistry.IsInitialized)
        {
            PluginRegistry.Unregister(UICommands, this);
        }

        UICommands = UICommands.WithWorkingDirectory(workingDir);
        UICommands.BrowseRepo = this;
        UICommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        if (PluginRegistry.IsInitialized)
        {
            PluginRegistry.Register(UICommands, this);
        }

        ReloadRepository();
        RecordRecentRepository(workingDir);
        return true;
    }

    public GitRevision? GetLatestSelectedRevision() => RevisionGrid.SelectedRevision;

    public IReadOnlyList<GitRevision> GetSelectedRevisions()
        => RevisionGrid.SelectedRevisions;

    public Point GetQuickItemSelectorLocation() => new(0, 0);

    public void GoToRef(string refName, bool showNoRevisionMsg, bool toggleSelection = false)
    {
        if (RevisionGrid.GoToRef(refName))
        {
            return;
        }

        if (showNoRevisionMsg)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoResultsFound);
        }
    }

    public void SetWorkingDir(string? path, ObjectId selectedId = default, ObjectId firstId = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            UICommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
            if (PluginRegistry.IsInitialized)
            {
                PluginRegistry.Unregister(UICommands, this);
            }

            UICommands = UICommands.WithWorkingDirectory(null);
            UICommands.BrowseRepo = this;
            UICommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
            if (PluginRegistry.IsInitialized)
            {
                PluginRegistry.Register(UICommands, this);
            }

            ReloadRepository();
            return;
        }

        ObjectId revisionToSelect = selectedId.IsZero ? firstId : selectedId;
        if (OpenRepository(path) && !revisionToSelect.IsZero)
        {
            RevisionGrid.SelectRevision(revisionToSelect);
        }
    }

    private void ToggleDashboard()
    {
        bool isValidWorkingDir = Module.IsValidGitWorkingDir();
        SetDashboardVisible(!dashboardPanel.IsVisible || !isValidWorkingDir, isValidWorkingDir);
    }

    private void BrowseHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (!AvaloniaHotkeyKeyConverter.TryCreateKeyData(e.Key, e.KeyModifiers, out Keys keyData))
        {
            return;
        }

        HotkeyCommand? hotkey = _browseHotkeys.FirstOrDefault(command => command.KeyData == keyData);
        if (hotkey is null || keyData == Keys.None || !ExecuteBrowseHotkey(hotkey.CommandCode))
        {
            return;
        }

        e.Handled = true;
    }

    private bool ExecuteBrowseHotkey(int commandCode)
    {
        switch ((AvaloniaBrowseHotkeyCommand)commandCode)
        {
            case AvaloniaBrowseHotkeyCommand.OpenRepository:
                openToolStripMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                return true;
            case AvaloniaBrowseHotkeyCommand.ToggleDashboard:
                ToggleDashboard();
                return true;
            case AvaloniaBrowseHotkeyCommand.Refresh:
                refreshToolStripMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                return true;
            case AvaloniaBrowseHotkeyCommand.Pull:
                PullCurrentBranch(rebase: false);
                return true;
            case AvaloniaBrowseHotkeyCommand.Push:
                PushCurrentBranch(ForcePushOptions.DoNotForce);
                return true;
            default:
                return false;
        }
    }

    private async Task ShowSettingsAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        SettingsWindow dialog = new(Module);
        if (await dialog.ShowDialog<bool>(this))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task ConfigureHotkeysAsync()
    {
        AvaloniaHotkeySettingsManager settingsManager = UICommands.GetRequiredService<AvaloniaHotkeySettingsManager>();
        HotkeySettingsWindow dialog = new(settingsManager);
        bool saved = await dialog.ShowDialog<bool>(this);
        if (saved)
        {
            _browseHotkeys = settingsManager.LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName);
        }
    }

    private void SetDashboardVisible(bool isVisible, bool isValidWorkingDir)
    {
        bool showDashboard = isVisible || !isValidWorkingDir;
        dashboardPanel.IsVisible = showDashboard;
        repositoryWorkspace.IsVisible = !showDashboard;
        repositoryInfoPanel.IsVisible = !showDashboard;
        btnDashboardReturn.IsVisible = showDashboard && isValidWorkingDir;
        goToCommitMenuItem.IsEnabled = isValidWorkingDir;
        goToSuperprojectMenuItem.IsEnabled = isValidWorkingDir && Module.SuperprojectModule is not null;
        commitMenuItem.IsEnabled = isValidWorkingDir && !Module.IsBareRepository();
        undoLastCommitMenuItem.IsEnabled = isValidWorkingDir && !Module.IsBareRepository();

        if (showDashboard)
        {
            LoadRecentRepositories();
        }
    }

    private void UndoLastCommit()
    {
        if (!AppSettings.DontConfirmUndoLastCommit.Value
            && MessageBoxes.Show(
                this,
                GitUI.TranslatedStrings.UndoLastCommitConfirmation,
                GitUI.TranslatedStrings.UndoLastCommitCaption,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        if (UICommands.StartGitCommandProcessDialog(this, ResetWorkflow.BuildUndoLastCommit())
            && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private void LoadRecentRepositories()
    {
        CancellationToken cancellationToken = _recentRepositoriesRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            IList<Repository> repositories;
            try
            {
                repositories = await RepositoryHistoryManager.Locals.LoadRecentHistoryAsync();
            }
            catch (Exception exception)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!cancellationToken.IsCancellationRequested && !_isClosed)
                {
                    lblDashboardStatus.Text = exception.ToStringWithData();
                }

                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!cancellationToken.IsCancellationRequested && !_isClosed && dashboardPanel.IsVisible)
            {
                lstRecentRepositories.ItemsSource = repositories.ToArray();
                lblDashboardStatus.Text = repositories.Count == 0 ? GitUI.TranslatedStrings.NoResultsFound : string.Empty;
                UpdateRecentRepositoryActionState();
            }
        });
    }

    private void UpdateRecentRepositoryActionState()
    {
        bool hasSelection = lstRecentRepositories.SelectedItem is Repository;
        btnDashboardOpen.IsEnabled = hasSelection;
        btnDashboardRemove.IsEnabled = hasSelection;
        btnDashboardClear.IsEnabled = lstRecentRepositories.Items.Count > 0;
    }

    private void OpenSelectedRecentRepository()
    {
        if (lstRecentRepositories.SelectedItem is Repository repository)
        {
            OpenRepository(repository.Path);
        }
    }

    private void RemoveSelectedRecentRepository()
    {
        if (lstRecentRepositories.SelectedItem is not Repository repository)
        {
            return;
        }

        UpdateRecentRepositories(async () =>
        {
            await RepositoryHistoryManager.Locals.RemoveRecentAsync(repository.Path);
        });
    }

    private void ClearRecentRepositories()
    {
        UpdateRecentRepositories(async () =>
        {
            await RepositoryHistoryManager.Locals.SaveRecentHistoryAsync([]);
        });
    }

    private void RecordRecentRepository(string path)
    {
        AppSettings.RecentWorkingDir = path;
        try
        {
            ThreadHelper.JoinableTaskFactory.Run(
                () => RepositoryHistoryManager.Locals.AddAsMostRecentAsync(path));
            if (dashboardPanel.IsVisible)
            {
                LoadRecentRepositories();
            }
        }
        catch (Exception exception)
        {
            lblDashboardStatus.Text = exception.ToStringWithData();
        }
    }

    private void UpdateRecentRepositories(Func<Task> update)
    {
        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                await update();
            }
            catch (Exception exception)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!_isClosed)
                {
                    lblDashboardStatus.Text = exception.ToStringWithData();
                }

                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!_isClosed && dashboardPanel.IsVisible)
            {
                LoadRecentRepositories();
            }
        });
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
        bool isCherryPickInProgress = File.Exists(module.ResolveGitInternalPath("CHERRY_PICK_HEAD"));
        bool isRevertInProgress = File.Exists(module.ResolveGitInternalPath("REVERT_HEAD"));
        bool hasUnresolvedConflicts = module.InTheMiddleOfConflictedMerge();
        RepositoryOperation operation = isRebaseInProgress ? RepositoryOperation.Rebase
            : isMergeInProgress ? RepositoryOperation.Merge
            : isPatchInProgress ? RepositoryOperation.Patch
            : isCherryPickInProgress ? RepositoryOperation.CherryPick
            : isRevertInProgress ? RepositoryOperation.Revert
            : isBisectInProgress ? RepositoryOperation.Bisect
            : hasUnresolvedConflicts ? RepositoryOperation.Unknown
            : RepositoryOperation.None;

        return new RepositoryOperationState(operation, hasUnresolvedConflicts);
    }

    private void SetOperationStatus(RepositoryOperationState state)
    {
        operationStatusPanel.IsVisible = state.IsActive;
        btnResolveConflicts.IsVisible = state.HasUnresolvedConflicts;
        btnContinueRebase.IsVisible = state.Operation == RepositoryOperation.Rebase && !state.HasUnresolvedConflicts;
        btnEditRebaseTodo.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnContinueMerge.IsVisible = state.Operation == RepositoryOperation.Merge && !state.HasUnresolvedConflicts;
        btnContinuePatch.IsVisible = state.Operation == RepositoryOperation.Patch && !state.HasUnresolvedConflicts;
        btnContinueCommitPick.IsVisible = (state.Operation is RepositoryOperation.CherryPick or RepositoryOperation.Revert)
            && !state.HasUnresolvedConflicts;
        btnSkipRebase.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnAbortRebase.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnAbortMerge.IsVisible = state.Operation == RepositoryOperation.Merge;
        btnAbortPatch.IsVisible = state.Operation == RepositoryOperation.Patch;
        btnAbortCommitPick.IsVisible = state.Operation is RepositoryOperation.CherryPick or RepositoryOperation.Revert;

        string operationText = state.Operation switch
        {
            RepositoryOperation.Rebase => GitUI.TranslatedStrings.RebaseInProgress,
            RepositoryOperation.Merge => GitUI.TranslatedStrings.MergeInProgress,
            RepositoryOperation.Patch => GitUI.TranslatedStrings.PatchInProgress,
            RepositoryOperation.CherryPick => GitUI.TranslatedStrings.CherryPickInProgress,
            RepositoryOperation.Revert => GitUI.TranslatedStrings.RevertInProgress,
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

    private void ResolveConflicts()
    {
        if (UICommands.StartResolveConflictsDialog(this))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private void ContinueCommitPick()
    {
        RunCommitPickRecoveryCommand(continueOperation: true);
    }

    private void AbortCommitPick()
    {
        RunCommitPickRecoveryCommand(continueOperation: false);
    }

    private void RunCommitPickRecoveryCommand(bool continueOperation)
    {
        IGitModule module = Module;
        RepositoryOperationState state;
        try
        {
            state = GetRepositoryOperationState(module);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
            return;
        }

        ArgumentString? command = (state.Operation, continueOperation) switch
        {
            (RepositoryOperation.CherryPick, true) when !state.HasUnresolvedConflicts => Commands.ContinueCherryPick(),
            (RepositoryOperation.Revert, true) when !state.HasUnresolvedConflicts => Commands.ContinueRevert(),
            (RepositoryOperation.CherryPick, false) => Commands.AbortCherryPick(),
            (RepositoryOperation.Revert, false) => Commands.AbortRevert(),
            _ => (ArgumentString?)null,
        };
        if (command is not ArgumentString arguments)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.RecoveryActionStateChanged);
            UICommands.RepoChangedNotifier.Notify();
            return;
        }

        RunOperationRecoveryCommand(state.Operation, arguments, requireResolvedConflicts: continueOperation);
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

    private async Task PullWithOptionsAsync()
    {
        IGitModule module = Module;
        IReadOnlyList<string> remotes = module.GetRemoteNames();
        if (remotes.Count == 0)
        {
            MessageBoxes.ShowError(this, RemoteWorkflow.Validate(module, requiresBranch: false) ?? GitUI.TranslatedStrings.RemoteLabel);
            return;
        }

        PullOptionsWindow dialog = new(remotes, RemoteWorkflow.ResolveRemote(module), autoStash: false);
        PullOptions? options = await dialog.ShowDialog<PullOptions?>(this);
        if (options is null)
        {
            return;
        }

        bool fetchOnly = options.Mode == PullMode.FetchOnly;
        RunRemoteCommand(
            RemoteWorkflow.BuildPull(module, options),
            fetchOnly ? ScriptEvent.BeforeFetch : ScriptEvent.BeforePull,
            fetchOnly ? ScriptEvent.AfterFetch : ScriptEvent.AfterPull);
        if (options.Mode != PullMode.FetchOnly)
        {
            HandOffConflicts(module, options.Mode == PullMode.Rebase);
        }
    }

    private async Task PushWithOptionsAsync()
    {
        IGitModule module = Module;
        IReadOnlyList<string> remotes = module.GetRemoteNames();
        if (remotes.Count == 0)
        {
            MessageBoxes.ShowError(this, RemoteWorkflow.Validate(module, requiresBranch: false) ?? GitUI.TranslatedStrings.RemoteLabel);
            return;
        }

        PushOptionsWindow dialog = new(remotes, RemoteWorkflow.ResolveRemote(module), module.GetSelectedBranch(emptyIfDetached: true), trackByDefault: false);
        PushOptions? options = await dialog.ShowDialog<PushOptions?>(this);
        if (options is null)
        {
            return;
        }

        if (options.Scope == PushScope.CurrentBranch && string.IsNullOrEmpty(module.GetSelectedBranch(emptyIfDetached: true)))
        {
            MessageBoxes.ShowError(this, RemoteWorkflow.Validate(module, requiresBranch: true) ?? GitUI.TranslatedStrings.PushOptionsTitle);
            return;
        }

        if (options.Force != ForcePushOptions.DoNotForce
            && !ConfirmForcePush(module, options.Remote))
        {
            return;
        }

        RunRemoteCommand(RemoteWorkflow.BuildPush(module, options), ScriptEvent.BeforePush, ScriptEvent.AfterPush);
    }

    private void HandOffConflicts(IGitModule module, bool rebase)
    {
        bool conflicted = rebase
            ? module.InTheMiddleOfRebase() && module.InTheMiddleOfConflictedMerge()
            : module.InTheMiddleOfConflictedMerge();
        if (conflicted)
        {
            UICommands.StartResolveConflictsDialog(this);
        }
    }

    private void PullCurrentBranch(bool rebase)
    {
        IGitModule module = Module;
        if (!TryResolveRemote(module, requiresBranch: false, out string remote))
        {
            return;
        }

        RunRemoteCommand(RemoteWorkflow.BuildPull(module, remote, rebase), ScriptEvent.BeforePull, ScriptEvent.AfterPull);
    }

    private void PushCurrentBranch(ForcePushOptions force)
    {
        IGitModule module = Module;
        if (!TryResolveRemote(module, requiresBranch: true, out string remote))
        {
            return;
        }

        if (force != ForcePushOptions.DoNotForce
            && !ConfirmForcePush(module, remote))
        {
            return;
        }

        RunRemoteCommand(RemoteWorkflow.BuildPush(module, remote, force), ScriptEvent.BeforePush, ScriptEvent.AfterPush);
    }

    private bool ConfirmForcePush(IGitModule module, string remote)
    {
        return MessageBoxes.Show(
            this,
            string.Format(GitUI.TranslatedStrings.ConfirmForcePush, module.GetSelectedBranch(emptyIfDetached: true), remote),
            GitUI.TranslatedStrings.ForcePushMenu,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private bool TryResolveRemote(IGitModule module, bool requiresBranch, out string remote)
    {
        remote = RemoteWorkflow.ResolveRemote(module) ?? string.Empty;
        string? error = RemoteWorkflow.Validate(module, requiresBranch);
        if (error is not null)
        {
            lblStatus.Text = error;
            MessageBoxes.ShowError(this, error);
            return false;
        }

        return true;
    }

    private bool RunRemoteCommand(ArgumentString arguments, ScriptEvent beforeEvent = ScriptEvent.None, ScriptEvent afterEvent = ScriptEvent.None)
    {
        return RunWithLifecycleScripts(beforeEvent, afterEvent, () => UICommands.StartGitCommandProcessDialog(this, arguments));
    }

    // After scripts run only on success, matching the WinForms behaviour; a failing before script cancels the command.
    private bool RunWithLifecycleScripts(ScriptEvent beforeEvent, ScriptEvent afterEvent, Func<bool> run)
    {
        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        IScriptsRunner? scriptsRunner = beforeEvent == ScriptEvent.None && afterEvent == ScriptEvent.None
            ? null
            : UICommands.GetRequiredService<IScriptsRunner>();
        if (beforeEvent != ScriptEvent.None && scriptsRunner?.RunEventScripts(beforeEvent, this) == false)
        {
            return false;
        }

        bool succeeded = run();
        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }

        if (succeeded && afterEvent != ScriptEvent.None)
        {
            scriptsRunner?.RunEventScripts(afterEvent, this);
        }

        return succeeded;
    }

    private void InitializeBranchContextMenu()
    {
        MenuItem checkoutItem = new() { Header = GitUI.TranslatedStrings.CheckoutBranchMenu };
        MenuItem checkoutWithMergeItem = new() { Header = GitUI.TranslatedStrings.CheckoutBranchMergeChangesMenu };
        MenuItem checkoutWithStashItem = new() { Header = GitUI.TranslatedStrings.CheckoutBranchStashChangesMenu };
        MenuItem checkoutWithResetItem = new() { Header = GitUI.TranslatedStrings.CheckoutBranchDiscardChangesMenu };
        MenuItem createItem = new() { Header = GitUI.TranslatedStrings.CreateBranchMenu };
        MenuItem createWithoutCheckoutItem = new() { Header = GitUI.TranslatedStrings.CreateBranchWithoutCheckoutMenu };
        MenuItem createOrphanItem = new() { Header = GitUI.TranslatedStrings.CreateOrphanBranchMenu };
        MenuItem renameItem = new() { Header = GitUI.TranslatedStrings.RenameBranchMenu };
        MenuItem deleteItem = new() { Header = GitUI.TranslatedStrings.DeleteBranchMenu };
        MenuItem deleteRemoteBranchItem = new() { Header = GitUI.TranslatedStrings.DeleteRemoteBranchMenu };
        MenuItem forceDeleteItem = new() { Header = GitUI.TranslatedStrings.ForceDeleteBranchMenu };
        MenuItem mergeItem = new() { Header = GitUI.TranslatedStrings.MergeBranchMenu };
        MenuItem rebaseItem = new() { Header = GitUI.TranslatedStrings.RebaseOntoMenu };
        MenuItem createTagItem = new() { Header = GitUI.TranslatedStrings.CreateTagMenu };
        MenuItem deleteTagItem = new() { Header = GitUI.TranslatedStrings.DeleteTagMenu };
        MenuItem stashApplyItem = new() { Header = GitUI.TranslatedStrings.StashApplyMenu };
        MenuItem stashPopItem = new() { Header = GitUI.TranslatedStrings.StashPopMenu };
        MenuItem stashDropItem = new() { Header = GitUI.TranslatedStrings.StashDropMenu };
        stashApplyItem.Click += (_, _) => RunSelectedStash(StashWorkflow.BuildApply);
        stashPopItem.Click += (_, _) => RunSelectedStash(stash => StashWorkflow.BuildPop(stash));
        stashDropItem.Click += (_, _) => DropSelectedStash();
        checkoutItem.Click += (_, _) => CheckoutSelectedRef();
        checkoutWithMergeItem.Click += (_, _) => CheckoutSelectedRef(LocalChangesAction.Merge);
        checkoutWithStashItem.Click += (_, _) => CheckoutSelectedRefWithStash();
        checkoutWithResetItem.Click += (_, _) => CheckoutSelectedRefWithReset();
        createItem.Click += (_, _) => ThreadHelper.FileAndForget(() => CreateBranchAtSelectionAsync(checkout: true));
        createWithoutCheckoutItem.Click += (_, _) => ThreadHelper.FileAndForget(() => CreateBranchAtSelectionAsync(checkout: false));
        createOrphanItem.Click += (_, _) => ThreadHelper.FileAndForget(CreateOrphanBranchAtSelectionAsync);
        renameItem.Click += (_, _) => ThreadHelper.FileAndForget(RenameSelectedBranchAsync);
        deleteItem.Click += (_, _) => DeleteSelectedBranch();
        deleteRemoteBranchItem.Click += (_, _) => DeleteSelectedRemoteBranch();
        forceDeleteItem.Click += (_, _) => ForceDeleteSelectedBranch();
        mergeItem.Click += (_, _) => ThreadHelper.FileAndForget(MergeSelectedBranchAsync);
        rebaseItem.Click += (_, _) => ThreadHelper.FileAndForget(RebaseSelectedBranchAsync);
        createTagItem.Click += (_, _) => ThreadHelper.FileAndForget(CreateTagAtSelectedRefAsync);
        deleteTagItem.Click += (_, _) => DeleteSelectedTag();

        ContextMenu menu = new() { ItemsSource = new[] { checkoutItem, checkoutWithMergeItem, checkoutWithStashItem, checkoutWithResetItem, createItem, createWithoutCheckoutItem, createOrphanItem, renameItem, deleteItem, deleteRemoteBranchItem, forceDeleteItem, mergeItem, rebaseItem, createTagItem, deleteTagItem, stashApplyItem, stashPopItem, stashDropItem } };
        AvaloniaTranslationUtils.NormalizeMenuMnemonics(menu);
        menu.Opening += (_, _) =>
        {
            IGitRef? selectedRef = repoObjectsTree.SelectedRef;
            checkoutItem.IsEnabled = BranchWorkflow.CanCheckout(selectedRef);
            checkoutWithMergeItem.IsEnabled = checkoutItem.IsEnabled;
            checkoutWithStashItem.IsEnabled = checkoutItem.IsEnabled;
            checkoutWithResetItem.IsEnabled = checkoutItem.IsEnabled;
            renameItem.IsEnabled = BranchWorkflow.CanRename(selectedRef);
            deleteItem.IsEnabled = BranchWorkflow.CanDelete(selectedRef);
            deleteRemoteBranchItem.IsEnabled = BranchWorkflow.CanDeleteRemoteBranch(selectedRef);
            forceDeleteItem.IsEnabled = BranchWorkflow.CanForceDelete(selectedRef);
            mergeItem.IsEnabled = MergeWorkflow.CanMerge(selectedRef, Module.GetSelectedBranch(emptyIfDetached: true));
            rebaseItem.IsEnabled = RebaseWorkflow.Validate(Module, selectedRef?.Name) is null && selectedRef is { IsTag: false };
            createTagItem.IsEnabled = selectedRef is not null && TagWorkflow.CanCreateTag(selectedRef.ObjectId);
            deleteTagItem.IsEnabled = TagWorkflow.CanDeleteTag(selectedRef);
            bool stashSelected = repoObjectsTree.SelectedStash is not null;
            stashApplyItem.IsEnabled = stashSelected;
            stashPopItem.IsEnabled = stashSelected;
            stashDropItem.IsEnabled = stashSelected;
            createItem.IsEnabled = selectedRef is not null || RevisionGrid.SelectedRevision is not null;
            createWithoutCheckoutItem.IsEnabled = createItem.IsEnabled;
            createOrphanItem.IsEnabled = createItem.IsEnabled;
        };
        repoObjectsTree.ContextMenu = menu;
        InitializeRevisionContextMenu();
    }

    private async Task MergeSelectedBranchAsync()
    {
        IGitRef? source = repoObjectsTree.SelectedRef;
        if (source is null || !MergeWorkflow.CanMerge(source, Module.GetSelectedBranch(emptyIfDetached: true)))
        {
            return;
        }

        MergeOptionsWindow dialog = new(source.Name);
        MergeOptions? options = await dialog.ShowDialog<MergeOptions?>(this);
        if (options is not MergeOptions mergeOptions)
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        string? messageFile = MergeWorkflow.WriteMessageFile(mergeOptions.Message);
        try
        {
            RunWithLifecycleScripts(
                ScriptEvent.BeforeMerge,
                ScriptEvent.AfterMerge,
                () => UICommands.StartGitCommandProcessDialog(this, MergeWorkflow.BuildMerge(module, source, mergeOptions, messageFile)));
        }
        finally
        {
            if (messageFile is not null)
            {
                File.Delete(messageFile);
            }
        }

        if (!IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        UICommands.RepoChangedNotifier.Notify();
        if (module.InTheMiddleOfConflictedMerge())
        {
            UICommands.StartResolveConflictsDialog(this);
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task RebaseSelectedBranchAsync()
    {
        IGitRef? source = repoObjectsTree.SelectedRef;
        IGitModule module = Module;
        string? validationError = RebaseWorkflow.Validate(module, source?.Name);
        if (source is null || source.IsTag || validationError is not null)
        {
            if (validationError is not null)
            {
                MessageBoxes.ShowError(this, validationError);
            }

            return;
        }

        RebaseOptionsWindow dialog = new(source.Name, AppSettings.RebaseAutoStash, module.GetEffectiveSetting<bool>("rebase.autosquash") is true);
        RebaseConfiguration? configuration = await dialog.ShowDialog<RebaseConfiguration?>(this);
        if (configuration is null)
        {
            return;
        }

        validationError = RebaseWorkflow.Validate(module, configuration.Onto);
        if (validationError is not null)
        {
            MessageBoxes.ShowError(this, validationError);
            return;
        }

        AppSettings.RebaseAutoStash = configuration.AutoStash;
        long loadVersion = _repositoryLoadVersion;
        string? todoPath = null;
        ArgumentString rebaseCommand = RebaseWorkflow.BuildRebase(configuration);
        if (configuration.Interactive)
        {
            IReadOnlyList<RebaseTodoEntry> commits = InteractiveRebaseWorkflow.ListCommits(module, InteractiveRebaseWorkflow.GetRange(configuration));
            if (commits.Count > 0)
            {
                IReadOnlyList<RebaseTodoEntry>? edited = await new InteractiveRebaseWindow(commits).ShowDialog<IReadOnlyList<RebaseTodoEntry>?>(this);
                if (edited is null)
                {
                    return;
                }

                todoPath = Path.Combine(Path.GetTempPath(), $"gitext-rebase-todo-{Guid.NewGuid():N}.txt");
                await File.WriteAllTextAsync(todoPath, InteractiveRebaseWorkflow.BuildTodo(edited));
                rebaseCommand = InteractiveRebaseWorkflow.BuildCommand(configuration, todoPath);
            }
        }

        try
        {
            UICommands.StartGitCommandProcessDialog(this, rebaseCommand);
        }
        finally
        {
            if (todoPath is not null)
            {
                File.Delete(todoPath);
            }
        }

        if (!IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        UICommands.RepoChangedNotifier.Notify();
        if (module.InTheMiddleOfRebase() && module.InTheMiddleOfConflictedMerge())
        {
            UICommands.StartResolveConflictsDialog(this);
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    // Conflicts are left in the working tree; the operation-in-progress banner offers continue/abort.
    private void InitializeRevisionContextMenu()
    {
        MenuItem checkoutRevisionItem = new() { Header = GitUI.TranslatedStrings.CheckoutRevisionMenu };
        MenuItem resetToRevisionItem = new() { Header = GitUI.TranslatedStrings.ResetCurrentBranchToRevisionMenu };
        MenuItem resetSoftItem = new() { Header = GitUI.TranslatedStrings.ResetSoftToRevisionMenu };
        MenuItem resetMixedItem = new() { Header = GitUI.TranslatedStrings.ResetMixedToRevisionMenu };
        MenuItem resetKeepItem = new() { Header = GitUI.TranslatedStrings.ResetKeepToRevisionMenu };
        MenuItem resetMergeItem = new() { Header = GitUI.TranslatedStrings.ResetMergeToRevisionMenu };
        MenuItem resetHardItem = new() { Header = GitUI.TranslatedStrings.ResetHardToRevisionMenu };
        MenuItem cherryPickItem = new() { Header = GitUI.TranslatedStrings.CherryPickMenu };
        MenuItem revertItem = new() { Header = GitUI.TranslatedStrings.RevertCommitMenu };
        MenuItem diffToolItem = new() { Header = GitUI.TranslatedStrings.DiffToolRevisionsMenu };
        MenuItem compareItem = new() { Header = GitUI.TranslatedStrings.CompareRevisionsMenu };
        MenuItem compareToHeadItem = new() { Header = GitUI.TranslatedStrings.CompareToHeadMenu };
        MenuItem copyMenuItem = new() { Header = GitUI.TranslatedStrings.CopyMenu };
        copyMenuItem.ItemsSource = new[]
        {
            CreateCopyItem(GitUI.TranslatedStrings.CopyFullHashMenu, RevisionCopyKind.FullHash),
            CreateCopyItem(GitUI.TranslatedStrings.CopyShortHashMenu, RevisionCopyKind.ShortHash),
            CreateCopyItem(GitUI.TranslatedStrings.CopySubjectMenu, RevisionCopyKind.Subject),
            CreateCopyItem(GitUI.TranslatedStrings.CopyHashAndSubjectMenu, RevisionCopyKind.HashAndSubject),
            CreateCopyItem(GitUI.TranslatedStrings.CopyAuthorMenu, RevisionCopyKind.Author),
        };
        compareToHeadItem.Click += (_, _) => CompareSelectedWithHead();
        MenuItem cherryPickOriginItem = new() { Header = GitUI.TranslatedStrings.CherryPickRecordOriginMenu };
        MenuItem revertNoCommitItem = new() { Header = GitUI.TranslatedStrings.RevertNoCommitMenu };
        MenuItem cherryPickSelectedItem = new() { Header = GitUI.TranslatedStrings.CherryPickSelectedMenu };
        MenuItem revertSelectedItem = new() { Header = GitUI.TranslatedStrings.RevertSelectedMenu };
        MenuItem createTagItem = new() { Header = GitUI.TranslatedStrings.CreateTagMenu };
        MenuItem archiveRevisionItem = new() { Header = GitUI.TranslatedStrings.ArchiveRevisionMenu };
        checkoutRevisionItem.Click += (_, _) => CheckoutSelectedRevision();
        resetSoftItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Soft);
        resetMixedItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Mixed);
        resetKeepItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Keep);
        resetMergeItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Merge);
        resetHardItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Hard);
        resetToRevisionItem.ItemsSource = new[] { resetSoftItem, resetMixedItem, resetKeepItem, resetMergeItem, resetHardItem };
        cherryPickItem.Click += (_, _) => ThreadHelper.FileAndForget(() => RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildCherryPick));
        revertItem.Click += (_, _) => ThreadHelper.FileAndForget(() => RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildRevert));
        diffToolItem.Click += (_, _) =>
        {
            if (ExternalToolWorkflow.ResolveComparison([.. RevisionGrid.SelectedRevisions]) is (string first, string second))
            {
                OpenDiffTool(first, second);
            }
        };
        compareItem.Click += (_, _) => ShowCompareRevisions();
        cherryPickOriginItem.Click += (_, _) => ThreadHelper.FileAndForget(() => RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildCherryPickRecordingOrigin));
        revertNoCommitItem.Click += (_, _) => ThreadHelper.FileAndForget(() => RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildRevertWithoutCommit));
        cherryPickSelectedItem.Click += (_, _) => RunSelectedRevisionsSequence(revisions => CommitPickWorkflow.BuildCherryPickSequence(revisions, recordOrigin: false));
        revertSelectedItem.Click += (_, _) => RunSelectedRevisionsSequence(revisions => CommitPickWorkflow.BuildRevertSequence(revisions, autoCommit: true));
        createTagItem.Click += (_, _) => ThreadHelper.FileAndForget(CreateTagAtSelectedRevisionAsync);
        archiveRevisionItem.Click += (_, _) => ArchiveSelectedRevisions();

        ContextMenu menu = new() { ItemsSource = new[] { checkoutRevisionItem, resetToRevisionItem, compareItem, compareToHeadItem, diffToolItem, copyMenuItem, cherryPickItem, cherryPickOriginItem, cherryPickSelectedItem, revertItem, revertNoCommitItem, revertSelectedItem, createTagItem, archiveRevisionItem } };
        AvaloniaTranslationUtils.NormalizeMenuMnemonics(menu);
        menu.Opening += (_, _) =>
        {
            checkoutRevisionItem.IsEnabled = BranchWorkflow.CanCheckoutRevision(RevisionGrid.SelectedRevision);
            resetToRevisionItem.IsEnabled = ResetWorkflow.CanResetTo(RevisionGrid.SelectedRevision);
            bool canApply = CommitPickWorkflow.CanApply(RevisionGrid.SelectedRevision);
            bool single0 = RevisionGrid.SelectedRevisions.Count == 1;
            compareItem.IsEnabled = ExternalToolWorkflow.ResolveComparison([.. RevisionGrid.SelectedRevisions]) is not null;
            compareToHeadItem.IsEnabled = single0 && !RevisionGrid.SelectedRevisions[0].IsArtificial;
            copyMenuItem.IsEnabled = RevisionGrid.SelectedRevisions.Count > 0;
            diffToolItem.IsEnabled = ExternalToolWorkflow.ResolveComparison([.. RevisionGrid.SelectedRevisions]) is not null;
            bool single = RevisionGrid.SelectedRevisions.Count == 1;
            bool canApplySequence = !single && CommitPickWorkflow.CanApplySequence(Module, RevisionGrid.SelectedRevisions);
            cherryPickItem.IsEnabled = canApply && single;
            cherryPickOriginItem.IsEnabled = canApply && single;
            revertItem.IsEnabled = canApply && single;
            revertNoCommitItem.IsEnabled = canApply && single;
            cherryPickSelectedItem.IsEnabled = canApplySequence;
            revertSelectedItem.IsEnabled = canApplySequence;
            createTagItem.IsEnabled = RevisionGrid.SelectedRevision is GitRevision revision && TagWorkflow.CanCreateTag(revision.ObjectId);
            archiveRevisionItem.IsEnabled = RevisionGrid.SelectedRevisions.Count > 0;
        };
        RevisionGrid.ContextMenu = menu;
    }

    private MenuItem CreateCopyItem(string header, RevisionCopyKind kind)
    {
        MenuItem item = new() { Header = header };
        item.Click += (_, _) => ThreadHelper.FileAndForget(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (Clipboard is { } clipboard && RevisionGrid.SelectedRevisions.Count > 0)
            {
                await clipboard.SetTextAsync(RevisionCopyWorkflow.Format(RevisionGrid.SelectedRevisions, kind));
            }
        });
        return item;
    }

    private void CompareSelectedWithHead()
    {
        IGitModule module = Module;
        ObjectId headId = module.GetCurrentCheckout();
        if (RevisionGrid.SelectedRevision is not GitRevision selected || headId.IsZero)
        {
            return;
        }

        new CompareRevisionsWindow(module, selected, new GitRevision(headId)).Show(this);
    }

    private void ShowCompareRevisions()
    {
        IReadOnlyList<GitRevision> selection = [.. RevisionGrid.SelectedRevisions];
        if (ExternalToolWorkflow.ResolveComparison(selection) is not (string first, string second))
        {
            return;
        }

        GitRevision? older = selection.FirstOrDefault(revision => revision.ObjectId.ToString() == first);
        GitRevision newer = selection.First(revision => revision.ObjectId.ToString() == second);
        older ??= new GitRevision(newer.FirstParentId);
        new CompareRevisionsWindow(Module, older, newer).Show(this);
    }

    private void ArchiveSelectedRevisions()
    {
        IReadOnlyList<GitRevision> revisions = GetSelectedRevisions();
        if (revisions.Count > 2)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.SelectOnlyOneOrTwoRevisions);
            return;
        }

        if (revisions.Count == 0 || revisions.Any(revision => revision.ObjectId.IsArtificial))
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoRevision);
            return;
        }

        UICommands.StartArchiveDialog(this, revisions[0], revisions.Count == 2 ? revisions[1] : null);
    }

    private void ResetCurrentBranchToSelectedRevision(ResetMode mode)
    {
        if (RevisionGrid.SelectedRevision is not GitRevision revision || !ResetWorkflow.CanResetTo(revision))
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        if (mode == ResetMode.Hard
            && MessageBoxes.Show(
                this,
                GitUI.TranslatedStrings.ConfirmHardResetToRevision,
                GitUI.TranslatedStrings.ResetHardToRevisionTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            RunRemoteCommand(ResetWorkflow.BuildResetTo(revision, mode));
        }
    }

    private void CheckoutSelectedRevision()
    {
        if (RevisionGrid.SelectedRevision is GitRevision revision && BranchWorkflow.CanCheckoutRevision(revision))
        {
            RunBranchCommand(BranchWorkflow.BuildCheckoutRevision(revision.ObjectId));
        }
    }

    private Task CreateTagAtSelectedRefAsync() => CreateTagAsync(repoObjectsTree.SelectedRef?.ObjectId);

    private Task CreateTagAtSelectedRevisionAsync() => CreateTagAsync(RevisionGrid.SelectedRevision?.ObjectId);

    private async Task CreateTagAsync(ObjectId? objectId)
    {
        if (objectId is not ObjectId target || !TagWorkflow.CanCreateTag(target))
        {
            return;
        }

        TagOptionsWindow dialog = new(target);
        TagConfiguration? configuration = await dialog.ShowDialog<TagConfiguration?>(this);
        if (configuration is null)
        {
            return;
        }

        TagConfiguration tagConfiguration = configuration with { ObjectId = target };
        IGitModule module = Module;
        string? validationError = TagWorkflow.ValidateCreate(module, tagConfiguration);
        if (validationError is not null)
        {
            MessageBoxes.ShowError(this, validationError);
            return;
        }

        string? messageFileName = tagConfiguration.Operation.CanProvideMessage() ? Path.GetTempFileName() : null;
        try
        {
            if (messageFileName is not null)
            {
                File.WriteAllText(messageFileName, tagConfiguration.Message);
            }

            RunBranchCommand(TagWorkflow.BuildCreate(tagConfiguration, messageFileName, module.GetPathForGitExecution));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
        finally
        {
            if (messageFileName is not null && File.Exists(messageFileName))
            {
                File.Delete(messageFileName);
            }
        }
    }

    private void DeleteSelectedTag()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !TagWorkflow.CanDeleteTag(gitRef))
        {
            return;
        }

        if (MessageBoxes.Show(
                this,
                string.Format(GitUI.TranslatedStrings.ConfirmDeleteTag, gitRef.Name),
                GitUI.TranslatedStrings.DeleteTagMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunBranchCommand(TagWorkflow.BuildDelete(gitRef.Name));
        }
    }

    private async Task RunSelectedRevisionCommandAsync(
        Func<IGitModule, GitRevision, int, ArgumentString> buildCommand)
    {
        if (RevisionGrid.SelectedRevision is not GitRevision revision || !CommitPickWorkflow.CanApply(revision))
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        int mainlineParent = 1;
        int parentCount;
        try
        {
            parentCount = module.GetParents(revision.ObjectId).Count;
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
            return;
        }

        if (parentCount > 1)
        {
            TextPromptWindow prompt = new(
                GitUI.TranslatedStrings.MainlineParentPromptTitle,
                string.Format(GitUI.TranslatedStrings.MainlineParentPrompt, parentCount),
                mainlineParent.ToString());
            string? parentText = await prompt.ShowDialog<string?>(this);
            if (parentText is null || !IsCurrentRepositoryLoad(module, loadVersion))
            {
                return;
            }

            if (!int.TryParse(parentText, out mainlineParent) || mainlineParent < 1 || mainlineParent > parentCount)
            {
                MessageBoxes.ShowError(this, string.Format(GitUI.TranslatedStrings.InvalidMainlineParentError, parentCount));
                return;
            }
        }

        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            RunRemoteCommand(buildCommand(module, revision, mainlineParent));
        }
    }

    private void OpenDiffTool(string firstRevision, string secondRevision)
    {
        IGitModule module = Module;
        if (ExternalToolWorkflow.GetConfiguredDiffTool(module) is null)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoDiffToolConfiguredError);
            return;
        }

        try
        {
            module.OpenWithDifftoolDirDiff(firstRevision, secondRevision, customTool: null);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private void RunSelectedRevisionsSequence(Func<IReadOnlyList<GitRevision>, ArgumentString> buildCommand)
    {
        IReadOnlyList<GitRevision> revisions = [.. RevisionGrid.SelectedRevisions];
        IGitModule module = Module;
        if (!CommitPickWorkflow.CanApplySequence(module, revisions))
        {
            return;
        }

        RunRemoteCommand(buildCommand(revisions));
        if (module.InTheMiddleOfConflictedMerge())
        {
            UICommands.StartResolveConflictsDialog(this);
        }
    }

    private void RunSelectedStash(Func<GitStash, ArgumentString> buildCommand)
    {
        if (repoObjectsTree.SelectedStash is GitStash stash)
        {
            IGitModule module = Module;
            long loadVersion = _repositoryLoadVersion;
            UICommands.StartGitCommandProcessDialog(this, buildCommand(stash));
            if (!IsCurrentRepositoryLoad(module, loadVersion))
            {
                return;
            }

            if (module.InTheMiddleOfConflictedMerge()
                && MessageBoxes.Show(
                    this,
                    GitUI.TranslatedStrings.OfferResolveStashConflicts,
                    GitUI.TranslatedStrings.ResolveMergeConflicts,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                UICommands.StartResolveConflictsDialog(this, offerCommit: false);
            }

            if (IsCurrentRepositoryLoad(module, loadVersion))
            {
                UICommands.RepoChangedNotifier.Notify();
            }
        }
    }

    private async Task ShowStashManagerAsync()
    {
        IGitModule module = Module;
        StashManagerWindow dialog = new(module, module.GetStashes(noLocks: true));
        StashManagerResult? result = await dialog.ShowDialog<StashManagerResult?>(this);
        if (result is null)
        {
            return;
        }

        switch (result.Action)
        {
            case StashManagerAction.Apply when result.Stash is not null:
                UICommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildApply(result.Stash));
                break;
            case StashManagerAction.Pop when result.Stash is not null:
                UICommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildPop(result.Stash));
                break;
            case StashManagerAction.Drop when result.Stash is not null:
                if (AppSettings.DontConfirmStashDrop
                    || MessageBoxes.Show(this, string.Format(GitUI.TranslatedStrings.ConfirmStashDrop, result.Stash.Name), GitUI.TranslatedStrings.StashDropConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    UICommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildDrop(result.Stash));
                }

                break;
            case StashManagerAction.Clear:
                if (MessageBoxes.Show(this, GitUI.TranslatedStrings.ConfirmStashClear, GitUI.TranslatedStrings.StashDropConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    UICommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildClear());
                }

                break;
        }

        UICommands.RepoChangedNotifier.Notify();
    }

    private void DropSelectedStash()
    {
        if (repoObjectsTree.SelectedStash is not GitStash stash)
        {
            return;
        }

        if (AppSettings.DontConfirmStashDrop
            || MessageBoxes.Show(this, string.Format(GitUI.TranslatedStrings.ConfirmStashDrop, stash.Name), GitUI.TranslatedStrings.StashDropConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunRemoteCommand(StashWorkflow.BuildDrop(stash));
        }
    }

    private async Task StashChangesAsync(IReadOnlyList<string>? selectedFiles = null)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        int changedFileCount = selectedFiles?.Count ?? workingTreePanel.ChangedCount;
        string? error = StashWorkflow.ValidateSave(changedFileCount);
        if (error is not null)
        {
            lblStatus.Text = error;
            MessageBoxes.ShowError(this, error);
            return;
        }

        TextPromptWindow prompt = new(GitUI.TranslatedStrings.StashChangesMenu, GitUI.TranslatedStrings.StashMessagePrompt);
        string? message = await prompt.ShowDialog<string?>(this);
        if (message is null)
        {
            return;
        }

        RunRemoteCommand(StashWorkflow.BuildSave(
            AppSettings.IncludeUntrackedFilesInManualStash,
            AppSettings.StashKeepIndex,
            message,
            selectedFiles));
    }

    private async Task StashStagedAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (!Module.GitVersion.SupportStashStaged)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.StashStagedMenu + ": git >= 2.35 required.");
            return;
        }

        string? error = StashWorkflow.ValidateSave(workingTreePanel.StagedCount);
        if (error is not null)
        {
            lblStatus.Text = error;
            MessageBoxes.ShowError(this, error);
            return;
        }

        TextPromptWindow prompt = new(GitUI.TranslatedStrings.StashStagedMenu, GitUI.TranslatedStrings.StashMessagePrompt);
        string? message = await prompt.ShowDialog<string?>(this);
        if (message is null)
        {
            return;
        }

        RunRemoteCommand(StashWorkflow.BuildSaveStaged(message));
    }

    private async Task ConfigureStashOptionsAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        StashOptionsWindow dialog = new(
            AppSettings.IncludeUntrackedFilesInManualStash,
            AppSettings.StashKeepIndex);
        ManualStashOptions? options = await dialog.ShowDialog<ManualStashOptions?>(this);
        if (options is not null)
        {
            AppSettings.IncludeUntrackedFilesInManualStash = options.IncludeUntrackedFiles;
            AppSettings.StashKeepIndex = options.KeepIndex;
        }
    }

    private void RunBranchCommand(IGitCommand command)
    {
        bool isCheckout = command.Arguments.ToString().Contains("checkout ", StringComparison.Ordinal);
        RunWithLifecycleScripts(
            isCheckout ? ScriptEvent.BeforeCheckout : ScriptEvent.None,
            isCheckout ? ScriptEvent.AfterCheckout : ScriptEvent.None,
            () => UICommands.StartCommandLineProcessDialog(this, command));
    }

    private void CheckoutSelectedRef(LocalChangesAction localChanges = LocalChangesAction.DontChange)
    {
        if (repoObjectsTree.SelectedRef is IGitRef gitRef && BranchWorkflow.CanCheckout(gitRef))
        {
            RunBranchCommand(BranchWorkflow.BuildCheckout(gitRef, localChanges));
        }
    }

    private void CheckoutSelectedRefWithStash()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !BranchWorkflow.CanCheckout(gitRef))
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        bool hasLocalChanges = module.IsDirtyDir();
        if (hasLocalChanges
            && !UICommands.StartGitCommandProcessDialog(
                this,
                StashWorkflow.BuildSave(
                    AppSettings.IncludeUntrackedFilesInAutoStash,
                    keepIndex: false,
                    message: string.Empty)))
        {
            return;
        }

        if (!IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        bool checkoutSucceeded = UICommands.StartCommandLineProcessDialog(this, BranchWorkflow.BuildCheckout(gitRef));
        if (!IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        if (!checkoutSucceeded)
        {
            if (hasLocalChanges)
            {
                MessageBoxes.Show(
                    this,
                    GitUI.TranslatedStrings.CheckoutFailedChangesStashed,
                    GitUI.TranslatedStrings.CheckoutBranchStashChangesMenu,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            UICommands.RepoChangedNotifier.Notify();
            return;
        }

        if (hasLocalChanges
            && MessageBoxes.Show(
                this,
                string.Format(GitUI.TranslatedStrings.ConfirmReapplyStashedChanges, gitRef.Name),
                GitUI.TranslatedStrings.CheckoutBranchStashChangesMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            UICommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildApplyLatest());
        }

        UICommands.RepoChangedNotifier.Notify();
    }

    private void CheckoutSelectedRefWithReset()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !BranchWorkflow.CanCheckout(gitRef))
        {
            return;
        }

        string confirmation = string.Format(GitUI.TranslatedStrings.ConfirmCheckoutDiscardChanges, gitRef.Name);
        if (MessageBoxes.Show(
                this,
                confirmation,
                GitUI.TranslatedStrings.CheckoutBranchDiscardChangesMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunBranchCommand(BranchWorkflow.BuildCheckout(gitRef, LocalChangesAction.Reset));
        }
    }

    private void DeleteSelectedBranch()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !BranchWorkflow.CanDelete(gitRef))
        {
            return;
        }

        if (ShowBranchWorktreeWarning(gitRef))
        {
            return;
        }

        if (MessageBoxes.Show(this, string.Format(GitUI.TranslatedStrings.ConfirmDeleteBranch, gitRef.Name), GitUI.TranslatedStrings.DeleteBranchMenu, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            RunBranchCommand(BranchWorkflow.BuildDelete(gitRef, force: false));
        }
    }

    private void DeleteSelectedRemoteBranch()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !BranchWorkflow.CanDeleteRemoteBranch(gitRef))
        {
            return;
        }

        string confirmation = string.Format(GitUI.TranslatedStrings.ConfirmDeleteRemoteBranch, gitRef.LocalName, gitRef.Remote);
        if (MessageBoxes.Show(
                this,
                confirmation,
                GitUI.TranslatedStrings.DeleteRemoteBranchMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            IGitModule module = Module;
            IGitRef? trackingBranch = BranchWorkflow.FindTrackingBranch(gitRef, module.GetRefs(RefsFilter.Heads));
            bool trackingBranchCanBeDeleted = trackingBranch is { IsSelected: false }
                && BranchWorkflow.FindWorktreeUsingBranch(trackingBranch, module.GetWorktrees()) is null;
            if (!UICommands.StartCommandLineProcessDialog(this, BranchWorkflow.BuildDeleteRemoteBranch(gitRef))
                || !ReferenceEquals(Module, module))
            {
                return;
            }

            if (trackingBranchCanBeDeleted
                && trackingBranch is not null
                && MessageBoxes.Show(
                    this,
                    string.Format(GitUI.TranslatedStrings.ConfirmDeleteLocalTrackingBranch, trackingBranch.Name),
                    GitUI.TranslatedStrings.DeleteRemoteBranchMenu,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                RunBranchCommand(BranchWorkflow.BuildDelete(trackingBranch, force: false));
            }
        }
    }

    private void ForceDeleteSelectedBranch()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !BranchWorkflow.CanForceDelete(gitRef))
        {
            return;
        }

        if (ShowBranchWorktreeWarning(gitRef))
        {
            return;
        }

        string confirmation = string.Format(GitUI.TranslatedStrings.ConfirmForceDeleteBranch, gitRef.Name);
        if (MessageBoxes.Show(
                this,
                confirmation,
                GitUI.TranslatedStrings.ForceDeleteBranchMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunBranchCommand(BranchWorkflow.BuildDelete(gitRef, force: true));
        }
    }

    private bool ShowBranchWorktreeWarning(IGitRef gitRef)
    {
        GitWorktree? worktree = BranchWorkflow.FindWorktreeUsingBranch(gitRef, Module.GetWorktrees());
        if (worktree is null)
        {
            return false;
        }

        string message = string.Format(GitUI.TranslatedStrings.BranchCheckedOutInWorktree, gitRef.Name, worktree.Path);
        MessageBoxes.Show(this, message, GitUI.TranslatedStrings.DeleteBranchMenu, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return true;
    }

    private async Task RenameSelectedBranchAsync()
    {
        if (repoObjectsTree.SelectedRef is not IGitRef gitRef || !BranchWorkflow.CanRename(gitRef))
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        Func<string, string>? normaliseBranchName = null;
        if (AppSettings.AutoNormaliseBranchName)
        {
            IGitBranchNameNormaliser branchNameNormaliser = UICommands.GetRequiredService<IGitBranchNameNormaliser>();
            GitBranchNameOptions options = new(AppSettings.AutoNormaliseSymbol);
            normaliseBranchName = branchName => BranchWorkflow.NormaliseBranchName(branchName, branchNameNormaliser, options);
        }

        TextPromptWindow prompt = new(
            GitUI.TranslatedStrings.RenameBranchMenu,
            GitUI.TranslatedStrings.BranchNamePrompt,
            gitRef.Name,
            normaliseBranchName);
        string? enteredName = await prompt.ShowDialog<string?>(this);
        if (enteredName is null)
        {
            return;
        }

        string newName = enteredName.Trim();
        if (newName == gitRef.Name || !IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        string? error = BranchWorkflow.ValidateNewBranchName(module, newName);
        if (error is not null)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        bool succeeded = UICommands.StartGitCommandProcessDialog(this, BranchWorkflow.BuildRename(gitRef.Name, newName));
        if (succeeded && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task CreateBranchAtSelectionAsync(bool checkout)
    {
        IGitRef? selectedRef = repoObjectsTree.SelectedRef;
        GitRevision? selectedRevision = RevisionGrid.SelectedRevision;
        ObjectId? startPoint = selectedRef?.ObjectId ?? selectedRevision?.ObjectId;
        if (startPoint is null)
        {
            return;
        }

        string title = checkout
            ? GitUI.TranslatedStrings.CreateBranchMenu
            : GitUI.TranslatedStrings.CreateBranchWithoutCheckoutMenu;
        string suggestedName = BranchWorkflow.GetSuggestedBranchName(selectedRef, selectedRevision);
        TextPromptWindow prompt = new(title, GitUI.TranslatedStrings.BranchNamePrompt, suggestedName);
        string? name = await prompt.ShowDialog<string?>(this);
        if (name is null)
        {
            return;
        }

        IGitModule module = Module;
        string? error = BranchWorkflow.ValidateNewBranchName(module, name);
        if (error is not null)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        bool succeeded = UICommands.StartGitCommandProcessDialog(this, BranchWorkflow.BuildCreate(name, startPoint.Value, checkout));
        if (!succeeded)
        {
            return;
        }

        UICommands.RepoChangedNotifier.Notify();
    }

    private async Task CreateOrphanBranchAtSelectionAsync()
    {
        IGitRef? selectedRef = repoObjectsTree.SelectedRef;
        GitRevision? selectedRevision = RevisionGrid.SelectedRevision;
        ObjectId? startPoint = selectedRef?.ObjectId ?? selectedRevision?.ObjectId;
        if (startPoint is null)
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        TextPromptWindow prompt = new(
            GitUI.TranslatedStrings.CreateOrphanBranchMenu,
            GitUI.TranslatedStrings.BranchNamePrompt,
            BranchWorkflow.GetSuggestedBranchName(selectedRef, selectedRevision));
        string? name = await prompt.ShowDialog<string?>(this);
        if (name is null || !IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        string? error = BranchWorkflow.ValidateNewBranchName(module, name);
        if (error is not null)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        bool succeeded = UICommands.StartGitCommandProcessDialog(
            this,
            BranchWorkflow.BuildCreateOrphan(name.Trim(), startPoint.Value));
        if (succeeded && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private void CommitStagedChanges(CommitRequest request)
    {
        if (_workingTreeOperationInProgress)
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        int stagedCount = workingTreePanel.StagedCount;

        try
        {
            RepositoryOperationState state = GetRepositoryOperationState(module);
            string? error = CommitWorkflow.Validate(request, stagedCount, state.Operation == RepositoryOperation.Merge, state.HasUnresolvedConflicts);
            if (error is not null)
            {
                lblStatus.Text = error;
                MessageBoxes.ShowError(this, error);
                return;
            }

            foreach (string warning in request.UseExplicitCommitMessage
                ? CommitWorkflow.GetMessageValidationWarnings(request.Message)
                : [])
            {
                if (MessageBoxes.Show(
                    this,
                    warning,
                    GitUI.TranslatedStrings.CommitValidationCaption,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Asterisk) != DialogResult.Yes)
                {
                    return;
                }
            }

            if (!AppSettings.DontConfirmCommitIfNoBranch
                && string.IsNullOrEmpty(module.GetSelectedBranch(emptyIfDetached: true))
                && !module.InTheMiddleOfRebase()
                && MessageBoxes.Show(
                    this,
                    GitUI.TranslatedStrings.ErrorInstructionNotOnBranch,
                    GitUI.TranslatedStrings.ErrorCaptionNotOnBranch,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            if (request.Amend
                && !AppSettings.DontConfirmAmend.Value
                && MessageBoxes.Show(
                    this,
                    GitUI.TranslatedStrings.AmendCommitConfirmation,
                    GitUI.TranslatedStrings.AmendCommitCaption,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return;
            }

            if (request.UseExplicitCommitMessage)
            {
                AppSettings.LastCommitMessage = request.Message;
            }

            CommitMessageManager messageManager = new(new GitExtensions.Shims.WinForms.Control(), module.WorkingDirGitDir, module.CommitEncoding);
            if (request.UseExplicitCommitMessage)
            {
                ThreadHelper.JoinableTaskFactory.Run(
                    () => messageManager.WriteCommitMessageToFileAsync(
                        request.Message,
                        CommitMessageType.Normal,
                        usingCommitTemplate: request.UsingCommitTemplate,
                        ensureCommitMessageSecondLineEmpty: AppSettings.EnsureCommitMessageSecondLineEmpty));
            }

            IScriptsRunner scriptsRunner = UICommands.GetRequiredService<IScriptsRunner>();
            if (!scriptsRunner.RunEventScripts(ScriptEvent.BeforeCommit, this))
            {
                return;
            }

            ArgumentString arguments = CommitWorkflow.BuildCommand(request, stagedCount, messageManager.CommitMessagePath, module.GetPathForGitExecution);
            bool succeeded = UICommands.StartGitCommandProcessDialog(this, arguments);

            if (IsCurrentRepositoryLoad(module, loadVersion))
            {
                UICommands.RepoChangedNotifier.Notify();
            }

            if (succeeded)
            {
                scriptsRunner.RunEventScripts(ScriptEvent.AfterCommit, this);
                ThreadHelper.JoinableTaskFactory.Run(messageManager.ResetCommitMessageAsync);
                workingTreePanel.ResetCommitOptions();
                LoadCommitOptions(module, loadVersion, loadTemplates: false);
                if (request.PushAfterCommit)
                {
                    ThreadHelper.FileAndForget(PushWithOptionsAsync);
                }
            }
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private void IgnoreSelectedFiles()
    {
        string[] paths = [.. workingTreePanel.SelectedItems
            .Where(item => !item.IsTracked && item.IsNew)
            .Select(item => item.Name)];
        if (paths.Length == 0)
        {
            return;
        }

        try
        {
            IgnoreWorkflow.AddToGitIgnore(Module.WorkingDir, paths);
            UICommands.RepoChangedNotifier.Notify();
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private async Task GoToCommitAsync()
    {
        if (!repositoryWorkspace.IsVisible)
        {
            return;
        }

        IGitModule module = Module;
        TextPromptWindow prompt = new(GitUI.TranslatedStrings.GoToCommitMenu, GitUI.TranslatedStrings.GoToCommitPrompt, "HEAD");
        string? expression = (await prompt.ShowDialog<string?>(this))?.Trim();
        if (string.IsNullOrEmpty(expression) || !ReferenceEquals(Module, module))
        {
            return;
        }

        ObjectId objectId;
        try
        {
            objectId = await Task.Run(() => module.RevParse(expression));
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
            return;
        }

        if (objectId.IsZero)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoResultsFound);
            return;
        }

        RevisionGrid.SelectRevision(objectId);
        if (RevisionGrid.SelectedRevision?.ObjectId != objectId)
        {
            MessageBoxes.ShowError(this, string.Format(GitUI.TranslatedStrings.GoToCommitNotLoaded, objectId.ToShortString()));
        }
    }

    private void GoToSuperproject()
    {
        if (Module.SuperprojectModule?.WorkingDir is string superprojectDir)
        {
            OpenRepository(superprojectDir);
        }
    }

    private void ShowCommitPanel()
    {
        if (!repositoryWorkspace.IsVisible)
        {
            return;
        }

        fileListTabs.SelectedItem = workingTreeTab;
        workingTreePanel.FocusCommitMessage();
    }

    private void LoadAmendMessage(bool isAmend)
    {
        if (!isAmend
            || !AppSettings.UseFormCommitMessage
            || !string.IsNullOrWhiteSpace(workingTreePanel.CommitMessage))
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        ThreadHelper.FileAndForget(async () =>
        {
            string? lastMessage = module.GetPreviousCommitMessages(1, "HEAD", string.Empty).FirstOrDefault();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!string.IsNullOrEmpty(lastMessage)
                && IsCurrentRepositoryLoad(module, loadVersion)
                && string.IsNullOrWhiteSpace(workingTreePanel.CommitMessage))
            {
                workingTreePanel.SetCommitMessage(lastMessage);
            }
        });
    }

    private void LoadCommitOptions(IGitModule module, long loadVersion, bool loadTemplates)
    {
        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                int messageCount = AppSettings.CommitDialogNumberOfPreviousMessages;
                IReadOnlyList<CommitMessageHistoryItem> messages = messageCount > 0
                    ? CommitWorkflow.BuildMessageHistory(
                        module.GetPreviousCommitMessages(messageCount, "HEAD", string.Empty),
                        AppSettings.LastCommitMessage,
                        messageCount)
                    : [];
                List<CommitTemplateItem>? templates = null;
                Exception? gitTemplateLoadError = null;
                if (loadTemplates)
                {
                    CommitTemplateManager templateManager = new(() => module);
                    templates = [.. templateManager.RegisteredTemplates];
                    templates.AddRange(CommitTemplateItem.LoadFromSettings() ?? []);
                    string? gitTemplate = null;
                    try
                    {
                        gitTemplate = templateManager.LoadGitCommitTemplate();
                    }
                    catch (Exception exception)
                    {
                        gitTemplateLoadError = exception;
                    }

                    if (!string.IsNullOrEmpty(gitTemplate))
                    {
                        templates.Insert(0, new CommitTemplateItem(GitUI.TranslatedStrings.GitCommitTemplate, gitTemplate, icon: null, isRegex: false));
                    }
                }

                string pendingMessage = string.Empty;
                if (AppSettings.UseFormCommitMessage)
                {
                    CommitMessageManager pendingMessageManager = new(new GitExtensions.Shims.WinForms.Control(), module.WorkingDirGitDir, module.CommitEncoding);
                    if (pendingMessageManager.IsMergeCommit)
                    {
                        pendingMessage = await pendingMessageManager.GetMergeOrCommitMessageAsync();
                    }
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (!IsCurrentRepositoryLoad(module, loadVersion))
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(pendingMessage) && string.IsNullOrWhiteSpace(workingTreePanel.CommitMessage))
                {
                    workingTreePanel.SetCommitMessage(pendingMessage);
                }

                workingTreePanel.SetCommitHistory(messages);
                if (templates is not null)
                {
                    workingTreePanel.SetCommitTemplates(templates);
                }

                if (gitTemplateLoadError is not null)
                {
                    MessageBoxes.ShowError(this, gitTemplateLoadError.ToStringWithData());
                }
            }
            catch (Exception exception)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                if (IsCurrentRepositoryLoad(module, loadVersion))
                {
                    MessageBoxes.ShowError(this, exception.ToStringWithData());
                }
            }
        });
    }

    private void DiscardSelectedChanges()
    {
        GitItemStatus[] selectedItems = [.. workingTreePanel.SelectedItems];
        string? confirmation = ResetWorkflow.BuildDiscardConfirmation(selectedItems);
        if (_workingTreeOperationInProgress
            || confirmation is null
            || MessageBoxes.Show(this, confirmation, GitUI.TranslatedStrings.ResetChangesCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        RunWorkingTreeReset(module => ResetWorkflow.Discard(module, selectedItems, new FullPathResolver(() => module.WorkingDir), out string output) ? string.Empty : output);
    }

    private void ResetAllChanges()
    {
        if (_workingTreeOperationInProgress
            || MessageBoxes.Show(this, GitUI.TranslatedStrings.ConfirmResetAllChanges, GitUI.TranslatedStrings.ResetChangesCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        RunWorkingTreeReset(module => ResetWorkflow.ResetAll(module) ? string.Empty : GitUI.TranslatedStrings.Error);
    }

    private void RunWorkingTreeReset(Func<IGitModule, string> reset)
    {
        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        _workingTreeOperationInProgress = true;
        workingTreePanel.SetOperationInProgress(isInProgress: true);

        ThreadHelper.FileAndForget(async () =>
        {
            string error;
            try
            {
                error = reset(module);
            }
            catch (Exception exception)
            {
                error = exception.ToStringWithData();
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (!IsCurrentRepositoryLoad(module, loadVersion))
            {
                return;
            }

            _workingTreeOperationInProgress = false;
            workingTreePanel.SetOperationInProgress(isInProgress: false);
            LoadWorkingTreeStatus(module, loadVersion);
            UICommands.RepoChangedNotifier.Notify();

            if (error.Length > 0)
            {
                lblStatus.Text = error;
                MessageBoxes.ShowError(this, error);
            }
        });
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

    private void DiscardSelectedLines()
    {
        if (_workingTreeOperationInProgress
            || fileListTabs.SelectedItem != workingTreeTab
            || workingTreePanel.SelectedItem is not GitItemStatus item
            || !ReferenceEquals(_workingTreeDiffItem, item))
        {
            return;
        }

        IGitModule module = Module;
        byte[]? patch = PartialStageWorkflow.CreateDiscardPatch(
            module,
            item,
            fileViewer.Text,
            fileViewer.SelectionPosition,
            fileViewer.SelectionLength);
        if (patch is not { Length: > 0 }
            || !MessageBoxes.Confirm(
                this,
                GitUI.TranslatedStrings.ConfirmDiscardSelectedLines,
                GitUI.TranslatedStrings.DiscardSelectedLines,
                MessageBoxIcon.Warning,
                GitExtensions.Shims.WinForms.MessageBoxDefaultButton.Button2))
        {
            return;
        }

        bool staged = item.Staged == StagedStatus.Index;
        RunWorkingTreePatch(module, () => PartialStageWorkflow.ApplyDiscardPatch(module, patch, staged));
    }

    private void StageSelectedLines(bool unstage)
    {
        if (_workingTreeOperationInProgress
            || fileListTabs.SelectedItem != workingTreeTab
            || workingTreePanel.SelectedItem is not GitItemStatus item
            || !ReferenceEquals(_workingTreeDiffItem, item))
        {
            return;
        }

        IGitModule module = Module;
        if (unstage != PartialStageWorkflow.CanUnstage(item))
        {
            return;
        }

        byte[]? patch = PartialStageWorkflow.CreatePatch(
            module,
            item,
            fileViewer.Text,
            fileViewer.SelectionPosition,
            fileViewer.SelectionLength);
        if (patch is not { Length: > 0 })
        {
            return;
        }

        RunWorkingTreePatch(module, () => PartialStageWorkflow.ApplyPatch(module, patch, unstage));
    }

    private void RunWorkingTreePatch(IGitModule module, Func<ExecutionResult> apply)
    {
        long loadVersion = _repositoryLoadVersion;
        _workingTreeOperationInProgress = true;
        workingTreePanel.SetOperationInProgress(isInProgress: true);

        ThreadHelper.FileAndForget(async () =>
        {
            bool succeeded = false;
            string output;
            try
            {
                ExecutionResult result = apply();
                succeeded = result.ExitedSuccessfully;
                output = result.AllOutput.Trim();
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

            if (succeeded)
            {
                _workingTreeDiffItem = null;
                fileViewer.ViewPatch(string.Empty);
                workingTreePanel.SetCanStageSelectedLines(false);
                workingTreePanel.SetCanUnstageSelectedLines(false);
                workingTreePanel.SetCanDiscardSelectedLines(false);
                UICommands.RepoChangedNotifier.Notify();
            }
            else
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

        GitItemStatus[] items = [.. status.Where(item => !item.IsStatusOnly)];
        int untracked = items.Count(item => !item.IsTracked && item.IsNew);

        return items.Length > 0
            ? string.Format(GitUI.TranslatedStrings.WorkingTreeStatusSummary, items.Length - untracked, untracked)
            : GitUI.TranslatedStrings.NoResultsFound;
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
        RefreshRevisionTree();

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

    private async Task HandleFilePathActionAsync(FileListAction action)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        if (fileStatusList.SelectedItem is not GitItemStatus file)
        {
            return;
        }

        string workingDir = Module.WorkingDir;
        string fullPath = FilePathWorkflow.GetFullPath(workingDir, file.Name);
        switch (action)
        {
            case FileListAction.CopyPath or FileListAction.CopyFullPath:
                if (Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(FilePathWorkflow.FormatPaths([file], workingDir, action == FileListAction.CopyFullPath));
                }

                break;
            case FileListAction.Open:
                if (!File.Exists(fullPath))
                {
                    MessageBoxes.ShowError(this, fullPath);
                    return;
                }

                OpenWithShell(fullPath);
                break;
            case FileListAction.ShowInFolder:
                string? directory = Path.GetDirectoryName(fullPath);
                if (directory is not null && Directory.Exists(directory))
                {
                    OpenWithShell(directory);
                }

                break;
        }

        return;

        static void OpenWithShell(string path)
            => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
    }

    private void OpenSelectedFileHistory(bool showBlame)
    {
        if (fileStatusList.SelectedItem is not GitItemStatus item
            || RevisionGrid.SelectedRevision is not GitRevision revision)
        {
            return;
        }

        FileHistoryWindow window = new(Module, item.Name, revision.ObjectId, showBlame);
        window.Show(this);
    }

    private void WorkingTreePanel_SelectionChanged(object? sender, EventArgs e)
    {
        if (fileListTabs.SelectedItem == workingTreeTab)
        {
            LoadWorkingTreeFileDiff();
        }
    }

    private void FileViewer_SelectionChanged(object? sender, EventArgs e)
    {
        UpdatePartialStageActionState();
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
        else if (fileListTabs.SelectedItem == revisionTreeTab)
        {
            RefreshRevisionTree();
        }
    }

    private void RefreshRevisionTree()
    {
        if (fileListTabs.SelectedItem != revisionTreeTab)
        {
            return;
        }

        if (RevisionGrid.SelectedRevision is GitRevision { IsArtificial: false } revision)
        {
            revisionFileTree.Show(Module, revision.ObjectId);
        }
        else
        {
            revisionFileTree.Clear();
        }

        fileViewer.ViewPatch(string.Empty);
    }

    private void ShowRevisionTreeFile()
    {
        if (_isClosed || fileListTabs.SelectedItem != revisionTreeTab)
        {
            return;
        }

        if (revisionFileTree.SelectedFile is not RevisionTreeEntry entry)
        {
            fileViewer.ViewPatch(string.Empty);
            return;
        }

        IGitModule module = Module;
        CancellationToken cancellationToken = _fileDiffRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            string? text = await Task.Run(() => module.GetFileText(entry.ObjectId, module.FilesEncoding, stripAnsiEscapeCodes: false), cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!cancellationToken.IsCancellationRequested && ReferenceEquals(Module, module))
            {
                fileViewer.ViewPatch(text ?? string.Empty);
            }
        });
    }

    private void OpenRevisionTreeFileHistory()
    {
        if (revisionFileTree.SelectedFile is RevisionTreeEntry entry)
        {
            new FileHistoryWindow(Module, entry.Path, revisionFileTree.Revision).Show(this);
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
            _workingTreeDiffItem = null;
            fileViewer.ViewPatch(string.Empty);
            workingTreePanel.SetCanStageSelectedLines(false);
            workingTreePanel.SetCanUnstageSelectedLines(false);
            workingTreePanel.SetCanDiscardSelectedLines(false);
            return;
        }

        _workingTreeDiffItem = null;
        fileViewer.ViewPatch(string.Empty);
        workingTreePanel.SetCanStageSelectedLines(false);
        workingTreePanel.SetCanUnstageSelectedLines(false);
        workingTreePanel.SetCanDiscardSelectedLines(false);
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
                _workingTreeDiffItem = patch is null ? null : item;
                UpdatePartialStageActionState();
            }
        });
    }

    private void UpdatePartialStageActionState()
    {
        GitItemStatus? item = _workingTreeDiffItem;
        bool hasLineSelection = fileListTabs.SelectedItem == workingTreeTab
            && item is not null
            && ReferenceEquals(item, workingTreePanel.SelectedItem)
            && (PartialStageWorkflow.CanStage(item) || PartialStageWorkflow.CanUnstage(item))
            && fileViewer.SelectionLength > 0
            && PartialStageWorkflow.CreatePatch(
                Module,
                item,
                fileViewer.Text,
                fileViewer.SelectionPosition,
                fileViewer.SelectionLength) is { Length: > 0 };

        workingTreePanel.SetCanStageSelectedLines(hasLineSelection && PartialStageWorkflow.CanStage(item));
        workingTreePanel.SetCanUnstageSelectedLines(hasLineSelection && PartialStageWorkflow.CanUnstage(item));
        workingTreePanel.SetCanDiscardSelectedLines(hasLineSelection);
    }

    private void UICommands_PostRepositoryChanged(object? sender, GitUIEventArgs e)
    {
        this.InvokeAndForget(ReloadRepository);
    }

    private void FormBrowse_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        if (PluginRegistry.IsInitialized)
        {
            PluginRegistry.Unregister(UICommands, this);
        }

        UICommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
        RevisionGrid.SelectionChanged -= RevisionGrid_SelectionChanged;
        fileStatusList.SelectedIndexChanged -= FileStatusList_SelectedIndexChanged;
        workingTreePanel.SelectionChanged -= WorkingTreePanel_SelectionChanged;
        fileViewer.SelectionChanged -= FileViewer_SelectionChanged;
        fileListTabs.SelectionChanged -= FileListTabs_SelectionChanged;
        repoObjectsTree.SelectionChanged -= RepoObjectsTree_SelectionChanged;
        Interlocked.Increment(ref _repositoryLoadVersion);
        _repositoryDataRequestSequence.Dispose();
        _revisionFilesRequestSequence.Dispose();
        _fileDiffRequestSequence.Dispose();
        _workingTreeStatusRequestSequence.Dispose();
        _workingTreeDiffRequestSequence.Dispose();
        _recentRepositoriesRequestSequence.Dispose();
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

    private async Task ManageRemotesAsync()
    {
        RemoteManagementWindow dialog = new(Module);
        await dialog.ShowDialog<bool>(this);
        if (dialog.HasChanges)
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task ManageSubmodulesAsync()
    {
        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        SubmoduleManagementWindow dialog = new(module, UICommands, OpenRepository);
        await dialog.ShowDialog<bool>(this);
        if (dialog.HasChanges && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task InitializeRepositoryAsync()
    {
        InitRepositoryWindow dialog = new(
            UICommands.GetRequiredService<IGitExecutorProvider>(),
            OpenRepository,
            Module.WorkingDir);
        await dialog.ShowDialog<bool>(this);
    }

    private async Task CloneRepositoryAsync()
    {
        CloneRepositoryWindow dialog = new(UICommands, source: null, repositoryCloned: path => OpenRepository(path));
        await dialog.ShowDialog<bool>(this);
    }

    private async Task ShowReflogAsync()
    {
        ReflogWindow dialog = new(Module);
        await dialog.ShowDialog<bool>(this);
        if (dialog.RequestedCommand is ArgumentString command)
        {
            RunRemoteCommand(command);
        }
    }

    private async Task ApplyPatchAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = GitUI.TranslatedStrings.ApplyPatchMenu,
        });

        if (files.Count == 0)
        {
            return;
        }

        IGitModule module = Module;
        long loadVersion = _repositoryLoadVersion;
        ArgumentString arguments;
        try
        {
            string patchPath = files[0].Path.LocalPath;
            PatchOptionsWindow optionsDialog = new(allowSignOff: !PatchWorkflow.IsDiffPatch(patchPath));
            PatchApplyOptions? patchOptions = await optionsDialog.ShowDialog<PatchApplyOptions?>(this);
            if (patchOptions is null)
            {
                return;
            }

            arguments = PatchWorkflow.BuildApply(module, patchPath, patchOptions);
        }
        catch (IOException)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.PatchFileReadError);
            return;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.PatchFileReadError);
            return;
        }

        UICommands.StartGitCommandProcessDialog(this, arguments);
        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            UICommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task FormatPatchAsync()
    {
        IGitModule module = Module;
        long repositoryLoadVersion = _repositoryLoadVersion;
        IReadOnlyList<GitRevision> revisions = [.. RevisionGrid.SelectedRevisions.Reverse()];
        if (revisions.Count == 0 || revisions.Any(revision => revision.ObjectId.IsArtificial))
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoRevision);
            return;
        }

        string previousOutputDirectory = AppSettings.LastFormatPatchDir;
        IStorageFolder? suggestedStartLocation = Directory.Exists(previousOutputDirectory)
            ? await StorageProvider.TryGetFolderFromPathAsync(previousOutputDirectory)
            : null;
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation,
            Title = GitUI.TranslatedStrings.FormatPatchMenu,
        });

        if (folders.Count == 0)
        {
            return;
        }

        if (folders[0].TryGetLocalPath() is not string outputDirectory)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.ChooseDirectory);
            return;
        }

        if (!Directory.Exists(outputDirectory))
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.ChooseDirectory);
            return;
        }

        if (!IsCurrentRepositoryLoad(module, repositoryLoadVersion))
        {
            return;
        }

        try
        {
            AppSettings.LastFormatPatchDir = outputDirectory;
            IReadOnlyList<ArgumentString> commands = PatchWorkflow.BuildFormatPatches(module, revisions, outputDirectory);
            string result = await Task.Run(() => string.Join(
                Environment.NewLine,
                commands.Select(command => module.GitExecutable.GetOutput(command))));

            if (string.IsNullOrWhiteSpace(result))
            {
                MessageBoxes.ShowError(this, GitUI.TranslatedStrings.NoResultsFound);
                return;
            }

            MessageBoxes.Show(this, result, GitUI.TranslatedStrings.FormatPatchMenu, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private async Task ViewPatchAsync()
    {
        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = GitUI.TranslatedStrings.ViewPatchMenu,
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            string patch = await Task.Run(() => PatchWorkflow.ReadPatchFile(files[0].Path.LocalPath));
            fileViewer.ViewPatch(patch);
        }
        catch (IOException)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.PatchFileReadError);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxes.ShowError(this, GitUI.TranslatedStrings.PatchFileReadError);
        }
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
        ThemeId themeId = AppSettings.ThemeId;
        bool followsSystem = themeId == ThemeId.WindowsAppColorModeId;
        bool isDark = themeId == ThemeId.DefaultDark;
        systemThemeMenuItem.IsChecked = followsSystem;
        darkThemeMenuItem.IsChecked = isDark;
        lightThemeMenuItem.IsChecked = !followsSystem && !isDark;
    }

    private enum RepositoryOperation
    {
        None,
        Unknown,
        Merge,
        Rebase,
        Patch,
        CherryPick,
        Revert,
        Bisect,
    }

    private readonly record struct RepositoryOperationState(RepositoryOperation Operation, bool HasUnresolvedConflicts)
    {
        public bool IsActive => Operation != RepositoryOperation.None || HasUnresolvedConflicts;
    }
}
