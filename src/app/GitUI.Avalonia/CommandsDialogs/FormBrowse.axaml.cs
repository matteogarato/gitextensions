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
using GitUI.Plugin;
using GitUI.ScriptsEngine;
using GitUI.UserControls;
using ResourceManager;
using Control = GitExtensions.Shims.WinForms.Control;

namespace GitUI.CommandsDialogs;

// Reduced shell: the read-only browse panels and first repository commands are functional;
// the remaining upstream menus and toolbars arrive with their corresponding dialogs.
public sealed partial class FormBrowse : GitModuleForm, IBrowseRepo
{
    private readonly CancellationTokenSequence _fileDiffRequestSequence = new();
    private readonly CancellationTokenSequence _recentRepositoriesRequestSequence = new();
    private readonly CancellationTokenSequence _repositoryDataRequestSequence = new();
    private readonly CancellationTokenSequence _revisionFilesRequestSequence = new();
    private readonly CancellationTokenSequence _workingTreeDiffRequestSequence = new();
    private readonly CancellationTokenSequence _workingTreeStatusRequestSequence = new();
    private IReadOnlyList<HotkeyCommand> _browseHotkeys = [];
    private bool _isClosed;
    private IGitModule? _lastRepositoryModule;
    private long _repositoryLoadVersion;
    private GitItemStatus? _workingTreeDiffItem;
    private bool _workingTreeOperationInProgress;

    public FormBrowse()
    {
        InitializeComponent();
    }

    public FormBrowse(IServiceProvider serviceProvider, GitModule module)
        : this(new GitUiCommands(serviceProvider, module))
    {
    }

    public FormBrowse(IGitUiCommands commands)
        : base(commands, true)
    {
        InitializeComponent();
        UiCommands.BrowseRepo = this;

        RevisionGrid.SelectionChanged += RevisionGrid_SelectionChanged;
        fileStatusList.SelectedIndexChanged += FileStatusList_SelectedIndexChanged;
        fileStatusList.FileHistoryRequested += (_, _) => OpenSelectedFileHistory(false);
        fileStatusList.PathActionRequested +=
            (_, action) => ThreadHelper.FileAndForget(() => HandleFilePathActionAsync(action));
        fileStatusList.BlameRequested += (_, _) => OpenSelectedFileHistory(true);
        workingTreePanel.SelectionChanged += WorkingTreePanel_SelectionChanged;
        workingTreePanel.StageRequested += (_, _) => ApplyWorkingTreeChanges(true);
        workingTreePanel.StageSelectedLinesRequested += (_, _) => StageSelectedLines(false);
        workingTreePanel.UnstageSelectedLinesRequested += (_, _) => StageSelectedLines(true);
        workingTreePanel.DiscardSelectedLinesRequested += (_, _) => DiscardSelectedLines();
        workingTreePanel.UnstageRequested += (_, _) => ApplyWorkingTreeChanges(false);
        workingTreePanel.DiscardRequested += (_, _) => DiscardSelectedChanges();
        fileViewer.SelectionChanged += FileViewer_SelectionChanged;
        _browseHotkeys = UiCommands.GetRequiredService<IHotkeySettingsLoader>()
            .LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName);
        AddHandler(KeyDownEvent, BrowseHotkeyKeyDown, RoutingStrategies.Tunnel);
        dashboardMenuItem.Header = ResourceManager.TranslatedStrings.RecentRepositoriesTitle;
        dashboardMenuItem.Click += (_, _) => ToggleDashboard();
        initializeRepositoryMenuItem.Header = TranslatedStrings.InitializeRepository;
        initializeRepositoryMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(InitializeRepositoryAsync);
        cloneRepositoryMenuItem.Header = TranslatedStrings.ForkCloneRepo;
        cloneRepositoryMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(CloneRepositoryAsync);
        gitMaintenanceToolStripMenuItem.Header = TranslatedStrings.GitMaintenanceMenu;
        compressGitDatabaseToolStripMenuItem.Header = TranslatedStrings.CompressGitDatabaseMenu;
        compressGitDatabaseToolStripMenuItem.Click += (_, _) => CompressGitDatabase();
        verifyRepositoryMenuItem.Header = TranslatedStrings.VerifyRepositoryMenu;
        verifyRepositoryMenuItem.Click += (_, _) => UiCommands.StartVerifyDatabaseDialog(this);
        deleteIndexLockToolStripMenuItem.Header = TranslatedStrings.DeleteIndexLockMenu;
        deleteIndexLockToolStripMenuItem.Click += (_, _) => DeleteIndexLock();
        cleanupRepositoryMenuItem.Header = TranslatedStrings.CleanupRepositoryMenu;
        cleanupRepositoryMenuItem.Click += (_, _) => UiCommands.StartCleanupRepositoryDialog(this);
        reflogMenuItem.Header = TranslatedStrings.ShowReflogMenu;
        reflogMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ShowReflogAsync);
        lblDashboardTitle.Text = ResourceManager.TranslatedStrings.RecentRepositoriesTitle;
        btnDashboardOpen.Content = ResourceManager.TranslatedStrings.OpenSelectedRepository;
        btnDashboardOpenFolder.Content = TranslatedStrings.Open;
        btnDashboardRemove.Content = ResourceManager.TranslatedStrings.RemoveFromRecentList;
        btnDashboardClear.Content = ResourceManager.TranslatedStrings.ClearRecentRepositories;
        btnDashboardReturn.Content = ResourceManager.TranslatedStrings.ReturnToRepository;
        lstRecentRepositories.ItemTemplate = new FuncDataTemplate<Repository>(
            (repository, _) => new TextBlock { Text = repository.Path, TextTrimming = TextTrimming.CharacterEllipsis },
            false);
        lstRecentRepositories.SelectionChanged += (_, _) => UpdateRecentRepositoryActionState();
        lstRecentRepositories.DoubleTapped += (_, _) => OpenSelectedRecentRepository();
        btnDashboardOpen.Click += (_, _) => OpenSelectedRecentRepository();
        btnDashboardOpenFolder.Click += OpenToolStripMenuItemClick;
        btnDashboardRemove.Click += (_, _) => RemoveSelectedRecentRepository();
        btnDashboardClear.Click += (_, _) => ClearRecentRepositories();
        btnDashboardReturn.Click += (_, _) => SetDashboardVisible(
            false,
            Module.IsValidGitWorkingDir());
        resetAllMenuItem.Header = TranslatedStrings.ResetAllChangesMenu;
        resetAllMenuItem.Click += (_, _) => ResetAllChanges();
        goToCommitMenuItem.Header = TranslatedStrings.GoToCommitMenu;
        goToCommitMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(GoToCommitAsync);
        goToSuperprojectMenuItem.Header = TranslatedStrings.GoToSuperprojectMenu;
        goToSuperprojectMenuItem.Click += (_, _) => GoToSuperproject();
        commitMenuItem.Header = TranslatedStrings.CommitMenu;
        commitMenuItem.Click += (_, _) => ShowCommitPanel();
        undoLastCommitMenuItem.Header = TranslatedStrings.UndoLastCommitMenu;
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
        stashChangesMenuItem.Header = TranslatedStrings.StashChangesMenu;
        stashChangesMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(() => StashChangesAsync());
        stashStagedMenuItem.Header = TranslatedStrings.StashStagedMenu;
        stashStagedMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(StashStagedAsync);
        stashManagerMenuItem.Header = TranslatedStrings.StashManagerMenu;
        stashManagerMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ShowStashManagerAsync);
        stashOptionsMenuItem.Header = TranslatedStrings.StashOptionsMenu;
        stashOptionsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ConfigureStashOptionsAsync);
        pullMergeMenuItem.Header = TranslatedStrings.PullMergeMenu;
        pullRebaseMenuItem.Header = TranslatedStrings.PullRebaseMenu;
        pushMenuItem.Header = TranslatedStrings.PushMenu;
        forcePushMenuItem.Header = TranslatedStrings.ForcePushMenu;
        pullMergeMenuItem.Click += (_, _) => PullCurrentBranch(false);
        pullRebaseMenuItem.Click += (_, _) => PullCurrentBranch(true);
        pushMenuItem.Click += (_, _) => PushCurrentBranch(ForcePushOptions.DoNotForce);
        forcePushMenuItem.Click += (_, _) => PushCurrentBranch(ForcePushOptions.ForceWithLease);
        pullOptionsMenuItem.Header = TranslatedStrings.PullOptionsTitle + "...";
        pushOptionsMenuItem.Header = TranslatedStrings.PushOptionsTitle + "...";
        pullOptionsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(PullWithOptionsAsync);
        pushOptionsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(PushWithOptionsAsync);
        formatPatchMenuItem.Header = TranslatedStrings.FormatPatchMenu;
        formatPatchMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(FormatPatchAsync);
        applyPatchMenuItem.Header = TranslatedStrings.ApplyPatchMenu;
        applyPatchMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ApplyPatchAsync);
        viewPatchMenuItem.Header = TranslatedStrings.ViewPatchMenu;
        viewPatchMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ViewPatchAsync);
        openToolStripMenuItem.Click += OpenToolStripMenuItemClick;
        refreshToolStripMenuItem.Click += RefreshToolStripMenuItemClick;
        remoteManagementMenuItem.Header = TranslatedStrings.RemoteManagementMenu;
        remoteManagementMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ManageRemotesAsync);
        submoduleManagementMenuItem.Header = TranslatedStrings.Submodules;
        submoduleManagementMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ManageSubmodulesAsync);
        pluginsMenuItem.Header = TranslatedStrings.PluginsMenu;
        pluginsMenuItem.PointerEntered += (_, _) => RefreshRepositoryPluginMenu();
        fetchAllToolStripMenuItem.Click += fetchAllToolStripMenuItem_Click;
        viewToolStripMenuItem.Header = AvaloniaTranslationUtils.ToAvaloniaMnemonics(TranslatedStrings.View);
        lightThemeMenuItem.Header = TranslatedStrings.LightTheme;
        darkThemeMenuItem.Header = TranslatedStrings.DarkTheme;
        systemThemeMenuItem.Header = TranslatedStrings.SystemTheme;
        commandLogMenuItem.Header = TranslatedStrings.CommandLogTitle;
        commandLogMenuItem.Click += (_, _) => CommandLogWindow.ShowOrActivate(this);
        keyboardShortcutsMenuItem.Header = ResourceManager.TranslatedStrings.KeyboardShortcutsMenu;
        keyboardShortcutsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ConfigureHotkeysAsync);
        diffToolWorkingTreeMenuItem.Header = TranslatedStrings.DiffToolWorkingTreeMenu;
        diffToolWorkingTreeMenuItem.Click += (_, _) => OpenDiffTool(GitRevision.IndexGuid, GitRevision.WorkTreeGuid);
        settingsMenuItem.Header = TranslatedStrings.SettingsMenu;
        settingsMenuItem.Click += (_, _) => ThreadHelper.FileAndForget(ShowSettingsAsync);
        userScriptsMenuItem.Header = ResourceManager.TranslatedStrings.ScriptsMenu;
        ReloadUserScriptsMenu();
        lightThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.DefaultLight);
        darkThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.DefaultDark);
        systemThemeMenuItem.Click += (_, _) => SetTheme(ThemeId.WindowsAppColorModeId);
        btnContinueRebase.Content = TranslatedStrings.ContinueRebase;
        btnEditRebaseTodo.Content = TranslatedStrings.EditRebaseTodo;
        btnContinueMerge.Content = TranslatedStrings.ButtonContinue;
        btnContinuePatch.Content = TranslatedStrings.ButtonContinue;
        btnResolveConflicts.Content = TranslatedStrings.ResolveMergeConflicts;
        btnSkipRebase.Content = TranslatedStrings.SkipRebase;
        btnAbortRebase.Content = TranslatedStrings.AbortRebase;
        btnAbortMerge.Content = TranslatedStrings.AbortMerge;
        btnAbortPatch.Content = TranslatedStrings.AbortPatch;
        btnContinueCommitPick.Content = TranslatedStrings.ButtonContinue;
        btnAbortCommitPick.Content = TranslatedStrings.AbortOperation;
        btnContinueRebase.Click += (_, _) =>
            RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.ContinueRebase(), true);
        btnEditRebaseTodo.Click += (_, _) =>
            RunOperationRecoveryCommand(RepositoryOperation.Rebase, RebaseWorkflow.BuildEditTodo());
        btnContinueMerge.Click += (_, _) =>
            RunOperationRecoveryCommand(RepositoryOperation.Merge, Commands.ContinueMerge(), true);
        btnContinuePatch.Click += (_, _) =>
            RunOperationRecoveryCommand(RepositoryOperation.Patch, Commands.Resolved(), true);
        btnResolveConflicts.Click += (_, _) => ResolveConflicts();
        btnSkipRebase.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.SkipRebase());
        btnAbortRebase.Click +=
            (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Rebase, Commands.AbortRebase());
        btnAbortMerge.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Merge, Commands.AbortMerge());
        btnAbortPatch.Click += (_, _) => RunOperationRecoveryCommand(RepositoryOperation.Patch, Commands.Abort());
        btnContinueCommitPick.Click += (_, _) => ContinueCommitPick();
        btnAbortCommitPick.Click += (_, _) => AbortCommitPick();
        UiCommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        Closed += FormBrowse_Closed;

        ReloadRepository();

        InitializeComplete();
    }

    GitRevision? IBrowseRepo.GetLatestSelectedRevision()
    {
        return RevisionGrid.SelectedRevision;
    }

    IReadOnlyList<GitRevision> IBrowseRepo.GetSelectedRevisions()
    {
        return RevisionGrid.SelectedRevisions;
    }

    Point IBrowseRepo.GetQuickItemSelectorLocation()
    {
        return new Point(0, 0);
    }

    void IBrowseRepo.GoToRef(string refName, bool showNoRevisionMsg, bool toggleSelection = false)
    {
        if (RevisionGrid.GoToRef(refName))
        {
            return;
        }

        if (showNoRevisionMsg)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.NoResultsFound);
        }
    }

    void IBrowseRepo.SetWorkingDir(string? path, ObjectId selectedId = default, ObjectId firstId = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            UiCommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
            if (PluginRegistry.IsInitialized)
            {
                PluginRegistry.Unregister(UiCommands, this);
            }

            UiCommands = UiCommands.WithWorkingDirectory(null);
            UiCommands.BrowseRepo = this;
            UiCommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
            if (PluginRegistry.IsInitialized)
            {
                PluginRegistry.Register(UiCommands, this);
            }

            ReloadRepository();
            return;
        }

        var revisionToSelect = selectedId.IsZero ? firstId : selectedId;
        if (OpenRepository(path) && !revisionToSelect.IsZero)
        {
            RevisionGrid.SelectRevision(revisionToSelect);
        }
    }

    private void ReloadUserScriptsMenu()
    {
        userScriptsMenuItem.Items.Clear();

        List<ScriptInfo> scripts =
        [
            .. UiCommands.GetRequiredService<IScriptsManager>()
                .GetScripts()
                .Where(script => script.Enabled && script.OnEvent == ScriptEvent.ShowInUserMenuBar)
        ];

        foreach (var script in scripts)
        {
            MenuItem menuItem = new()
            {
                Header = string.IsNullOrWhiteSpace(script.Name)
                    ? TranslatedStrings.ScriptText
                    : script.GetDisplayName(),
                IsEnabled = !string.IsNullOrWhiteSpace(script.Command)
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
            PluginRegistry.Register(UiCommands, this);
        }

        UiCommands.GetRequiredService<IScriptsRunner>()
            .RunScript(script, this, UiCommands, ScriptOptionsProviderBase.Default);
    }

    private static bool IsPluginCommand(string? command)
    {
        return command is not null
               && (command.StartsWith("plugin:", StringComparison.OrdinalIgnoreCase)
                   || command.StartsWith("{plugin", StringComparison.OrdinalIgnoreCase));
    }

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
            foreach (var plugin in PluginRegistry.RepositoryCommandPlugins)
            {
                MenuItem pluginMenuItem = new() { Header = plugin.Name };
                foreach (var command in plugin.Commands)
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

                    MenuItem commandMenuItem = new() { Header = command.Name, IsEnabled = canExecute };
                    commandMenuItem.Click += (_, _) =>
                        ThreadHelper.FileAndForget(() => ExecuteRepositoryPluginCommandAsync(command));
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
        UiCommands.StartCommandLineProcessDialog(this, Commands.GarbageCollect());
    }

    private void DeleteIndexLock()
    {
        try
        {
            Module.UnlockIndex(true);
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
            await RepositoryPluginCommandRunner.ExecuteAsync(UiCommands, command);
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

        var module = Module;
        var loadVersion = Interlocked.Increment(ref _repositoryLoadVersion);
        var dataRequestCancellationToken = _repositoryDataRequestSequence.Next();
        _workingTreeStatusRequestSequence.Next();
        _revisionFilesRequestSequence.Next();
        _fileDiffRequestSequence.Next();
        _workingTreeDiffRequestSequence.Next();
        _recentRepositoriesRequestSequence.Next();
        SetOperationStatus(default);

        var repositoryChanged = !ReferenceEquals(_lastRepositoryModule, module);
        if (repositoryChanged)
        {
            _lastRepositoryModule = module;
            _workingTreeOperationInProgress = false;
            workingTreePanel.SetOperationInProgress(false);
        }

        var isValidWorkingDir = module.IsValidGitWorkingDir();
        var appTitleGenerator = UiCommands.GetRequiredService<IAppTitleGenerator>();
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
            SetDashboardVisible(false, isValidWorkingDir);
            lblRepoPath.Text = module.WorkingDir;
            lblStatus.Text = TranslatedStrings.LoadingData;
            LoadCommitOptions(module, loadVersion, repositoryChanged);
            RevisionGrid.ReloadRevisions(module);
            repoObjectsTree.SetRefs([]);
            commitFilesTab.Header = TranslatedStrings.CommitFiles;
            revisionTreeTab.Header = TranslatedStrings.FileTree;
            workingTreeTab.Header = TranslatedStrings.WorkingTree;
            ThreadHelper.FileAndForget(async () =>
            {
                try
                {
                    var branchName = module.GetSelectedBranch();
                    var refs = module.GetRefs(RefsFilter.NoFilter);
                    var stashes = module.GetStashes(true);

                    if (dataRequestCancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(dataRequestCancellationToken);
                    if (!IsCurrentRepositoryLoad(module, loadVersion))
                    {
                        return;
                    }

                    foreach (var gitRef in refs.Where(gitRef => gitRef.IsHead))
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
            SetDashboardVisible(true, isValidWorkingDir);
            lblRepoPath.Text = module.WorkingDir ?? string.Empty;
            lblStatus.Text = string.IsNullOrWhiteSpace(module.WorkingDir)
                ? string.Empty
                : TranslatedStrings.DirectoryInvalidRepository;
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

        var workingDir = GitModule.TryFindGitWorkingDir(path);
        if (workingDir is null)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.DirectoryInvalidRepository);
            return false;
        }

        UiCommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
        if (PluginRegistry.IsInitialized)
        {
            PluginRegistry.Unregister(UiCommands, this);
        }

        UiCommands = UiCommands.WithWorkingDirectory(workingDir);
        UiCommands.BrowseRepo = this;
        UiCommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        if (PluginRegistry.IsInitialized)
        {
            PluginRegistry.Register(UiCommands, this);
        }

        ReloadRepository();
        RecordRecentRepository(workingDir);
        return true;
    }

    private void ToggleDashboard()
    {
        var isValidWorkingDir = Module.IsValidGitWorkingDir();
        SetDashboardVisible(!dashboardPanel.IsVisible || !isValidWorkingDir, isValidWorkingDir);
    }

    private void BrowseHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        if (!AvaloniaHotkeyKeyConverter.TryCreateKeyData(e.Key, e.KeyModifiers, out var keyData))
        {
            return;
        }

        var hotkey = _browseHotkeys.FirstOrDefault(command => command.KeyData == keyData);
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
                PullCurrentBranch(false);
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
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task ConfigureHotkeysAsync()
    {
        var settingsManager = UiCommands.GetRequiredService<AvaloniaHotkeySettingsManager>();
        HotkeySettingsWindow dialog = new(settingsManager);
        var saved = await dialog.ShowDialog<bool>(this);
        if (saved)
        {
            _browseHotkeys = settingsManager.LoadHotkeys(AvaloniaHotkeySettingsManager.BrowseSettingsName);
        }
    }

    private void SetDashboardVisible(bool isVisible, bool isValidWorkingDir)
    {
        var showDashboard = isVisible || !isValidWorkingDir;
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
                TranslatedStrings.UndoLastCommitConfirmation,
                TranslatedStrings.UndoLastCommitCaption,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        if (UiCommands.StartGitCommandProcessDialog(this, ResetWorkflow.BuildUndoLastCommit())
            && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private void LoadRecentRepositories()
    {
        var cancellationToken = _recentRepositoriesRequestSequence.Next();
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
                lblDashboardStatus.Text = repositories.Count == 0 ? TranslatedStrings.NoResultsFound : string.Empty;
                UpdateRecentRepositoryActionState();
            }
        });
    }

    private void UpdateRecentRepositoryActionState()
    {
        var hasSelection = lstRecentRepositories.SelectedItem is Repository;
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
            ThreadHelper.JoinableTaskFactory.Run(() => RepositoryHistoryManager.Locals.AddAsMostRecentAsync(path));
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

        var cancellationToken = _workingTreeStatusRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                var status = module.GetAllChangedFilesWithSubmodulesStatus(cancellationToken);
                var operationState = GetRepositoryOperationState(module);
                var statusText = FormatWorkingTreeStatus(status);

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
        var isRebaseInProgress = module.InTheMiddleOfRebase();
        var isMergeInProgress = module.InTheMiddleOfMerge();
        var isPatchInProgress = module.InTheMiddleOfPatch();
        var isBisectInProgress = module.InTheMiddleOfBisect();
        var isCherryPickInProgress = File.Exists(module.ResolveGitInternalPath("CHERRY_PICK_HEAD"));
        var isRevertInProgress = File.Exists(module.ResolveGitInternalPath("REVERT_HEAD"));
        var hasUnresolvedConflicts = module.InTheMiddleOfConflictedMerge();
        var operation = isRebaseInProgress ? RepositoryOperation.Rebase
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
        btnContinueCommitPick.IsVisible =
            state.Operation is RepositoryOperation.CherryPick or RepositoryOperation.Revert
            && !state.HasUnresolvedConflicts;
        btnSkipRebase.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnAbortRebase.IsVisible = state.Operation == RepositoryOperation.Rebase;
        btnAbortMerge.IsVisible = state.Operation == RepositoryOperation.Merge;
        btnAbortPatch.IsVisible = state.Operation == RepositoryOperation.Patch;
        btnAbortCommitPick.IsVisible = state.Operation is RepositoryOperation.CherryPick or RepositoryOperation.Revert;

        var operationText = state.Operation switch
        {
            RepositoryOperation.Rebase => TranslatedStrings.RebaseInProgress,
            RepositoryOperation.Merge => TranslatedStrings.MergeInProgress,
            RepositoryOperation.Patch => TranslatedStrings.PatchInProgress,
            RepositoryOperation.CherryPick => TranslatedStrings.CherryPickInProgress,
            RepositoryOperation.Revert => TranslatedStrings.RevertInProgress,
            RepositoryOperation.Bisect => TranslatedStrings.BisectInProgress,
            RepositoryOperation.Unknown when state.HasUnresolvedConflicts => TranslatedStrings
                .UnrecognizedConflictOperation,
            RepositoryOperation.Unknown => TranslatedStrings.UnrecognizedOperationInProgress,
            _ => string.Empty
        };

        lblOperationStatus.Text = AppendConflictStatus(operationText);

        return;

        string AppendConflictStatus(string text)
        {
            return state.HasUnresolvedConflicts
                ? $"{text}{Environment.NewLine}{TranslatedStrings.UnresolvedConflicts}"
                : text;
        }
    }

    private void ResolveConflicts()
    {
        if (UiCommands.StartResolveConflictsDialog(this))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private void ContinueCommitPick()
    {
        RunCommitPickRecoveryCommand(true);
    }

    private void AbortCommitPick()
    {
        RunCommitPickRecoveryCommand(false);
    }

    private void RunCommitPickRecoveryCommand(bool continueOperation)
    {
        var module = Module;
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
            _ => null
        };
        if (command is not { } arguments)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.RecoveryActionStateChanged);
            UiCommands.RepoChangedNotifier.Notify();
            return;
        }

        RunOperationRecoveryCommand(state.Operation, arguments, continueOperation);
    }

    private void RunOperationRecoveryCommand(RepositoryOperation expectedOperation, ArgumentString arguments,
        bool requireResolvedConflicts = false)
    {
        var module = Module;
        var loadVersion = _repositoryLoadVersion;
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
            MessageBoxes.ShowError(this, TranslatedStrings.RecoveryActionStateChanged);
            UiCommands.RepoChangedNotifier.Notify();
            return;
        }

        UiCommands.StartGitCommandProcessDialog(this, arguments);

        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task PullWithOptionsAsync()
    {
        var module = Module;
        var remotes = module.GetRemoteNames();
        if (remotes.Count == 0)
        {
            MessageBoxes.ShowError(this, RemoteWorkflow.Validate(module, false) ?? TranslatedStrings.RemoteLabel);
            return;
        }

        PullOptionsWindow dialog = new(remotes, RemoteWorkflow.ResolveRemote(module), false);
        var options = await dialog.ShowDialog<PullOptions?>(this);
        if (options is null)
        {
            return;
        }

        var fetchOnly = options.Mode == PullMode.FetchOnly;
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
        var module = Module;
        var remotes = module.GetRemoteNames();
        if (remotes.Count == 0)
        {
            MessageBoxes.ShowError(this, RemoteWorkflow.Validate(module, false) ?? TranslatedStrings.RemoteLabel);
            return;
        }

        PushOptionsWindow dialog = new(remotes, RemoteWorkflow.ResolveRemote(module), module.GetSelectedBranch(true),
            false);
        var options = await dialog.ShowDialog<PushOptions?>(this);
        if (options is null)
        {
            return;
        }

        if (options.Scope == PushScope.CurrentBranch && string.IsNullOrEmpty(module.GetSelectedBranch(true)))
        {
            MessageBoxes.ShowError(this, RemoteWorkflow.Validate(module, true) ?? TranslatedStrings.PushOptionsTitle);
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
        var conflicted = rebase
            ? module.InTheMiddleOfRebase() && module.InTheMiddleOfConflictedMerge()
            : module.InTheMiddleOfConflictedMerge();
        if (conflicted)
        {
            UiCommands.StartResolveConflictsDialog(this);
        }
    }

    private void PullCurrentBranch(bool rebase)
    {
        var module = Module;
        if (!TryResolveRemote(module, false, out var remote))
        {
            return;
        }

        RunRemoteCommand(RemoteWorkflow.BuildPull(module, remote, rebase), ScriptEvent.BeforePull,
            ScriptEvent.AfterPull);
    }

    private void PushCurrentBranch(ForcePushOptions force)
    {
        var module = Module;
        if (!TryResolveRemote(module, true, out var remote))
        {
            return;
        }

        if (force != ForcePushOptions.DoNotForce
            && !ConfirmForcePush(module, remote))
        {
            return;
        }

        RunRemoteCommand(RemoteWorkflow.BuildPush(module, remote, force), ScriptEvent.BeforePush,
            ScriptEvent.AfterPush);
    }

    private bool ConfirmForcePush(IGitModule module, string remote)
    {
        return MessageBoxes.Show(
            this,
            string.Format(TranslatedStrings.ConfirmForcePush, module.GetSelectedBranch(true), remote),
            TranslatedStrings.ForcePushMenu,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private bool TryResolveRemote(IGitModule module, bool requiresBranch, out string remote)
    {
        remote = RemoteWorkflow.ResolveRemote(module) ?? string.Empty;
        var error = RemoteWorkflow.Validate(module, requiresBranch);
        if (error is not null)
        {
            lblStatus.Text = error;
            MessageBoxes.ShowError(this, error);
            return false;
        }

        return true;
    }

    private bool RunRemoteCommand(ArgumentString arguments, ScriptEvent beforeEvent = ScriptEvent.None,
        ScriptEvent afterEvent = ScriptEvent.None)
    {
        return RunWithLifecycleScripts(beforeEvent, afterEvent,
            () => UiCommands.StartGitCommandProcessDialog(this, arguments));
    }

    // After scripts run only on success, matching the WinForms behaviour; a failing before script cancels the command.
    private bool RunWithLifecycleScripts(ScriptEvent beforeEvent, ScriptEvent afterEvent, Func<bool> run)
    {
        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        var scriptsRunner = beforeEvent == ScriptEvent.None && afterEvent == ScriptEvent.None
            ? null
            : UiCommands.GetRequiredService<IScriptsRunner>();
        if (beforeEvent != ScriptEvent.None && scriptsRunner?.RunEventScripts(beforeEvent, this) == false)
        {
            return false;
        }

        var succeeded = run();
        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }

        if (succeeded && afterEvent != ScriptEvent.None)
        {
            scriptsRunner?.RunEventScripts(afterEvent, this);
        }

        return succeeded;
    }

    private void InitializeBranchContextMenu()
    {
        MenuItem checkoutItem = new() { Header = TranslatedStrings.CheckoutBranchMenu };
        MenuItem checkoutWithMergeItem = new() { Header = TranslatedStrings.CheckoutBranchMergeChangesMenu };
        MenuItem checkoutWithStashItem = new() { Header = TranslatedStrings.CheckoutBranchStashChangesMenu };
        MenuItem checkoutWithResetItem = new() { Header = TranslatedStrings.CheckoutBranchDiscardChangesMenu };
        MenuItem createItem = new() { Header = TranslatedStrings.CreateBranchMenu };
        MenuItem createWithoutCheckoutItem = new() { Header = TranslatedStrings.CreateBranchWithoutCheckoutMenu };
        MenuItem createOrphanItem = new() { Header = TranslatedStrings.CreateOrphanBranchMenu };
        MenuItem renameItem = new() { Header = TranslatedStrings.RenameBranchMenu };
        MenuItem deleteItem = new() { Header = TranslatedStrings.DeleteBranchMenu };
        MenuItem deleteRemoteBranchItem = new() { Header = TranslatedStrings.DeleteRemoteBranchMenu };
        MenuItem forceDeleteItem = new() { Header = TranslatedStrings.ForceDeleteBranchMenu };
        MenuItem mergeItem = new() { Header = TranslatedStrings.MergeBranchMenu };
        MenuItem rebaseItem = new() { Header = TranslatedStrings.RebaseOntoMenu };
        MenuItem createTagItem = new() { Header = TranslatedStrings.CreateTagMenu };
        MenuItem deleteTagItem = new() { Header = TranslatedStrings.DeleteTagMenu };
        MenuItem stashApplyItem = new() { Header = TranslatedStrings.StashApplyMenu };
        MenuItem stashPopItem = new() { Header = TranslatedStrings.StashPopMenu };
        MenuItem stashDropItem = new() { Header = TranslatedStrings.StashDropMenu };
        stashApplyItem.Click += (_, _) => RunSelectedStash(StashWorkflow.BuildApply);
        stashPopItem.Click += (_, _) => RunSelectedStash(stash => StashWorkflow.BuildPop(stash));
        stashDropItem.Click += (_, _) => DropSelectedStash();
        checkoutItem.Click += (_, _) => CheckoutSelectedRef();
        checkoutWithMergeItem.Click += (_, _) => CheckoutSelectedRef(LocalChangesAction.Merge);
        checkoutWithStashItem.Click += (_, _) => CheckoutSelectedRefWithStash();
        checkoutWithResetItem.Click += (_, _) => CheckoutSelectedRefWithReset();
        createItem.Click += (_, _) => ThreadHelper.FileAndForget(() => CreateBranchAtSelectionAsync(true));
        createWithoutCheckoutItem.Click +=
            (_, _) => ThreadHelper.FileAndForget(() => CreateBranchAtSelectionAsync(false));
        createOrphanItem.Click += (_, _) => ThreadHelper.FileAndForget(CreateOrphanBranchAtSelectionAsync);
        renameItem.Click += (_, _) => ThreadHelper.FileAndForget(RenameSelectedBranchAsync);
        deleteItem.Click += (_, _) => DeleteSelectedBranch();
        deleteRemoteBranchItem.Click += (_, _) => DeleteSelectedRemoteBranch();
        forceDeleteItem.Click += (_, _) => ForceDeleteSelectedBranch();
        mergeItem.Click += (_, _) => ThreadHelper.FileAndForget(MergeSelectedBranchAsync);
        rebaseItem.Click += (_, _) => ThreadHelper.FileAndForget(RebaseSelectedBranchAsync);
        createTagItem.Click += (_, _) => ThreadHelper.FileAndForget(CreateTagAtSelectedRefAsync);
        deleteTagItem.Click += (_, _) => DeleteSelectedTag();

        ContextMenu menu = new()
        {
            ItemsSource = new[]
            {
                checkoutItem, checkoutWithMergeItem, checkoutWithStashItem, checkoutWithResetItem, createItem,
                createWithoutCheckoutItem, createOrphanItem, renameItem, deleteItem, deleteRemoteBranchItem,
                forceDeleteItem, mergeItem, rebaseItem, createTagItem, deleteTagItem, stashApplyItem,
                stashPopItem, stashDropItem
            }
        };
        AvaloniaTranslationUtils.NormalizeMenuMnemonics(menu);
        menu.Opening += (_, _) =>
        {
            var selectedRef = repoObjectsTree.SelectedRef;
            checkoutItem.IsEnabled = BranchWorkflow.CanCheckout(selectedRef);
            checkoutWithMergeItem.IsEnabled = checkoutItem.IsEnabled;
            checkoutWithStashItem.IsEnabled = checkoutItem.IsEnabled;
            checkoutWithResetItem.IsEnabled = checkoutItem.IsEnabled;
            renameItem.IsEnabled = BranchWorkflow.CanRename(selectedRef);
            deleteItem.IsEnabled = BranchWorkflow.CanDelete(selectedRef);
            deleteRemoteBranchItem.IsEnabled = BranchWorkflow.CanDeleteRemoteBranch(selectedRef);
            forceDeleteItem.IsEnabled = BranchWorkflow.CanForceDelete(selectedRef);
            mergeItem.IsEnabled = MergeWorkflow.CanMerge(selectedRef, Module.GetSelectedBranch(true));
            rebaseItem.IsEnabled = RebaseWorkflow.Validate(Module, selectedRef?.Name) is null &&
                                   selectedRef is { IsTag: false };
            createTagItem.IsEnabled = selectedRef is not null && TagWorkflow.CanCreateTag(selectedRef.ObjectId);
            deleteTagItem.IsEnabled = TagWorkflow.CanDeleteTag(selectedRef);
            var stashSelected = repoObjectsTree.SelectedStash is not null;
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
        var source = repoObjectsTree.SelectedRef;
        if (source is null || !MergeWorkflow.CanMerge(source, Module.GetSelectedBranch(true)))
        {
            return;
        }

        MergeOptionsWindow dialog = new(source.Name);
        var options = await dialog.ShowDialog<MergeOptions?>(this);
        if (options is not { } mergeOptions)
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        var messageFile = MergeWorkflow.WriteMessageFile(mergeOptions.Message);
        try
        {
            RunWithLifecycleScripts(
                ScriptEvent.BeforeMerge,
                ScriptEvent.AfterMerge,
                () => UiCommands.StartGitCommandProcessDialog(this,
                    MergeWorkflow.BuildMerge(module, source, mergeOptions, messageFile)));
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

        UiCommands.RepoChangedNotifier.Notify();
        if (module.InTheMiddleOfConflictedMerge())
        {
            UiCommands.StartResolveConflictsDialog(this);
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task RebaseSelectedBranchAsync()
    {
        var source = repoObjectsTree.SelectedRef;
        var module = Module;
        var validationError = RebaseWorkflow.Validate(module, source?.Name);
        if (source is null || source.IsTag || validationError is not null)
        {
            if (validationError is not null)
            {
                MessageBoxes.ShowError(this, validationError);
            }

            return;
        }

        RebaseOptionsWindow dialog = new(source.Name, AppSettings.RebaseAutoStash,
            module.GetEffectiveSetting<bool>("rebase.autosquash") is true);
        var configuration = await dialog.ShowDialog<RebaseConfiguration?>(this);
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
        var loadVersion = _repositoryLoadVersion;
        string? todoPath = null;
        var rebaseCommand = RebaseWorkflow.BuildRebase(configuration);
        if (configuration.Interactive)
        {
            var commits =
                InteractiveRebaseWorkflow.ListCommits(module, InteractiveRebaseWorkflow.GetRange(configuration));
            if (commits.Count > 0)
            {
                var edited =
                    await new InteractiveRebaseWindow(commits).ShowDialog<IReadOnlyList<RebaseTodoEntry>?>(this);
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
            UiCommands.StartGitCommandProcessDialog(this, rebaseCommand);
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

        UiCommands.RepoChangedNotifier.Notify();
        if (module.InTheMiddleOfRebase() && module.InTheMiddleOfConflictedMerge())
        {
            UiCommands.StartResolveConflictsDialog(this);
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    // Conflicts are left in the working tree; the operation-in-progress banner offers continue/abort.
    private void InitializeRevisionContextMenu()
    {
        MenuItem checkoutRevisionItem = new() { Header = TranslatedStrings.CheckoutRevisionMenu };
        MenuItem resetToRevisionItem = new() { Header = TranslatedStrings.ResetCurrentBranchToRevisionMenu };
        MenuItem resetSoftItem = new() { Header = TranslatedStrings.ResetSoftToRevisionMenu };
        MenuItem resetMixedItem = new() { Header = TranslatedStrings.ResetMixedToRevisionMenu };
        MenuItem resetKeepItem = new() { Header = TranslatedStrings.ResetKeepToRevisionMenu };
        MenuItem resetMergeItem = new() { Header = TranslatedStrings.ResetMergeToRevisionMenu };
        MenuItem resetHardItem = new() { Header = TranslatedStrings.ResetHardToRevisionMenu };
        MenuItem cherryPickItem = new() { Header = TranslatedStrings.CherryPickMenu };
        MenuItem revertItem = new() { Header = TranslatedStrings.RevertCommitMenu };
        MenuItem diffToolItem = new() { Header = TranslatedStrings.DiffToolRevisionsMenu };
        MenuItem compareItem = new() { Header = TranslatedStrings.CompareRevisionsMenu };
        MenuItem compareToHeadItem = new() { Header = TranslatedStrings.CompareToHeadMenu };
        MenuItem copyMenuItem = new() { Header = TranslatedStrings.CopyMenu };
        copyMenuItem.ItemsSource = new[]
        {
            CreateCopyItem(TranslatedStrings.CopyFullHashMenu, RevisionCopyKind.FullHash),
            CreateCopyItem(TranslatedStrings.CopyShortHashMenu, RevisionCopyKind.ShortHash),
            CreateCopyItem(TranslatedStrings.CopySubjectMenu, RevisionCopyKind.Subject),
            CreateCopyItem(TranslatedStrings.CopyHashAndSubjectMenu, RevisionCopyKind.HashAndSubject),
            CreateCopyItem(TranslatedStrings.CopyAuthorMenu, RevisionCopyKind.Author)
        };
        compareToHeadItem.Click += (_, _) => CompareSelectedWithHead();
        MenuItem cherryPickOriginItem = new() { Header = TranslatedStrings.CherryPickRecordOriginMenu };
        MenuItem revertNoCommitItem = new() { Header = TranslatedStrings.RevertNoCommitMenu };
        MenuItem cherryPickSelectedItem = new() { Header = TranslatedStrings.CherryPickSelectedMenu };
        MenuItem revertSelectedItem = new() { Header = TranslatedStrings.RevertSelectedMenu };
        MenuItem createTagItem = new() { Header = TranslatedStrings.CreateTagMenu };
        MenuItem archiveRevisionItem = new() { Header = TranslatedStrings.ArchiveRevisionMenu };
        checkoutRevisionItem.Click += (_, _) => CheckoutSelectedRevision();
        resetSoftItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Soft);
        resetMixedItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Mixed);
        resetKeepItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Keep);
        resetMergeItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Merge);
        resetHardItem.Click += (_, _) => ResetCurrentBranchToSelectedRevision(ResetMode.Hard);
        resetToRevisionItem.ItemsSource =
            new[] { resetSoftItem, resetMixedItem, resetKeepItem, resetMergeItem, resetHardItem };
        cherryPickItem.Click += (_, _) =>
            ThreadHelper.FileAndForget(() => RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildCherryPick));
        revertItem.Click += (_, _) =>
            ThreadHelper.FileAndForget(() => RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildRevert));
        diffToolItem.Click += (_, _) =>
        {
            if (ExternalToolWorkflow.ResolveComparison([.. RevisionGrid.SelectedRevisions]) is ({ } first, { } second))
            {
                OpenDiffTool(first, second);
            }
        };
        compareItem.Click += (_, _) => ShowCompareRevisions();
        cherryPickOriginItem.Click += (_, _) => ThreadHelper.FileAndForget(() =>
            RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildCherryPickRecordingOrigin));
        revertNoCommitItem.Click += (_, _) =>
            ThreadHelper.FileAndForget(() =>
                RunSelectedRevisionCommandAsync(CommitPickWorkflow.BuildRevertWithoutCommit));
        cherryPickSelectedItem.Click += (_, _) =>
            RunSelectedRevisionsSequence(revisions => CommitPickWorkflow.BuildCherryPickSequence(revisions, false));
        revertSelectedItem.Click += (_, _) =>
            RunSelectedRevisionsSequence(revisions => CommitPickWorkflow.BuildRevertSequence(revisions, true));
        createTagItem.Click += (_, _) => ThreadHelper.FileAndForget(CreateTagAtSelectedRevisionAsync);
        archiveRevisionItem.Click += (_, _) => ArchiveSelectedRevisions();

        ContextMenu menu = new()
        {
            ItemsSource = new[]
            {
                checkoutRevisionItem, resetToRevisionItem, compareItem, compareToHeadItem, diffToolItem,
                copyMenuItem, cherryPickItem, cherryPickOriginItem, cherryPickSelectedItem, revertItem,
                revertNoCommitItem, revertSelectedItem, createTagItem, archiveRevisionItem
            }
        };
        AvaloniaTranslationUtils.NormalizeMenuMnemonics(menu);
        menu.Opening += (_, _) =>
        {
            checkoutRevisionItem.IsEnabled = BranchWorkflow.CanCheckoutRevision(RevisionGrid.SelectedRevision);
            resetToRevisionItem.IsEnabled = ResetWorkflow.CanResetTo(RevisionGrid.SelectedRevision);
            var canApply = CommitPickWorkflow.CanApply(RevisionGrid.SelectedRevision);
            var single0 = RevisionGrid.SelectedRevisions.Count == 1;
            compareItem.IsEnabled =
                ExternalToolWorkflow.ResolveComparison([.. RevisionGrid.SelectedRevisions]) is not null;
            compareToHeadItem.IsEnabled = single0 && !RevisionGrid.SelectedRevisions[0].IsArtificial;
            copyMenuItem.IsEnabled = RevisionGrid.SelectedRevisions.Count > 0;
            diffToolItem.IsEnabled =
                ExternalToolWorkflow.ResolveComparison([.. RevisionGrid.SelectedRevisions]) is not null;
            var single = RevisionGrid.SelectedRevisions.Count == 1;
            var canApplySequence =
                !single && CommitPickWorkflow.CanApplySequence(Module, RevisionGrid.SelectedRevisions);
            cherryPickItem.IsEnabled = canApply && single;
            cherryPickOriginItem.IsEnabled = canApply && single;
            revertItem.IsEnabled = canApply && single;
            revertNoCommitItem.IsEnabled = canApply && single;
            cherryPickSelectedItem.IsEnabled = canApplySequence;
            revertSelectedItem.IsEnabled = canApplySequence;
            createTagItem.IsEnabled = RevisionGrid.SelectedRevision is { } revision &&
                                      TagWorkflow.CanCreateTag(revision.ObjectId);
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
        var module = Module;
        var headId = module.GetCurrentCheckout();
        if (RevisionGrid.SelectedRevision is not { } selected || headId.IsZero)
        {
            return;
        }

        new CompareRevisionsWindow(module, selected, new GitRevision(headId)).Show(this);
    }

    private void ShowCompareRevisions()
    {
        IReadOnlyList<GitRevision> selection = [.. RevisionGrid.SelectedRevisions];
        if (ExternalToolWorkflow.ResolveComparison(selection) is not ({ } first, { } second))
        {
            return;
        }

        var older = selection.FirstOrDefault(revision => revision.ObjectId.ToString() == first);
        var newer = selection.First(revision => revision.ObjectId.ToString() == second);
        older ??= new GitRevision(newer.FirstParentId);
        new CompareRevisionsWindow(Module, older, newer).Show(this);
    }

    private void ArchiveSelectedRevisions()
    {
        var revisions = GetSelectedRevisions();
        if (revisions.Count > 2)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.SelectOnlyOneOrTwoRevisions);
            return;
        }

        if (revisions.Count == 0 || revisions.Any(revision => revision.ObjectId.IsArtificial))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.NoRevision);
            return;
        }

        UiCommands.StartArchiveDialog(this, revisions[0], revisions.Count == 2 ? revisions[1] : null);
    }

    private void ResetCurrentBranchToSelectedRevision(ResetMode mode)
    {
        if (RevisionGrid.SelectedRevision is not { } revision || !ResetWorkflow.CanResetTo(revision))
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        if (mode == ResetMode.Hard
            && MessageBoxes.Show(
                this,
                TranslatedStrings.ConfirmHardResetToRevision,
                TranslatedStrings.ResetHardToRevisionTitle,
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
        if (RevisionGrid.SelectedRevision is { } revision && BranchWorkflow.CanCheckoutRevision(revision))
        {
            RunBranchCommand(BranchWorkflow.BuildCheckoutRevision(revision.ObjectId));
        }
    }

    private Task CreateTagAtSelectedRefAsync()
    {
        return CreateTagAsync(repoObjectsTree.SelectedRef?.ObjectId);
    }

    private Task CreateTagAtSelectedRevisionAsync()
    {
        return CreateTagAsync(RevisionGrid.SelectedRevision?.ObjectId);
    }

    private async Task CreateTagAsync(ObjectId? objectId)
    {
        if (objectId is not { } target || !TagWorkflow.CanCreateTag(target))
        {
            return;
        }

        TagOptionsWindow dialog = new(target);
        var configuration = await dialog.ShowDialog<TagConfiguration?>(this);
        if (configuration is null)
        {
            return;
        }

        var tagConfiguration = configuration with { ObjectId = target };
        var module = Module;
        var validationError = TagWorkflow.ValidateCreate(module, tagConfiguration);
        if (validationError is not null)
        {
            MessageBoxes.ShowError(this, validationError);
            return;
        }

        var messageFileName = tagConfiguration.Operation.CanProvideMessage() ? Path.GetTempFileName() : null;
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
        if (repoObjectsTree.SelectedRef is not { } gitRef || !TagWorkflow.CanDeleteTag(gitRef))
        {
            return;
        }

        if (MessageBoxes.Show(
                this,
                string.Format(TranslatedStrings.ConfirmDeleteTag, gitRef.Name),
                TranslatedStrings.DeleteTagMenu,
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
        if (RevisionGrid.SelectedRevision is not { } revision || !CommitPickWorkflow.CanApply(revision))
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        var mainlineParent = 1;
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
                TranslatedStrings.MainlineParentPromptTitle,
                string.Format(TranslatedStrings.MainlineParentPrompt, parentCount),
                mainlineParent.ToString());
            var parentText = await prompt.ShowDialog<string?>(this);
            if (parentText is null || !IsCurrentRepositoryLoad(module, loadVersion))
            {
                return;
            }

            if (!int.TryParse(parentText, out mainlineParent) || mainlineParent < 1 || mainlineParent > parentCount)
            {
                MessageBoxes.ShowError(this, string.Format(TranslatedStrings.InvalidMainlineParentError, parentCount));
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
        var module = Module;
        if (ExternalToolWorkflow.GetConfiguredDiffTool(module) is null)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.NoDiffToolConfiguredError);
            return;
        }

        try
        {
            module.OpenWithDifftoolDirDiff(firstRevision, secondRevision, null);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private void RunSelectedRevisionsSequence(Func<IReadOnlyList<GitRevision>, ArgumentString> buildCommand)
    {
        IReadOnlyList<GitRevision> revisions = [.. RevisionGrid.SelectedRevisions];
        var module = Module;
        if (!CommitPickWorkflow.CanApplySequence(module, revisions))
        {
            return;
        }

        RunRemoteCommand(buildCommand(revisions));
        if (module.InTheMiddleOfConflictedMerge())
        {
            UiCommands.StartResolveConflictsDialog(this);
        }
    }

    private void RunSelectedStash(Func<GitStash, ArgumentString> buildCommand)
    {
        if (repoObjectsTree.SelectedStash is { } stash)
        {
            var module = Module;
            var loadVersion = _repositoryLoadVersion;
            UiCommands.StartGitCommandProcessDialog(this, buildCommand(stash));
            if (!IsCurrentRepositoryLoad(module, loadVersion))
            {
                return;
            }

            if (module.InTheMiddleOfConflictedMerge()
                && MessageBoxes.Show(
                    this,
                    TranslatedStrings.OfferResolveStashConflicts,
                    TranslatedStrings.ResolveMergeConflicts,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                UiCommands.StartResolveConflictsDialog(this, false);
            }

            if (IsCurrentRepositoryLoad(module, loadVersion))
            {
                UiCommands.RepoChangedNotifier.Notify();
            }
        }
    }

    private async Task ShowStashManagerAsync()
    {
        var module = Module;
        StashManagerWindow dialog = new(module, module.GetStashes(true));
        var result = await dialog.ShowDialog<StashManagerResult?>(this);
        if (result is null)
        {
            return;
        }

        switch (result.Action)
        {
            case StashManagerAction.Apply when result.Stash is not null:
                UiCommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildApply(result.Stash));
                break;
            case StashManagerAction.Pop when result.Stash is not null:
                UiCommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildPop(result.Stash));
                break;
            case StashManagerAction.Drop when result.Stash is not null:
                if (AppSettings.DontConfirmStashDrop
                    || MessageBoxes.Show(this, string.Format(TranslatedStrings.ConfirmStashDrop, result.Stash.Name),
                        TranslatedStrings.StashDropConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    UiCommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildDrop(result.Stash));
                }

                break;
            case StashManagerAction.Clear:
                if (MessageBoxes.Show(this, TranslatedStrings.ConfirmStashClear,
                        TranslatedStrings.StashDropConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                {
                    UiCommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildClear());
                }

                break;
        }

        UiCommands.RepoChangedNotifier.Notify();
    }

    private void DropSelectedStash()
    {
        if (repoObjectsTree.SelectedStash is not { } stash)
        {
            return;
        }

        if (AppSettings.DontConfirmStashDrop
            || MessageBoxes.Show(this, string.Format(TranslatedStrings.ConfirmStashDrop, stash.Name),
                TranslatedStrings.StashDropConfirmTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunRemoteCommand(StashWorkflow.BuildDrop(stash));
        }
    }

    private async Task StashChangesAsync(IReadOnlyList<string>? selectedFiles = null)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        var changedFileCount = selectedFiles?.Count ?? workingTreePanel.ChangedCount;
        var error = StashWorkflow.ValidateSave(changedFileCount);
        if (error is not null)
        {
            lblStatus.Text = error;
            MessageBoxes.ShowError(this, error);
            return;
        }

        TextPromptWindow prompt = new(TranslatedStrings.StashChangesMenu, TranslatedStrings.StashMessagePrompt);
        var message = await prompt.ShowDialog<string?>(this);
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
            MessageBoxes.ShowError(this, TranslatedStrings.StashStagedMenu + ": git >= 2.35 required.");
            return;
        }

        var error = StashWorkflow.ValidateSave(workingTreePanel.StagedCount);
        if (error is not null)
        {
            lblStatus.Text = error;
            MessageBoxes.ShowError(this, error);
            return;
        }

        TextPromptWindow prompt = new(TranslatedStrings.StashStagedMenu, TranslatedStrings.StashMessagePrompt);
        var message = await prompt.ShowDialog<string?>(this);
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
        var options = await dialog.ShowDialog<ManualStashOptions?>(this);
        if (options is not null)
        {
            AppSettings.IncludeUntrackedFilesInManualStash = options.IncludeUntrackedFiles;
            AppSettings.StashKeepIndex = options.KeepIndex;
        }
    }

    private void RunBranchCommand(IGitCommand command)
    {
        var isCheckout = command.Arguments.Contains("checkout ", StringComparison.Ordinal);
        RunWithLifecycleScripts(
            isCheckout ? ScriptEvent.BeforeCheckout : ScriptEvent.None,
            isCheckout ? ScriptEvent.AfterCheckout : ScriptEvent.None,
            () => UiCommands.StartCommandLineProcessDialog(this, command));
    }

    private void CheckoutSelectedRef(LocalChangesAction localChanges = LocalChangesAction.DontChange)
    {
        if (repoObjectsTree.SelectedRef is { } gitRef && BranchWorkflow.CanCheckout(gitRef))
        {
            RunBranchCommand(BranchWorkflow.BuildCheckout(gitRef, localChanges));
        }
    }

    private void CheckoutSelectedRefWithStash()
    {
        if (repoObjectsTree.SelectedRef is not { } gitRef || !BranchWorkflow.CanCheckout(gitRef))
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        var hasLocalChanges = module.IsDirtyDir();
        if (hasLocalChanges
            && !UiCommands.StartGitCommandProcessDialog(
                this,
                StashWorkflow.BuildSave(
                    AppSettings.IncludeUntrackedFilesInAutoStash,
                    false,
                    string.Empty)))
        {
            return;
        }

        if (!IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        var checkoutSucceeded = UiCommands.StartCommandLineProcessDialog(this, BranchWorkflow.BuildCheckout(gitRef));
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
                    TranslatedStrings.CheckoutFailedChangesStashed,
                    TranslatedStrings.CheckoutBranchStashChangesMenu,
                    MessageBoxButtons.Ok,
                    MessageBoxIcon.Information);
            }

            UiCommands.RepoChangedNotifier.Notify();
            return;
        }

        if (hasLocalChanges
            && MessageBoxes.Show(
                this,
                string.Format(TranslatedStrings.ConfirmReapplyStashedChanges, gitRef.Name),
                TranslatedStrings.CheckoutBranchStashChangesMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            UiCommands.StartGitCommandProcessDialog(this, StashWorkflow.BuildApplyLatest());
        }

        UiCommands.RepoChangedNotifier.Notify();
    }

    private void CheckoutSelectedRefWithReset()
    {
        if (repoObjectsTree.SelectedRef is not { } gitRef || !BranchWorkflow.CanCheckout(gitRef))
        {
            return;
        }

        var confirmation = string.Format(TranslatedStrings.ConfirmCheckoutDiscardChanges, gitRef.Name);
        if (MessageBoxes.Show(
                this,
                confirmation,
                TranslatedStrings.CheckoutBranchDiscardChangesMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunBranchCommand(BranchWorkflow.BuildCheckout(gitRef, LocalChangesAction.Reset));
        }
    }

    private void DeleteSelectedBranch()
    {
        if (repoObjectsTree.SelectedRef is not { } gitRef || !BranchWorkflow.CanDelete(gitRef))
        {
            return;
        }

        if (ShowBranchWorktreeWarning(gitRef))
        {
            return;
        }

        if (MessageBoxes.Show(this, string.Format(TranslatedStrings.ConfirmDeleteBranch, gitRef.Name),
                TranslatedStrings.DeleteBranchMenu, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
            DialogResult.Yes)
        {
            RunBranchCommand(BranchWorkflow.BuildDelete(gitRef, false));
        }
    }

    private void DeleteSelectedRemoteBranch()
    {
        if (repoObjectsTree.SelectedRef is not { } gitRef || !BranchWorkflow.CanDeleteRemoteBranch(gitRef))
        {
            return;
        }

        var confirmation =
            string.Format(TranslatedStrings.ConfirmDeleteRemoteBranch, gitRef.LocalName, gitRef.Remote);
        if (MessageBoxes.Show(
                this,
                confirmation,
                TranslatedStrings.DeleteRemoteBranchMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            var module = Module;
            var trackingBranch = BranchWorkflow.FindTrackingBranch(gitRef, module.GetRefs(RefsFilter.Heads));
            var trackingBranchCanBeDeleted = trackingBranch is { IsSelected: false }
                                             && BranchWorkflow.FindWorktreeUsingBranch(trackingBranch,
                                                 module.GetWorktrees()) is null;
            if (!UiCommands.StartCommandLineProcessDialog(this, BranchWorkflow.BuildDeleteRemoteBranch(gitRef))
                || !ReferenceEquals(Module, module))
            {
                return;
            }

            if (trackingBranchCanBeDeleted
                && trackingBranch is not null
                && MessageBoxes.Show(
                    this,
                    string.Format(TranslatedStrings.ConfirmDeleteLocalTrackingBranch, trackingBranch.Name),
                    TranslatedStrings.DeleteRemoteBranchMenu,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                RunBranchCommand(BranchWorkflow.BuildDelete(trackingBranch, false));
            }
        }
    }

    private void ForceDeleteSelectedBranch()
    {
        if (repoObjectsTree.SelectedRef is not { } gitRef || !BranchWorkflow.CanForceDelete(gitRef))
        {
            return;
        }

        if (ShowBranchWorktreeWarning(gitRef))
        {
            return;
        }

        var confirmation = string.Format(TranslatedStrings.ConfirmForceDeleteBranch, gitRef.Name);
        if (MessageBoxes.Show(
                this,
                confirmation,
                TranslatedStrings.ForceDeleteBranchMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            RunBranchCommand(BranchWorkflow.BuildDelete(gitRef, true));
        }
    }

    private bool ShowBranchWorktreeWarning(IGitRef gitRef)
    {
        var worktree = BranchWorkflow.FindWorktreeUsingBranch(gitRef, Module.GetWorktrees());
        if (worktree is null)
        {
            return false;
        }

        var message = string.Format(TranslatedStrings.BranchCheckedOutInWorktree, gitRef.Name, worktree.Path);
        MessageBoxes.Show(this, message, TranslatedStrings.DeleteBranchMenu, MessageBoxButtons.Ok,
            MessageBoxIcon.Warning);
        return true;
    }

    private async Task RenameSelectedBranchAsync()
    {
        if (repoObjectsTree.SelectedRef is not { } gitRef || !BranchWorkflow.CanRename(gitRef))
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        Func<string, string>? normaliseBranchName = null;
        if (AppSettings.AutoNormaliseBranchName)
        {
            var branchNameNormaliser = UiCommands.GetRequiredService<IGitBranchNameNormaliser>();
            GitBranchNameOptions options = new(AppSettings.AutoNormaliseSymbol);
            normaliseBranchName = branchName =>
                BranchWorkflow.NormaliseBranchName(branchName, branchNameNormaliser, options);
        }

        TextPromptWindow prompt = new(
            TranslatedStrings.RenameBranchMenu,
            TranslatedStrings.BranchNamePrompt,
            gitRef.Name,
            normaliseBranchName);
        var enteredName = await prompt.ShowDialog<string?>(this);
        if (enteredName is null)
        {
            return;
        }

        var newName = enteredName.Trim();
        if (newName == gitRef.Name || !IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        var error = BranchWorkflow.ValidateNewBranchName(module, newName);
        if (error is not null)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        var succeeded =
            UiCommands.StartGitCommandProcessDialog(this, BranchWorkflow.BuildRename(gitRef.Name, newName));
        if (succeeded && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task CreateBranchAtSelectionAsync(bool checkout)
    {
        var selectedRef = repoObjectsTree.SelectedRef;
        var selectedRevision = RevisionGrid.SelectedRevision;
        var startPoint = selectedRef?.ObjectId ?? selectedRevision?.ObjectId;
        if (startPoint is null)
        {
            return;
        }

        var title = checkout
            ? TranslatedStrings.CreateBranchMenu
            : TranslatedStrings.CreateBranchWithoutCheckoutMenu;
        var suggestedName = BranchWorkflow.GetSuggestedBranchName(selectedRef, selectedRevision);
        TextPromptWindow prompt = new(title, TranslatedStrings.BranchNamePrompt, suggestedName);
        var name = await prompt.ShowDialog<string?>(this);
        if (name is null)
        {
            return;
        }

        var module = Module;
        var error = BranchWorkflow.ValidateNewBranchName(module, name);
        if (error is not null)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        var succeeded =
            UiCommands.StartGitCommandProcessDialog(this, BranchWorkflow.BuildCreate(name, startPoint.Value, checkout));
        if (!succeeded)
        {
            return;
        }

        UiCommands.RepoChangedNotifier.Notify();
    }

    private async Task CreateOrphanBranchAtSelectionAsync()
    {
        var selectedRef = repoObjectsTree.SelectedRef;
        var selectedRevision = RevisionGrid.SelectedRevision;
        var startPoint = selectedRef?.ObjectId ?? selectedRevision?.ObjectId;
        if (startPoint is null)
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        TextPromptWindow prompt = new(
            TranslatedStrings.CreateOrphanBranchMenu,
            TranslatedStrings.BranchNamePrompt,
            BranchWorkflow.GetSuggestedBranchName(selectedRef, selectedRevision));
        var name = await prompt.ShowDialog<string?>(this);
        if (name is null || !IsCurrentRepositoryLoad(module, loadVersion))
        {
            return;
        }

        var error = BranchWorkflow.ValidateNewBranchName(module, name);
        if (error is not null)
        {
            MessageBoxes.ShowError(this, error);
            return;
        }

        var succeeded = UiCommands.StartGitCommandProcessDialog(
            this,
            BranchWorkflow.BuildCreateOrphan(name.Trim(), startPoint.Value));
        if (succeeded && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private void CommitStagedChanges(CommitRequest request)
    {
        if (_workingTreeOperationInProgress)
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        var stagedCount = workingTreePanel.StagedCount;

        try
        {
            var state = GetRepositoryOperationState(module);
            var error = CommitWorkflow.Validate(request, stagedCount, state.Operation == RepositoryOperation.Merge,
                state.HasUnresolvedConflicts);
            if (error is not null)
            {
                lblStatus.Text = error;
                MessageBoxes.ShowError(this, error);
                return;
            }

            foreach (var warning in request.UseExplicitCommitMessage
                         ? CommitWorkflow.GetMessageValidationWarnings(request.Message)
                         : [])
            {
                if (MessageBoxes.Show(
                        this,
                        warning,
                        TranslatedStrings.CommitValidationCaption,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Asterisk) != DialogResult.Yes)
                {
                    return;
                }
            }

            if (!AppSettings.DontConfirmCommitIfNoBranch
                && string.IsNullOrEmpty(module.GetSelectedBranch(true))
                && !module.InTheMiddleOfRebase()
                && MessageBoxes.Show(
                    this,
                    TranslatedStrings.ErrorInstructionNotOnBranch,
                    TranslatedStrings.ErrorCaptionNotOnBranch,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            if (request.Amend
                && !AppSettings.DontConfirmAmend.Value
                && MessageBoxes.Show(
                    this,
                    TranslatedStrings.AmendCommitConfirmation,
                    TranslatedStrings.AmendCommitCaption,
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

            CommitMessageManager messageManager = new(new Control(), module.WorkingDirGitDir, module.CommitEncoding);
            if (request.UseExplicitCommitMessage)
            {
                ThreadHelper.JoinableTaskFactory.Run(() => messageManager.WriteCommitMessageToFileAsync(
                    request.Message,
                    CommitMessageType.Normal,
                    usingCommitTemplate: request.UsingCommitTemplate,
                    ensureCommitMessageSecondLineEmpty: AppSettings.EnsureCommitMessageSecondLineEmpty));
            }

            var scriptsRunner = UiCommands.GetRequiredService<IScriptsRunner>();
            if (!scriptsRunner.RunEventScripts(ScriptEvent.BeforeCommit, this))
            {
                return;
            }

            var arguments = CommitWorkflow.BuildCommand(request, stagedCount,
                messageManager.CommitMessagePath, module.GetPathForGitExecution);
            var succeeded = UiCommands.StartGitCommandProcessDialog(this, arguments);

            if (IsCurrentRepositoryLoad(module, loadVersion))
            {
                UiCommands.RepoChangedNotifier.Notify();
            }

            if (succeeded)
            {
                scriptsRunner.RunEventScripts(ScriptEvent.AfterCommit, this);
                ThreadHelper.JoinableTaskFactory.Run(messageManager.ResetCommitMessageAsync);
                workingTreePanel.ResetCommitOptions();
                LoadCommitOptions(module, loadVersion, false);
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
        string[] paths =
        [
            .. workingTreePanel.SelectedItems
                .Where(item => !item.IsTracked && item.IsNew)
                .Select(item => item.Name)
        ];
        if (paths.Length == 0)
        {
            return;
        }

        try
        {
            IgnoreWorkflow.AddToGitIgnore(Module.WorkingDir, paths);
            UiCommands.RepoChangedNotifier.Notify();
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

        var module = Module;
        TextPromptWindow prompt = new(TranslatedStrings.GoToCommitMenu, TranslatedStrings.GoToCommitPrompt, "HEAD");
        var expression = (await prompt.ShowDialog<string?>(this))?.Trim();
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
            MessageBoxes.ShowError(this, TranslatedStrings.NoResultsFound);
            return;
        }

        RevisionGrid.SelectRevision(objectId);
        if (RevisionGrid.SelectedRevision?.ObjectId != objectId)
        {
            MessageBoxes.ShowError(this,
                string.Format(TranslatedStrings.GoToCommitNotLoaded, objectId.ToShortString()));
        }
    }

    private void GoToSuperproject()
    {
        if (Module.SuperprojectModule?.WorkingDir is { } superprojectDir)
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

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        ThreadHelper.FileAndForget(async () =>
        {
            var lastMessage = module.GetPreviousCommitMessages(1, "HEAD", string.Empty).FirstOrDefault();
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
                var messageCount = AppSettings.CommitDialogNumberOfPreviousMessages;
                var messages = messageCount > 0
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
                        templates.Insert(0,
                            new CommitTemplateItem(TranslatedStrings.GitCommitTemplate, gitTemplate, null, false));
                    }
                }

                var pendingMessage = string.Empty;
                if (AppSettings.UseFormCommitMessage)
                {
                    CommitMessageManager pendingMessageManager =
                        new(new Control(), module.WorkingDirGitDir, module.CommitEncoding);
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

                if (!string.IsNullOrWhiteSpace(pendingMessage) &&
                    string.IsNullOrWhiteSpace(workingTreePanel.CommitMessage))
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
        var confirmation = ResetWorkflow.BuildDiscardConfirmation(selectedItems);
        if (_workingTreeOperationInProgress
            || confirmation is null
            || MessageBoxes.Show(this, confirmation, TranslatedStrings.ResetChangesCaption, MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        RunWorkingTreeReset(module =>
            ResetWorkflow.Discard(module, selectedItems, new FullPathResolver(() => module.WorkingDir),
                out var output)
                ? string.Empty
                : output);
    }

    private void ResetAllChanges()
    {
        if (_workingTreeOperationInProgress
            || MessageBoxes.Show(this, TranslatedStrings.ConfirmResetAllChanges, TranslatedStrings.ResetChangesCaption,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        RunWorkingTreeReset(module => ResetWorkflow.ResetAll(module) ? string.Empty : TranslatedStrings.Error);
    }

    private void RunWorkingTreeReset(Func<IGitModule, string> reset)
    {
        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        _workingTreeOperationInProgress = true;
        workingTreePanel.SetOperationInProgress(true);

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
            workingTreePanel.SetOperationInProgress(false);
            LoadWorkingTreeStatus(module, loadVersion);
            UiCommands.RepoChangedNotifier.Notify();

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

        var module = Module;
        GitItemStatus[] selectedItems = [.. workingTreePanel.SelectedItems];
        var loadVersion = _repositoryLoadVersion;
        _workingTreeOperationInProgress = true;
        workingTreePanel.SetOperationInProgress(true);

        ThreadHelper.FileAndForget(async () =>
        {
            var succeeded = false;
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
            workingTreePanel.SetOperationInProgress(false);
            LoadWorkingTreeStatus(module, loadVersion);

            if (!succeeded)
            {
                var errorMessage = string.IsNullOrWhiteSpace(output) ? TranslatedStrings.Error : output;
                lblStatus.Text = errorMessage;
                MessageBoxes.ShowError(this, errorMessage);
            }
        });
    }

    private void DiscardSelectedLines()
    {
        if (_workingTreeOperationInProgress
            || fileListTabs.SelectedItem != workingTreeTab
            || workingTreePanel.SelectedItem is not { } item
            || !ReferenceEquals(_workingTreeDiffItem, item))
        {
            return;
        }

        var module = Module;
        var patch = PartialStageWorkflow.CreateDiscardPatch(
            module,
            item,
            fileViewer.Text,
            fileViewer.SelectionPosition,
            fileViewer.SelectionLength);
        if (patch is not { Length: > 0 }
            || !MessageBoxes.Confirm(
                this,
                TranslatedStrings.ConfirmDiscardSelectedLines,
                TranslatedStrings.DiscardSelectedLines,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2))
        {
            return;
        }

        var staged = item.Staged == StagedStatus.Index;
        RunWorkingTreePatch(module, () => PartialStageWorkflow.ApplyDiscardPatch(module, patch, staged));
    }

    private void StageSelectedLines(bool unstage)
    {
        if (_workingTreeOperationInProgress
            || fileListTabs.SelectedItem != workingTreeTab
            || workingTreePanel.SelectedItem is not { } item
            || !ReferenceEquals(_workingTreeDiffItem, item))
        {
            return;
        }

        var module = Module;
        if (unstage != PartialStageWorkflow.CanUnstage(item))
        {
            return;
        }

        var patch = PartialStageWorkflow.CreatePatch(
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
        var loadVersion = _repositoryLoadVersion;
        _workingTreeOperationInProgress = true;
        workingTreePanel.SetOperationInProgress(true);

        ThreadHelper.FileAndForget(async () =>
        {
            var succeeded = false;
            string output;
            try
            {
                var result = apply();
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
            workingTreePanel.SetOperationInProgress(false);
            LoadWorkingTreeStatus(module, loadVersion);

            if (succeeded)
            {
                _workingTreeDiffItem = null;
                fileViewer.ViewPatch(string.Empty);
                workingTreePanel.SetCanStageSelectedLines(false);
                workingTreePanel.SetCanUnstageSelectedLines(false);
                workingTreePanel.SetCanDiscardSelectedLines(false);
                UiCommands.RepoChangedNotifier.Notify();
            }
            else
            {
                var errorMessage = string.IsNullOrWhiteSpace(output) ? TranslatedStrings.Error : output;
                lblStatus.Text = errorMessage;
                MessageBoxes.ShowError(this, errorMessage);
            }
        });
    }

    private static string FormatWorkingTreeStatus(IReadOnlyList<GitItemStatus> status)
    {
        var error = status.FirstOrDefault(item => item.ErrorMessage is not null);
        if (error is not null)
        {
            throw new InvalidOperationException(error.ErrorMessage);
        }

        GitItemStatus[] items = [.. status.Where(item => !item.IsStatusOnly)];
        var untracked = items.Count(item => !item.IsTracked && item.IsNew);

        return items.Length > 0
            ? string.Format(TranslatedStrings.WorkingTreeStatusSummary, items.Length - untracked, untracked)
            : TranslatedStrings.NoResultsFound;
    }

    private void RepoObjectsTree_SelectionChanged(object? sender, EventArgs e)
    {
        if (repoObjectsTree.SelectedRef is { } gitRef)
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

        var fileListCancellationToken = _revisionFilesRequestSequence.Next();
        _fileDiffRequestSequence.Next();
        fileStatusList.Clear();
        fileViewer.ViewPatch(string.Empty);
        CommitInfo.Revision = RevisionGrid.SelectedRevision;
        RefreshRevisionTree();

        if (RevisionGrid.SelectedRevision is not { } revision
            || !revision.HasParent)
        {
            return;
        }

        var module = Module;
        var parentId = revision.FirstParentId;
        ThreadHelper.FileAndForget(async () =>
        {
            var diffs = module.GetDiffFilesWithSubmodulesStatus(
                parentId,
                revision.ObjectId,
                parentId,
                false,
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

        if (RevisionGrid.SelectedRevision is not { } revision
            || !revision.HasParent
            || fileStatusList.SelectedItem is not { } item)
        {
            return;
        }

        var module = Module;
        var parentId = revision.FirstParentId;
        var diffCancellationToken = _fileDiffRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            (var patch, var errorMessage) = await module.GetSingleDiffAsync(
                parentId,
                revision.ObjectId,
                item.Name,
                item.OldName,
                "",
                module.FilesEncoding,
                true,
                item.IsTracked,
                false,
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
        if (fileStatusList.SelectedItem is not { } file)
        {
            return;
        }

        var workingDir = Module.WorkingDir;
        var fullPath = FilePathWorkflow.GetFullPath(workingDir, file.Name);
        switch (action)
        {
            case FileListAction.CopyPath or FileListAction.CopyFullPath:
                if (Clipboard is { } clipboard)
                {
                    await clipboard.SetTextAsync(FilePathWorkflow.FormatPaths([file], workingDir,
                        action == FileListAction.CopyFullPath));
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
                var directory = Path.GetDirectoryName(fullPath);
                if (directory is not null && Directory.Exists(directory))
                {
                    OpenWithShell(directory);
                }

                break;
        }

        return;

        static void OpenWithShell(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
    }

    private void OpenSelectedFileHistory(bool showBlame)
    {
        if (fileStatusList.SelectedItem is not { } item
            || RevisionGrid.SelectedRevision is not { } revision)
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

        if (RevisionGrid.SelectedRevision is { IsArtificial: false } revision)
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

        if (revisionFileTree.SelectedFile is not { } entry)
        {
            fileViewer.ViewPatch(string.Empty);
            return;
        }

        var module = Module;
        var cancellationToken = _fileDiffRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            var text = await Task.Run(() => module.GetFileText(entry.ObjectId, module.FilesEncoding, false),
                cancellationToken);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            if (!cancellationToken.IsCancellationRequested && ReferenceEquals(Module, module))
            {
                fileViewer.ViewPatch(text ?? string.Empty);
            }
        });
    }

    private void OpenRevisionTreeFileHistory()
    {
        if (revisionFileTree.SelectedFile is { } entry)
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
        if (workingTreePanel.SelectedItem is not { } item)
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
        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        var cancellationToken = _workingTreeDiffRequestSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            var firstId = item.Staged == StagedStatus.Index ? module.GetCurrentCheckout() : ObjectId.IndexId;
            var secondId = item.Staged == StagedStatus.Index ? ObjectId.IndexId : ObjectId.WorkTreeId;
            (var patch, var errorMessage) = await module.GetSingleDiffAsync(
                firstId,
                secondId,
                item.Name,
                item.OldName,
                "",
                module.FilesEncoding,
                false,
                item.IsTracked,
                false,
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
        var item = _workingTreeDiffItem;
        var hasLineSelection = fileListTabs.SelectedItem == workingTreeTab
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

    private void UICommands_PostRepositoryChanged(object? sender, GitUiEventArgs e)
    {
        this.InvokeAndForget(ReloadRepository);
    }

    private void FormBrowse_Closed(object? sender, EventArgs e)
    {
        _isClosed = true;
        if (PluginRegistry.IsInitialized)
        {
            PluginRegistry.Unregister(UiCommands, this);
        }

        UiCommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
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
        if (dialog.ShowDialog(this) is DialogResult.Ok && dialog.SelectedPath is { } path)
        {
            OpenRepository(path);
        }
    }

    private void RefreshToolStripMenuItemClick(object? sender, EventArgs e)
    {
        UiCommands.RepoChangedNotifier.Notify();
    }

    private async Task ManageRemotesAsync()
    {
        RemoteManagementWindow dialog = new(Module);
        await dialog.ShowDialog<bool>(this);
        if (dialog.HasChanges)
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task ManageSubmodulesAsync()
    {
        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        SubmoduleManagementWindow dialog = new(module, UiCommands, OpenRepository);
        await dialog.ShowDialog<bool>(this);
        if (dialog.HasChanges && IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task InitializeRepositoryAsync()
    {
        InitRepositoryWindow dialog = new(
            UiCommands.GetRequiredService<IGitExecutorProvider>(),
            OpenRepository,
            Module.WorkingDir);
        await dialog.ShowDialog<bool>(this);
    }

    private async Task CloneRepositoryAsync()
    {
        CloneRepositoryWindow dialog = new(UiCommands, null, path => OpenRepository(path));
        await dialog.ShowDialog<bool>(this);
    }

    private async Task ShowReflogAsync()
    {
        ReflogWindow dialog = new(Module);
        await dialog.ShowDialog<bool>(this);
        if (dialog.RequestedCommand is { } command)
        {
            RunRemoteCommand(command);
        }
    }

    private async Task ApplyPatchAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false, Title = TranslatedStrings.ApplyPatchMenu
        });

        if (files.Count == 0)
        {
            return;
        }

        var module = Module;
        var loadVersion = _repositoryLoadVersion;
        ArgumentString arguments;
        try
        {
            var patchPath = files[0].Path.LocalPath;
            PatchOptionsWindow optionsDialog = new(!PatchWorkflow.IsDiffPatch(patchPath));
            var patchOptions = await optionsDialog.ShowDialog<PatchApplyOptions?>(this);
            if (patchOptions is null)
            {
                return;
            }

            arguments = PatchWorkflow.BuildApply(module, patchPath, patchOptions);
        }
        catch (IOException)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.PatchFileReadError);
            return;
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.PatchFileReadError);
            return;
        }

        UiCommands.StartGitCommandProcessDialog(this, arguments);
        if (IsCurrentRepositoryLoad(module, loadVersion))
        {
            UiCommands.RepoChangedNotifier.Notify();
        }
    }

    private async Task FormatPatchAsync()
    {
        var module = Module;
        var repositoryLoadVersion = _repositoryLoadVersion;
        IReadOnlyList<GitRevision> revisions = [.. RevisionGrid.SelectedRevisions.Reverse()];
        if (revisions.Count == 0 || revisions.Any(revision => revision.ObjectId.IsArtificial))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.NoRevision);
            return;
        }

        var previousOutputDirectory = AppSettings.LastFormatPatchDir;
        var suggestedStartLocation = Directory.Exists(previousOutputDirectory)
            ? await StorageProvider.TryGetFolderFromPathAsync(previousOutputDirectory)
            : null;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation,
            Title = TranslatedStrings.FormatPatchMenu
        });

        if (folders.Count == 0)
        {
            return;
        }

        if (folders[0].TryGetLocalPath() is not { } outputDirectory)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.ChooseDirectory);
            return;
        }

        if (!Directory.Exists(outputDirectory))
        {
            MessageBoxes.ShowError(this, TranslatedStrings.ChooseDirectory);
            return;
        }

        if (!IsCurrentRepositoryLoad(module, repositoryLoadVersion))
        {
            return;
        }

        try
        {
            AppSettings.LastFormatPatchDir = outputDirectory;
            var commands =
                PatchWorkflow.BuildFormatPatches(module, revisions, outputDirectory);
            var result = await Task.Run(() => string.Join(
                Environment.NewLine,
                commands.Select(command => module.GitExecutable.GetOutput(command))));

            if (string.IsNullOrWhiteSpace(result))
            {
                MessageBoxes.ShowError(this, TranslatedStrings.NoResultsFound);
                return;
            }

            MessageBoxes.Show(this, result, TranslatedStrings.FormatPatchMenu, MessageBoxButtons.Ok,
                MessageBoxIcon.Information);
        }
        catch (Exception exception)
        {
            MessageBoxes.ShowError(this, exception.ToStringWithData());
        }
    }

    private async Task ViewPatchAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false, Title = TranslatedStrings.ViewPatchMenu
        });

        if (files.Count == 0)
        {
            return;
        }

        try
        {
            var patch = await Task.Run(() => PatchWorkflow.ReadPatchFile(files[0].Path.LocalPath));
            fileViewer.ViewPatch(patch);
        }
        catch (IOException)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.PatchFileReadError);
        }
        catch (UnauthorizedAccessException)
        {
            MessageBoxes.ShowError(this, TranslatedStrings.PatchFileReadError);
        }
    }

    private void fetchAllToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        ArgumentString arguments = new GitArgumentBuilder("fetch") { "--all", "--progress" };

        if (UiCommands.StartGitCommandProcessDialog(this, arguments))
        {
            UiCommands.RepoChangedNotifier.Notify();
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
        var themeId = AppSettings.ThemeId;
        var followsSystem = themeId == ThemeId.WindowsAppColorModeId;
        var isDark = themeId == ThemeId.DefaultDark;
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
        Bisect
    }

    private readonly record struct RepositoryOperationState(RepositoryOperation Operation, bool HasUnresolvedConflicts)
    {
        public bool IsActive
        {
            get => Operation != RepositoryOperation.None || HasUnresolvedConflicts;
        }
    }
}
