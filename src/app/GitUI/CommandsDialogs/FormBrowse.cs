using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using GitCommands;
using GitCommands.Config;
using GitCommands.Git;
using GitCommands.Git.Gpg;
using GitCommands.Settings;
using GitCommands.Submodules;
using GitCommands.Utils;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Plugins;
using GitExtensions.Extensibility.Settings;
using GitExtensions.Extensibility.Translations;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.Avatars;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.CommandsDialogs.BrowseDialog.DashboardControl;
using GitUI.CommandsDialogs.Menus;
using GitUI.CommandsDialogs.WorktreeDialog;
using GitUI.ConsoleEmulation;
using GitUI.HelperDialogs;
using GitUI.Infrastructure.Telemetry;
using GitUI.LeftPanel;
using GitUI.Models;
using GitUI.Plugin;
using GitUI.Properties;
using GitUI.ScriptsEngine;
using GitUI.Shells;
using GitUI.UserControls;
using GitUI.UserControls.RevisionGrid;
using Microsoft.VisualStudio.Threading;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Taskbar;
using ResourceManager;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommandsDialogs;

public sealed partial class FormBrowse : GitModuleForm, IBrowseRepo
{
    private readonly IAheadBehindDataProvider? _aheadBehindDataProvider;
    private readonly IAppTitleGenerator _appTitleGenerator;

    private readonly uint _closeAllMessage =
        System.Windows.Forms.NativeMethods.RegisterWindowMessageW("Global.GitExtensions.CloseAllInstances");

    private readonly ICommitDataManager _commitDataManager;
    private readonly IConsoleEmulatorsRegistry _consoleEmulatorsRegistry;
    private readonly IGpgInfoProvider _controller;
    private readonly FormBrowseDiagnosticsReporter _formBrowseDiagnosticsReporter;
    private readonly FormBrowseMenus _formBrowseMenus;
    private readonly GitStatusMonitor _gitStatusMonitor;
    private readonly CancellationTokenSequence _gpgInfoSequence = new();
    private readonly bool _isFileHistoryMode;
    private readonly TaskManager _loadOperations = ThreadHelper.CreateTaskManager();

    private readonly Dictionary<Brush, Icon> _overlayIconByBrush = [];
    private readonly IRepositoryHistoryUiService _repositoryHistoryUiService;
    private readonly IScriptsManager _scriptsManager;
    private readonly IShellProvider _shellProvider;
    private readonly SplitterManager _splitterManager;
    private readonly ISubmoduleStatusProvider _submoduleStatusProvider;
    private readonly IWindowsJumpListManager _windowsJumpListManager;
    private BuildReportTabPageExtension? _buildReportTabPageExtension;

    private TabPage? _consoleTabPage;
    private List<ToolStripItem>? _currentSubmoduleMenuItems;
    private Dashboard? _dashboard;
    private bool _fileBlameHistoryLeftPanelStartupState;
    private OutputHistoryControllerBase? _outputHistoryController;

    private UpdateTargets _selectedRevisionUpdatedTargets = UpdateTargets.None;
    private IConsoleShellRunner? _terminal;

    /// <summary>
    ///     Open Browse - main GUI including dashboard.
    /// </summary>
    /// <param name="commands">The commands in the current form.</param>
    /// <param name="args">The start up arguments.</param>
    public FormBrowse(IGitUiCommands commands, BrowseArguments args)
#pragma warning disable CS0618 // Type or member is obsolete
        : this(commands, args, new AppSettingsPath("FormBrowse"))
#pragma warning restore CS0618 // Type or member is obsolete
    {
    }

    [Obsolete("Test only!")]
    internal FormBrowse(IGitUiCommands commands, BrowseArguments args, SettingsSource settingsSource)
        : base(commands)
    {
        _commitDataManager = new CommitDataManager(() => Module);
        _commitDataManager.RevisionDetailsLoaded += (_, _) => RevisionGridControl.Invalidate(true);

        _splitterManager = new SplitterManager(settingsSource);

        SystemEvents.SessionEnding += (sender, args) => SaveApplicationSettings();

        _isFileHistoryMode = args.IsFileHistoryMode;
        InitializeComponent();

        _repositoryHistoryUiService = commands.GetRequiredService<IRepositoryHistoryUiService>();

        _consoleEmulatorsRegistry = commands.GetRequiredService<IConsoleEmulatorsRegistry>();
        _shellProvider = commands.GetRequiredService<IShellProvider>();

        fileToolStripMenuItem.Initialize(() => UiCommands);
        helpToolStripMenuItem.Initialize(() => UiCommands);
        toolsToolStripMenuItem.Initialize(() => UiCommands);

        BackColor = OtherColors.BackgroundColor;

        WorkaroundPaddingIncreaseBug();

        _appTitleGenerator = commands.GetRequiredService<IAppTitleGenerator>();
        _windowsJumpListManager = commands.GetRequiredService<IWindowsJumpListManager>();
        _scriptsManager = commands.GetRequiredService<IScriptsManager>();

        _formBrowseDiagnosticsReporter = new FormBrowseDiagnosticsReporter(this);

        _repositoryHostsToolStripMenuItem.Visible = false;

        MainSplitContainer.Visible = false;
        MainSplitContainer.SplitterDistance = DpiUtil.Scale(260);

        InitCountArtificial(out _gitStatusMonitor);

        _formBrowseMenus = new FormBrowseMenus(mainMenuStrip);

        RevisionGridControl.SuspendRefreshRevisions();

        ToolStripFilters.Bind(() => Module, RevisionGridControl);

        InitRevisionGrid(args.SelectedId, args.FirstId, args.IsFileHistoryMode);
        InitCommitDetails();

        // Please do not ask me why the setting in the Designer has no effect!
        var splitterBackColor = LeftSplitContainer.BackColor;
        LeftSplitContainer.Invalidated += FixupSplitterColor;

        InitializeComplete();

        // The toolstrip and menu items must be initialised after InitializeComplete
        // which invokes the translation logic and applies the current language to the components.
        _NO_TRANSLATE_WorkingDir.Initialize(() => UiCommands, _repositoryHistoryUiService, fileToolStripMenuItem,
            closeToolStripMenuItem);

        HotkeysEnabled = true;
        LoadHotkeys(HotkeySettingsName);
        SetShortcutKeyDisplayStringsFromHotkeySettings();
        InitMenusAndToolbars(args.RevFilter, args.PathFilter.ToPosixPath());

        UiCommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        UiCommands.BrowseRepo = this;

        _controller = new GpgInfoProvider(new GitGpgController(() => Module));

        _submoduleStatusProvider = commands.GetRequiredService<ISubmoduleStatusProvider>();
        _submoduleStatusProvider.StatusUpdating += SubmoduleStatusProvider_StatusUpdating;
        _submoduleStatusProvider.StatusUpdated += SubmoduleStatusProvider_StatusUpdated;

        foreach (var control in this.FindDescendants())
        {
            control.AllowDrop = true;
            control.DragEnter += FormBrowse_DragEnter;
            control.DragDrop += FormBrowse_DragDrop;
        }

        _aheadBehindDataProvider = new AheadBehindDataProvider(() => Module.GitExecutable);
        toolStripButtonPush.ResetToDefaultState();
        repoObjectsTree.Initialize(_aheadBehindDataProvider, ToolStripFilters.SetBranchFilter, RevisionGridControl,
            RevisionGridControl);
        revisionDiff.Bind(RevisionGridControl, RevisionGridControl, fileTree,
            () => RevisionGridControl.CurrentFilter.PathFilter, RefreshGitStatusMonitor);
        fileTree.Bind(RevisionGridControl, RevisionGridControl, null,
            () => RevisionGridControl.CurrentFilter.PathFilter, RefreshGitStatusMonitor, _isFileHistoryMode);
        RevisionGridControl.SetAheadBehindDataProvider(_aheadBehindDataProvider);
        RevisionGridControl.ResumeRefreshRevisions();

        // Application is init, the repo related operations are triggered in OnLoad()
        return;

        void FixupSplitterColor(object? sender, EventArgs eventArgs)
        {
            LeftSplitContainer.BackColor = splitterBackColor;
            LeftSplitContainer.Invalidated -= FixupSplitterColor;
        }

        void InitCountArtificial(out GitStatusMonitor gitStatusMonitor)
        {
            Brush? lastBrush = null;

            gitStatusMonitor = new GitStatusMonitor(this, () => IsMinimized());
            if (!NeedsGitStatusMonitor())
            {
                gitStatusMonitor.Active = false;
            }

            gitStatusMonitor.GitStatusMonitorStateChanged += (s, e) =>
            {
                var status = e.State;
                if (status == GitStatusMonitorState.Stopped)
                {
                    // fall back to operation without info in the button
                    UpdateCommitButtonAndGetBrush(null, false);
                    RevisionGridControl.UpdateArtificialCommitCount(null);
                    if (EnvUtils.RunningOnWindowsWithMainWindow())
                    {
                        TaskbarManager.Instance.SetOverlayIcon(null, "");
                    }

                    lastBrush = null;
                }
            };

            gitStatusMonitor.GitWorkingDirectoryStatusChanged += (s, e) =>
            {
                var status = e?.ItemStatuses;

                var countToolbar = AppSettings.ShowGitStatusInBrowseToolbar;
                var countArtificial = AppSettings.ShowGitStatusForArtificialCommits &&
                                      AppSettings.RevisionGraphShowArtificialCommits;

                var brush = UpdateCommitButtonAndGetBrush(status, countToolbar);

                RevisionGridControl.UpdateArtificialCommitCount(countArtificial ? status : null);

                if (countToolbar || countArtificial)
                {
                    UpdateStatusInTaskbar();

                    if (AppSettings.ShowSubmoduleStatus)
                    {
                        Validates.NotNull(_submoduleStatusProvider);

                        ThreadHelper.FileAndForget(async () =>
                        {
                            try
                            {
                                await _submoduleStatusProvider.UpdateSubmodulesStatusAsync(Module.WorkingDir, status);
                            }
                            catch (GitConfigurationException ex)
                            {
                                await this.SwitchToMainThreadAsync();
                                MessageBoxes.ShowGitConfigurationExceptionMessage(this, ex);
                            }
                        });
                    }
                }

                return;

                void UpdateStatusInTaskbar()
                {
                    if (!EnvUtils.RunningOnWindowsWithMainWindow())
                    {
                        return;
                    }

                    if (ReferenceEquals(brush, lastBrush))
                    {
                        TaskbarManager.Instance.SetOverlayIcon(_overlayIconByBrush[lastBrush], "");
                        return;
                    }

                    lastBrush = brush;

                    if (!_overlayIconByBrush.TryGetValue(brush, out var overlay))
                    {
                        const int imgDim = 32;
                        const int dotDim = 15;
                        const int pad = 2;
                        using Bitmap bmp = new(imgDim, imgDim);
                        using var g = Graphics.FromImage(bmp);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        g.FillEllipse(brush,
                            new Rectangle(imgDim - dotDim - pad, imgDim - dotDim - pad, dotDim, dotDim));

                        overlay = bmp.ToIcon();
                        _overlayIconByBrush.Add(brush, overlay);
                    }

                    TaskbarManager.Instance.SetOverlayIcon(overlay, "");

                    _windowsJumpListManager.UpdateCommitIcon(toolStripButtonCommit.Image!);
                }
            };
        }

        bool IsMinimized()
        {
            return WindowState == FormWindowState.Minimized;
        }

        void WorkaroundPaddingIncreaseBug()
        {
            MainSplitContainer.Panel1.Padding = new Padding(1);
            RevisionsSplitContainer.Panel1.Padding = new Padding(1);
            RevisionsSplitContainer.Panel2.Padding = new Padding(1);
        }
    }

    public RevisionGridControl RevisionGridControl { get; private set; }

    void IBrowseRepo.SetWorkingDir(string? path, ObjectId selectedId = default, ObjectId firstId = default)
    {
        RevisionGridControl.SelectedId = selectedId;
        RevisionGridControl.FirstId = firstId;
        SetGitModule(this,
            new GitModuleEventArgs(new GitModule(UiCommands.GetRequiredService<IGitExecutorProvider>(), path)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadOperations.JoinPendingOperations();
            _formBrowseMenus?.Dispose();
            components?.Dispose();
            _gitStatusMonitor?.Dispose();
            _windowsJumpListManager?.Dispose();
            _gpgInfoSequence.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnApplicationActivated()
    {
        if (AppSettings.RefreshArtificialCommitOnApplicationActivated)
        {
            if (CommitInfoTabControl.SelectedTab == DiffTabPage)
            {
                revisionDiff.RefreshArtificial();
            }
            else if (CommitInfoTabControl.SelectedTab == TreeTabPage)
            {
                fileTree.RefreshArtificial();
            }
        }

        base.OnApplicationActivated();
    }

    protected override void OnLoad(EventArgs e)
    {
        _formBrowseMenus.CreateToolbarsMenus(ToolStripMain, ToolStripFilters, ToolStripScripts);

        RefreshSplitViewLayout();
        LayoutRevisionInfo();
        SetSplitterPositions();

        base.OnLoad(e);

        _formBrowseDiagnosticsReporter.Report();

        // All app init is done, make all repo related similar to switching repos
        SetGitModule(this,
            new GitModuleEventArgs(new GitModule(UiCommands.GetRequiredService<IGitExecutorProvider>(),
                Module.WorkingDir)));
        var isDashboard = _dashboard?.Visible ?? false;
        _loadOperations.InvokeAndForget(this, async () =>
        {
            _outputHistoryController = AppSettings.ShowOutputHistoryAsTab.Value
                ? new OutputHistoryTabController(UiCommands.GetRequiredService<IOutputHistoryProvider>(),
                    new OutputHistoryControl(), CommitInfoTabControl,
                    _outputHistoryTabCaption.Text)
                : new OutputHistoryPanelController(UiCommands.GetRequiredService<IOutputHistoryProvider>(),
                    new OutputHistoryControl(), toolPanel.ContentPanel,
                    toggleLeftPanel.PerformClick,
                    LeftSplitContainer, revisionDiff.LeftSplitContainer, revisionDiff.HorizontalSplitter);

            await TaskScheduler.Default;

            if (isDashboard)
            {
                // Load only the git hoster plugin to quickly provide related features in dashboard
                PluginRegistry.InitializeGitHostersOnly();

                await this.SwitchToMainThreadAsync();

                // Now that hoster plugin is registered, populate Git-host-plugin actions on Dashboard, like "Clone GitHub repository"
                UpdateRepositoryHostsMenu();

                // Check if during plugin loading user left dashboard
                if (_dashboard?.Visible is true)
                {
                    _dashboard.RefreshContent();
                }

                // Come back to background to load all the other plugins (that could be long...)
                await TaskScheduler.Default;
            }

            await InitializeAndRegisterAllPluginsAsync();

            await Task.Delay(100);
            UiCommands.GetRequiredService<IRepositoryHistoryUiService>().TriggerBranchNameCacheUpdate(true);
        });

        return;

        async Task InitializeAndRegisterAllPluginsAsync()
        {
            PluginRegistry.InitializeAll();
            await this.SwitchToMainThreadAsync();
            RegisterPlugins();
            UpdateRepositoryHostsMenu();
        }

        void UpdateRepositoryHostsMenu()
        {
            if (PluginRegistry.GitHosters.Count != 0)
            {
                // TODO: support more than one Git hosting provider plugin (ADO, GitLab,...)
                _repositoryHostsToolStripMenuItem.Text = PluginRegistry.GitHosters[0].Name;
            }

            // Show "Repository hosts" menu item when there is at least 1 repository host plugin loaded
            _repositoryHostsToolStripMenuItem.Visible = PluginRegistry.GitHosters.Count != 0;
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        // wait for windows to really be displayed, which isn't necessarily the case in OnLoad()
        if (_windowsJumpListManager.NeedsJumpListCreation)
        {
            _windowsJumpListManager.CreateJumpList(
                Handle,
                new WindowsThumbnailToolbarButtons(
                    new WindowsThumbnailToolbarButton(toolStripButtonCommit.Text!, toolStripButtonCommit.Image!,
                        CommitToolStripMenuItemClick),
                    new WindowsThumbnailToolbarButton(toolStripButtonPush.Text!, toolStripButtonPush.Image!,
                        PushToolStripMenuItemClick),
                    new WindowsThumbnailToolbarButton(toolStripButtonPull.Text!, toolStripButtonPull.Image!,
                        PullToolStripMenuItemClick),
                    new WindowsThumbnailToolbarButton(_closeAll.Text, Images.DeleteFile,
                        (s, e) => System.Windows.Forms.NativeMethods.PostMessageW(
                            System.Windows.Forms.NativeMethods.HWND_BROADCAST, _closeAllMessage))));
        }

        _windowsJumpListManager.EnableThumbnailToolbar(_dashboard?.Visible is not true &&
                                                       Module.IsValidGitWorkingDir());

        this.InvokeAndForget(OnActivate);
        base.OnActivated(e);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        var formDeactivatedByOwnModalDialog = ActiveForm is not null;
        _windowsJumpListManager.EnableThumbnailToolbar(!formDeactivatedByOwnModalDialog &&
                                                       _dashboard?.Visible is not true &&
                                                       Module.IsValidGitWorkingDir());

        base.OnDeactivate(e);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Restore state at startup if file history mode, ignore the forced setting
        if (_isFileHistoryMode)
        {
            MainSplitContainer.Panel1Collapsed = _fileBlameHistoryLeftPanelStartupState;
        }

        _splitterManager.SaveSplitters();

        SaveApplicationSettings();

        foreach (var control in this.FindDescendants())
        {
            control.DragEnter -= FormBrowse_DragEnter;
            control.DragDrop -= FormBrowse_DragDrop;
        }

        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        PluginRegistry.Unregister(UiCommands);
        RevisionGridControl.CancelBackgroundTasks();
        base.OnFormClosed(e);
    }

    protected override void OnUICommandsChanged(GitUiCommandsChangedEventArgs e)
    {
        var oldCommands = e.OldCommands;
        RefreshDefaultPullAction();

        if (oldCommands is not null)
        {
            oldCommands.PostRepositoryChanged -= UICommands_PostRepositoryChanged;
            oldCommands.BrowseRepo = null;
        }

        UiCommands.PostRepositoryChanged += UICommands_PostRepositoryChanged;
        UiCommands.BrowseRepo = this;

        base.OnUICommandsChanged(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == _closeAllMessage || m is
            {
                Msg: System.Windows.Forms.NativeMethods.WmSyscommand, WParam: System.Windows.Forms.NativeMethods.ScClose
            })
        {
            // Application close is requested, e.g. using the Taskbar context menu.
            // This request is directed to the main form also if a modal form like FormCommit is on top.
            // So forward the request and try to close the modal form.
            var modalForm = Application.OpenForms.Cast<Form>().FirstOrDefault(form => form.Modal);
            modalForm?.Close();

            Close();
        }

        base.WndProc(ref m);
    }

    public override void AddTranslationItems(ITranslation translation)
    {
        base.AddTranslationItems(translation);
        TranslationUtils.AddTranslationItemsFromFields(Name, ToolStripFilters, translation);
    }

    public override void TranslateItems(ITranslation translation)
    {
        base.TranslateItems(translation);
        TranslationUtils.TranslateItemsFromFields(Name, ToolStripFilters, translation);
    }

    public override void CancelButtonClick(object? sender, EventArgs e)
    {
        // If a filter is applied, clear it
        if (RevisionGridControl.FilterIsApplied())
        {
            // Clear filter
            ToolStripFilters.SetRevisionFilter(string.Empty);
        }
    }

    private static bool NeedsGitStatusMonitor()
    {
        return AppSettings.ShowGitStatusInBrowseToolbar || (AppSettings.ShowGitStatusForArtificialCommits &&
                                                            AppSettings.RevisionGraphShowArtificialCommits);
    }

    private void UICommands_PostRepositoryChanged(object? sender, GitUiEventArgs e)
    {
        // Note that this called in most FormBrowse context to "be sure"
        // that the repo has not been updated externally.

        // It can also be called from background tasks, e.g. from BackgroundFetchPlugin.
        if (!ThreadHelper.JoinableTaskContext.IsOnMainThread)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await this.SwitchToMainThreadAsync();
                RefreshRevisions(e.GetRefs);
            });
            return;
        }

        RefreshRevisions(e.GetRefs);
    }

    /// <summary>
    ///     Refresh revisions, also handling changes external to GE.
    /// </summary>
    private void RefreshRevisions()
    {
        RefreshRevisions(new FilteredGitRefsProvider(UiCommands.Module).GetRefs);
    }

    /// <summary>
    ///     Refresh revisions, also handling changes external to GE.
    /// </summary>
    /// <param name="getRefs">(Lazy) func to get refs.</param>
    private void RefreshRevisions(Func<RefsFilter, IReadOnlyList<IGitRef>> getRefs)
    {
        if (RevisionGridControl.IsDisposed || IsDisposed || Disposing)
        {
            return;
        }

        _aheadBehindDataProvider?.ResetCache();
        var isDashboard = string.IsNullOrEmpty(Module.WorkingDir) || (_dashboard?.Visible ?? false);
        if (isDashboard)
        {
            // Explicit call: Title is normally updated on RevisionGrid filter change
            Text = _appTitleGenerator.Generate();

            // "Repo" related methods, creates _dashboard
            InternalInitialize();

            return;
        }

        // Pre-warm the ahead/behind cache in the background so the revision grid can pick it up synchronously. GetData checks the AppSetting itself.
        ThreadHelper.FileAndForget(() => _ = _aheadBehindDataProvider?.GetData());

        RevisionGridControl.PerformRefreshRevisions(getRefs, true);

        InternalInitialize();
        ToolStripFilters.RefreshRevisionFunction(getRefs);
        UpdateSubmodulesStructure();

        RefreshGitStatusMonitor();
        UpdateStashCount();
    }

    private void RefreshGitStatusMonitor()
    {
        _gitStatusMonitor?.RequestRefresh();
    }

    private void RefreshSelection()
    {
        var selectedRevisions = RevisionGridControl.GetSelectedRevisions();
        var selectedRevision = selectedRevisions.Count > 0 ? selectedRevisions[0] : null;

        FillFileTree(selectedRevision);
        FillDiff(selectedRevisions);

        var old = (selectedRevision?.Body, selectedRevision?.Notes);
        FillCommitInfo(selectedRevision);

        // If the revision's body has been updated then the grid needs to be refreshed to display it
        if (AppSettings.ShowCommitBodyInRevisionGrid && selectedRevision?.HasMultiLineMessage is true &&
            old != (selectedRevision.Body, selectedRevision.Notes))
        {
            RevisionGridControl.Refresh();
        }

        var gpgInfoCancellationToken = _gpgInfoSequence.Next();
        revisionGpgInfo1.InvokeAndForget(() => FillGpgInfoAsync(selectedRevision, gpgInfoCancellationToken),
            cancellationToken: gpgInfoCancellationToken);
        FillBuildReport(selectedRevision);
        repoObjectsTree.SelectionChanged(selectedRevisions);
    }

    /// <summary>
    ///     Set the path filter.
    /// </summary>
    /// <param name="pathFilter">Zero or more quoted paths, separated by spaces.</param>
    public void SetPathFilter(string pathFilter)
    {
        RevisionGridControl.SetAndApplyPathFilter(pathFilter);
    }

    private void ShowDashboard()
    {
        _windowsJumpListManager.EnableThumbnailToolbar(false);

        toolPanel.SuspendLayout();
        toolPanel.TopToolStripPanelVisible = false;
        toolPanel.BottomToolStripPanelVisible = false;
        toolPanel.LeftToolStripPanelVisible = false;
        toolPanel.RightToolStripPanelVisible = false;
        toolPanel.ResumeLayout();

        MainSplitContainer.Visible = false;

        if (_dashboard is null)
        {
            _dashboard = new Dashboard { Dock = DockStyle.Fill };
            _dashboard.GitModuleChanged += SetGitModule;
            toolPanel.ContentPanel.Controls.Add(_dashboard);
        }

        Text = _appTitleGenerator.Generate(branchName: TranslatedStrings.NoBranch);

        _dashboard.Visible = true;
        _dashboard.BringToFront();

        _createPullRequestsToolStripMenuItem.Enabled = false;
        _viewPullRequestsToolStripMenuItem.Enabled = false;
        _addUpstreamRemoteToolStripMenuItem.Enabled = false;

        DiagnosticsClient.TrackPageView("Dashboard");
    }

    private void HideDashboard()
    {
        MainSplitContainer.Visible = true;
        if (!_dashboard?.Visible ?? true)
        {
            return;
        }

        _dashboard!.Visible = false;
        toolPanel.SuspendLayout();
        toolPanel.TopToolStripPanelVisible = true;
        toolPanel.BottomToolStripPanelVisible = true;
        toolPanel.LeftToolStripPanelVisible = true;
        toolPanel.RightToolStripPanelVisible = true;
        toolPanel.ResumeLayout();

        DiagnosticsClient.TrackPageView("Revision graph");
    }

    private void UpdatePluginMenu(bool validWorkingDir)
    {
        foreach (ToolStripItem item in pluginsToolStripMenuItem.DropDownItems)
        {
            if (item == pluginsLoadingToolStripMenuItem)
            {
                continue;
            }

            item.Enabled = item.Tag is not IGitPluginForRepository || validWorkingDir;
        }
    }

    private void RegisterPlugins()
    {
        const string pluginManagerName = "Plugin Manager";
        var existingPluginMenus =
            pluginsToolStripMenuItem.DropDownItems.OfType<ToolStripMenuItem>().ToLookup(c => c.Tag)!;

        lock (PluginRegistry.Plugins)
        {
            if (PluginRegistry.Plugins.Count == 0)
            {
                return;
            }

            if (pluginsToolStripMenuItem.DropDownItems.Contains(pluginsLoadingToolStripMenuItem))
            {
                pluginsToolStripMenuItem.DropDownItems.Remove(pluginsLoadingToolStripMenuItem);
            }

            var pluginEntries = PluginRegistry.Plugins
                .OrderByDescending(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase);

            // pluginsToolStripMenuItem.DropDownItems menu already contains at least 2 items:
            //    [1] Separator
            //    [0] Plugin Settings
            // insert all plugins except 'Plugin Manager' above the separator
            foreach (var plugin in pluginEntries)
            {
                // don't add the plugin to the Plugins menu, if already added
                if (existingPluginMenus.Contains(plugin))
                {
                    continue;
                }

                ToolStripMenuItem item = new() { Text = plugin.Name, Image = plugin.Icon, Tag = plugin };
                item.Click += delegate
                {
                    if (plugin.Execute(new GitUiEventArgs(this, UiCommands)))
                    {
                        UiCommands.RepoChangedNotifier.Notify();
                    }
                };

                if (plugin.Name == pluginManagerName)
                {
                    // insert Plugin Manager below the separator
                    pluginsToolStripMenuItem.DropDownItems.Insert(pluginsToolStripMenuItem.DropDownItems.Count - 1,
                        item);
                }
                else
                {
                    // Handle special case where Git Hosting plugin has already been loaded
                    var first = pluginsToolStripMenuItem.DropDownItems[0];
                    var index = first is ToolStripSeparator || string.CompareOrdinal(first.Text, item.Text) >= 0
                        ? 0
                        : 1;

                    pluginsToolStripMenuItem.DropDownItems.Insert(index, item);
                }
            }

            mainMenuStrip?.Refresh();
        }

        // Allow the plugin to perform any self-registration actions
        PluginRegistry.Register(UiCommands);

        UiCommands.RaisePostRegisterPlugin(this);

        UpdatePluginMenu(Module.IsValidGitWorkingDir());
    }

    /// <summary>
    ///     to avoid showing menu items that should not be there during
    ///     the transition from dashboard to repo browser and vice versa
    ///     and reset hotkeys that are shared between mutual exclusive menu items.
    /// </summary>
    private void HideVariableMainMenuItems()
    {
        dashboardToolStripMenuItem.Visible = false;
        repositoryToolStripMenuItem.Visible = false;
        commandsToolStripMenuItem.Visible = false;
        pluginsToolStripMenuItem.Visible = false;
        refreshToolStripMenuItem.ShortcutKeys = Keys.None;
        refreshDashboardToolStripMenuItem.ShortcutKeys = Keys.None;
        _formBrowseMenus.RemoveRevisionGridMainMenuItems();
        mainMenuStrip.Refresh();
    }

    private void InternalInitialize()
    {
        toolPanel.SuspendLayout();
        toolPanel.TopToolStripPanel.SuspendLayout();

        using (WaitCursorScope.Enter())
        {
            // check for updates
            if (AppSettings.CheckForUpdates && AppSettings.LastUpdateCheck.AddDays(7) < DateTime.Now)
            {
                AppSettings.LastUpdateCheck = DateTime.Now;
                FormUpdates updateForm = new(AppSettings.AppVersion);
                updateForm.SearchForUpdatesAndShow(this, false);
            }

            var hasWorkingDir = !string.IsNullOrEmpty(Module.WorkingDir);
            if (hasWorkingDir)
            {
                HideDashboard();
            }
            else
            {
                ShowDashboard();
            }

            var bareRepository = Module.IsBareRepository();
            var isDashboard = _dashboard?.Visible ?? false;
            var validBrowseDir = !isDashboard && Module.IsValidGitWorkingDir();

            branchSelect.Text = validBrowseDir
                ? !string.IsNullOrWhiteSpace(RevisionGridControl.CurrentBranch.Value)
                    ? RevisionGridControl.CurrentBranch.Value
                    : DetachedHeadParser.DetachedBranch
                : "";
            toolStripButtonLevelUp.Enabled = hasWorkingDir && !bareRepository;
            UpdateWorktreeToolStripVisibility();
            CommitInfoTabControl.Visible = validBrowseDir;
            fileExplorerToolStripMenuItem.Enabled = validBrowseDir;
            manageRemoteRepositoriesToolStripMenuItem1.Enabled = validBrowseDir;
            branchSelect.Enabled = validBrowseDir;
            toolStripButtonCommit.Enabled = validBrowseDir && !bareRepository;

            toolStripButtonPull.Enabled = validBrowseDir;
            toolStripButtonPush.Enabled = validBrowseDir;
            toolStripButtonPush.ResetBeforeUpdate();

            if (validBrowseDir)
            {
                UpdateFetchAllVisibility();
            }

            dashboardToolStripMenuItem.Visible = isDashboard;
            pluginsToolStripMenuItem.Visible = validBrowseDir;
            repositoryToolStripMenuItem.Visible = validBrowseDir;
            commandsToolStripMenuItem.Visible = validBrowseDir;
            toolStripFileExplorer.Enabled = validBrowseDir;
            if (!isDashboard)
            {
                refreshToolStripMenuItem.ShortcutKeys = Keys.F5;
            }
            else
            {
                refreshDashboardToolStripMenuItem.ShortcutKeys = Keys.F5;
            }

            UpdatePluginMenu(validBrowseDir);
            gitMaintenanceToolStripMenuItem.Enabled = validBrowseDir;
            editgitignoreToolStripMenuItem1.Enabled = validBrowseDir;
            editGitAttributesToolStripMenuItem.Enabled = validBrowseDir;
            editmailmapToolStripMenuItem.Enabled = validBrowseDir;
            toolStripSplitStash.Enabled = validBrowseDir && !bareRepository;
            _createPullRequestsToolStripMenuItem.Enabled = validBrowseDir;
            _viewPullRequestsToolStripMenuItem.Enabled = validBrowseDir;
            _addUpstreamRemoteToolStripMenuItem.Enabled = validBrowseDir;

            // repositoryToolStripMenuItem.Visible
            if (validBrowseDir)
            {
                manageSubmodulesToolStripMenuItem.Enabled = !bareRepository;
                updateAllSubmodulesToolStripMenuItem.Enabled = !bareRepository;
                synchronizeAllSubmodulesToolStripMenuItem.Enabled = !bareRepository;
                editgitignoreToolStripMenuItem1.Enabled = !bareRepository;
                editGitAttributesToolStripMenuItem.Enabled = !bareRepository;
                editmailmapToolStripMenuItem.Enabled = !bareRepository;
            }

            // commandsToolStripMenuItem.Visible
            if (validBrowseDir)
            {
                commitToolStripMenuItem.Enabled = !bareRepository;
                mergeToolStripMenuItem.Enabled = !bareRepository;
                rebaseToolStripMenuItem1.Enabled = !bareRepository;
                pullToolStripMenuItem1.Enabled = !bareRepository;
                cleanupToolStripMenuItem.Enabled = !bareRepository;
                stashToolStripMenuItem.Enabled = !bareRepository;
                checkoutBranchToolStripMenuItem.Enabled = !bareRepository;
                mergeBranchToolStripMenuItem.Enabled = !bareRepository;
                rebaseToolStripMenuItem.Enabled = !bareRepository;
                applyPatchToolStripMenuItem.Enabled = !bareRepository;
            }

            stashChangesToolStripMenuItem.Enabled = !bareRepository;
            stashStagedToolStripMenuItem.Visible = Module.GitVersion.SupportStashStaged;

            toolsToolStripMenuItem.RefreshState(bareRepository);

            _NO_TRANSLATE_WorkingDir.RefreshContent();

            OnActivate();

            LoadUserMenu();
            toolStripButtonLevelUp.Image = validBrowseDir && Module.SuperprojectModule is not null
                ? Images.NavigateUp
                : Images.SubmodulesManage;

            if (validBrowseDir)
            {
                _windowsJumpListManager.AddToRecent(Module.WorkingDir);

                // add Navigate and View menu
                _formBrowseMenus.ResetMenuCommandSets();
                _formBrowseMenus.AddMenuCommandSet(MainMenuItem.NavigateMenu,
                    RevisionGridControl.MenuCommands.NavigateMenuCommands);
                _formBrowseMenus.AddMenuCommandSet(MainMenuItem.ViewMenu,
                    RevisionGridControl.MenuCommands.ViewMenuCommands);

                _formBrowseMenus.InsertRevisionGridMainMenuItems(repositoryToolStripMenuItem);

                if (AppSettings.ShowAheadBehindData)
                {
                    var currentBranch = RevisionGridControl.CurrentBranch.Value;
                    ThreadHelper.FileAndForget(async () =>
                    {
                        // Always query only current branch here
                        // because, due to race condition with left panel async refresh:
                        // * when there are a lot of branches, we end up here 1st (and so, we want only the current branch data
                        // because getting ahead - behind data for all branches will be (very ?) long
                        // * when there are few branches, we will end up here not in 1st
                        // and the data will be taken from cache (so what we pass as argument is kind of useless)
                        var aheadBehindData =
                            _aheadBehindDataProvider?.GetData(currentBranch);
                        await this.SwitchToMainThreadAsync();
                        toolStripButtonPush.DisplayAheadBehindInformation(aheadBehindData, currentBranch,
                            GetShortcutKeyTooltipString(Command.Push));
                    });
                }

                // Do not steal focus from an open dropdown menu.
                if (!_NO_TRANSLATE_WorkingDir.DropDown.Visible)
                {
                    ActiveControl = RevisionGridControl;
                }
            }

            _windowsJumpListManager.EnableThumbnailToolbar(validBrowseDir);

            UiCommands.RaisePostBrowseInitialize(this);
        }

        toolPanel.TopToolStripPanel.ResumeLayout();
        toolPanel.ResumeLayout();

        return;

        void LoadUserMenu()
        {
            List<ScriptInfo> scripts =
            [
                .. _scriptsManager.GetScripts()
                    .Where(script => script.Enabled && script.OnEvent == ScriptEvent.ShowInUserMenuBar)
            ];

            for (var i = ToolStripScripts.Items.Count - 1; i >= 0; i--)
            {
                if (ToolStripScripts.Items[i].Tag as string == "userscript")
                {
                    ToolStripScripts.Items.RemoveAt(i);
                }
            }

            if (scripts.Count == 0)
            {
                ToolStripScripts.GripStyle = ToolStripGripStyle.Hidden;
                return;
            }

            ToolStripScripts.GripStyle = ToolStripGripStyle.Visible;
            foreach (var script in scripts)
            {
                ToolStripButton button = new()
                {
                    // store scriptname
                    Text = script.Name,
                    Tag = "userscript",
                    Enabled = true,
                    Visible = true,
                    Image = script.GetIcon(),
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
                };

                UpdateTooltipWithShortcut(button, (Command)script.HotkeyCommandIdentifier);

                button.Click += (s, e) => ExecuteCommand(script.HotkeyCommandIdentifier);

                // add to toolstrip
                ToolStripScripts.Items.Add(button);
            }
        }
    }

    private void SetShortcutKeyDisplayStringsFromHotkeySettings()
    {
        // Add shortcuts to the menu items
        commitToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.Commit);
        stashChangesToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.Stash);
        stashStagedToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.StashStaged);
        stashPopToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.StashPop);
        closeToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.CloseRepository);
        checkoutBranchToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.CheckoutBranch);
        branchToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.CreateBranch);
        tagToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.CreateTag);
        mergeBranchToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.MergeBranches);
        pullToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.PullOrFetch);
        pullToolStripMenuItem1.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.PullOrFetch);
        pushToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.Push);
        rebaseToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.Rebase);
        manageWorktreeToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.ManageWorkTrees);

        fileToolStripMenuItem.RefreshShortcutKeys(Hotkeys);
        helpToolStripMenuItem.RefreshShortcutKeys(Hotkeys);
        toolsToolStripMenuItem.RefreshShortcutKeys(Hotkeys);
        _NO_TRANSLATE_WorkingDir.RefreshShortcutKeys(Hotkeys);
        ToolStripFilters.RefreshBrowseDialogShortcutKeys(Hotkeys!);
        ToolStripFilters.RefreshRevisionGridShortcutKeys(GetHotkeys(RevisionGridControl.HotkeySettingsName));

        // Set shortcuts on the Browse toolbar with commands in RevGrid
        RevisionGridControl.SetFilterShortcutKeys(ToolStripFilters);

        // TODO: add more
        UpdateTooltipWithShortcut(toggleLeftPanel, Command.ToggleLeftPanel);
        UpdateTooltipWithShortcut(toolStripButtonCommit, Command.Commit);
        UpdateTooltipWithShortcut(EditSettings, Command.OpenSettings);
        UpdateTooltipWithShortcut(branchSelect, Command.CheckoutBranch);
        UpdateTooltipWithShortcut(toolStripFileExplorer, fileExplorerToolStripMenuItem.ShortcutKeys);
        UpdateTooltipWithShortcut(RefreshButton, Keys.F5);
        UpdateTooltipWithShortcut(userShell, Command.GitBash);
    }

    private void OnActivate()
    {
        // check if we are in the middle of bisect
        notificationBarBisectInProgress.RefreshBisect();

        // check if we are in the middle of an action (merge/rebase/etc.)
        notificationBarGitActionInProgress.RefreshGitAction(
            AppSettings.GitAsyncWhenMinimized || WindowState != FormWindowState.Minimized);
    }

    private void UpdateStashCount()
    {
        if (AppSettings.ShowStashCount && !Module.IsBareRepository())
        {
            ThreadHelper.FileAndForget(async () =>
            {
                // Add a delay to not interfere with GUI updates when switching repository
                await Task.Delay(500);

                var result = Module.GetStashes(true).Count;

                await this.SwitchToMainThreadAsync();

                toolStripSplitStash.Text = $"({result})";
            });
        }
        else
        {
            toolStripSplitStash.Text = string.Empty;
        }
    }

    public override IScriptOptionsProvider GetScriptOptionsProvider()
    {
        if (fileTree.Visible)
        {
            return fileTree.ScriptOptionsProvider;
        }

        if (revisionDiff.Visible)
        {
            return revisionDiff.ScriptOptionsProvider;
        }

        return base.GetScriptOptionsProvider();
    }

    private void FillFileTree(GitRevision? revision)
    {
        // "File Tree" tab implemented using git-grep works for artificial commits, too
        var showFileTreeTab = true;

        if (showFileTreeTab)
        {
            if (TreeTabPage.Parent is null)
            {
                var index = CommitInfoTabControl.TabPages.IndexOf(DiffTabPage);
                DebugHelpers.Assert(index != -1, "TabControl should contain diff tab page");
                CommitInfoTabControl.TabPages.Insert(index + 1, TreeTabPage);
            }
        }
        else
        {
            TreeTabPage.Parent = null;
        }

        if (CommitInfoTabControl.SelectedTab != TreeTabPage ||
            _selectedRevisionUpdatedTargets.HasFlag(UpdateTargets.FileTree))
        {
            return;
        }

        _selectedRevisionUpdatedTargets |= UpdateTargets.FileTree;
        fileTree.DisplayDiffTab(revision is null ? [] : [revision]);
    }

    private void FillDiff(IReadOnlyList<GitRevision> revisions)
    {
        if (CommitInfoTabControl.SelectedTab != DiffTabPage)
        {
            return;
        }

        if (_selectedRevisionUpdatedTargets.HasFlag(UpdateTargets.DiffList))
        {
            return;
        }

        _selectedRevisionUpdatedTargets |= UpdateTargets.DiffList;
        revisionDiff.DisplayDiffTab(revisions);
    }

    private void FillCommitInfo(GitRevision? revision)
    {
        if (_selectedRevisionUpdatedTargets.HasFlag(UpdateTargets.CommitInfo))
        {
            return;
        }

        if (AppSettings.CommitInfoPosition == CommitInfoPosition.BelowList &&
            CommitInfoTabControl.SelectedTab != CommitInfoTabPage)
        {
            return;
        }

        _selectedRevisionUpdatedTargets |= UpdateTargets.CommitInfo;

        if (revision is null)
        {
            return;
        }

        var children = RevisionGridControl.GetRevisionChildren(revision.ObjectId);
        RevisionInfo.SetRevisionWithChildren(revision, children);
    }

    private async Task FillGpgInfoAsync(GitRevision? revision, CancellationToken cancellationToken)
    {
        // Don't show the "GPG" tab for artificial commits
        var showGpgInfoTab = revision?.IsArtificial is false && AppSettings.ShowGpgInformation.Value;

        if (!showGpgInfoTab)
        {
            GpgInfoTabPage.Parent = null;
            return;
        }

        if (GpgInfoTabPage.Parent is null)
        {
            var index = CommitInfoTabControl.TabPages.IndexOf(TreeTabPage);
            Debug.Assert(index != -1, "TabControl should contain file tree tab page");
            CommitInfoTabControl.TabPages.Insert(index + 1, GpgInfoTabPage);
        }

        if (!AppSettings.ShowGpgInformation.Value || CommitInfoTabControl.SelectedTab != GpgInfoTabPage)
        {
            return;
        }

        if (revision is null)
        {
            return;
        }

        // Verifying spawns git and gpg, which can take seconds while the agent is cold. Until it is
        // done, neither the result for the previously selected revision nor the default "not signed"
        // may be presented as if it was verified.
        revisionGpgInfo1.DisplayVerificationPending();

        var info = await _controller.LoadGpgInfoAsync(revision, cancellationToken);

        // A newer selection may have started (and cancelled this token) while awaiting above;
        // avoid overwriting its result with this now-stale verdict.
        cancellationToken.ThrowIfCancellationRequested();
        revisionGpgInfo1.DisplayGpgInfo(info);
    }

    private void RefreshLeftPanel(Func<RefsFilter, IReadOnlyList<IGitRef>> getRefs,
        Lazy<IReadOnlyCollection<GitRevision>> getStashRevs, bool forceRefresh)
    {
        repoObjectsTree.RefreshRevisionsLoading(getRefs, getStashRevs, forceRefresh);
    }

    private void CheckoutToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartCheckoutRevisionDialog(this);
    }

    private void CommitToolStripMenuItemClick(object? sender, EventArgs e)
    {
        this.ForceActivate();
        UiCommands.StartCommitDialog(this);
    }

    private void PushToolStripMenuItemClick(object? sender, EventArgs e)
    {
        this.ForceActivate();
        UiCommands.StartPushDialog(this, ModifierKeys.HasFlag(Keys.Shift));
    }

    private void RefreshToolStripMenuItemClick(object sender, EventArgs e)
    {
        // Broadcast RepoChanged in case repo was changed outside of GE
        UiCommands.RepoChangedNotifier.Notify();
    }

    private void RefreshDashboardToolStripMenuItemClick(object sender, EventArgs e)
    {
        _dashboard?.RefreshContent();
    }

    private void PatchToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartViewPatchDialog(this);
    }

    private void ApplyPatchToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartApplyPatchDialog(this);
    }

    private void userShell_Click(object? sender, EventArgs e)
    {
        if (userShell.DropDownButtonPressed)
        {
            return;
        }

        if ((sender as ToolStripItem)?.Tag is not IShellDescriptor shell)
        {
            return;
        }

        try
        {
            Validates.NotNull(shell.ExecutablePath);

            Executable executable = new(shell.ExecutablePath, Module.WorkingDir);
            executable.Start(createWindow: true, throwOnErrorExit: false); // throwOnErrorExit would redirect the output
        }
        catch (Exception exception)
        {
            MessageBoxes.FailedToRunShell(this, shell.Name, exception);
        }
    }

    private void FormatPatchToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartFormatPatchDialog(this);
    }

    private void CheckoutBranchToolStripMenuItemClick(object? sender, EventArgs e)
    {
        UiCommands.StartCheckoutBranch(this);
    }

    private void StashToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartStashDialog(this);
        UpdateStashCount();
    }

    private void ResetToolStripMenuItem_Click(object sender, EventArgs e)
    {
        UiCommands.StartResetChangesDialog(this, Module.GetWorkTreeFiles(), false);
        RefreshGitStatusMonitor();
        revisionDiff.RefreshArtificial();
    }

    private void RunMergetoolToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartResolveConflictsDialog(this);
    }

    private void CurrentBranchClick(object sender, EventArgs e)
    {
        branchSelect.ShowDropDown();
    }

    private void DeleteBranchToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartDeleteBranchDialog(this, string.Empty);
    }

    private void DeleteTagToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartDeleteTagDialog(this, null);
    }

    private void CherryPickToolStripMenuItemClick(object sender, EventArgs e)
    {
        var revisions = RevisionGridControl.GetSelectedRevisions(SortDirection.Descending);

        UiCommands.StartCherryPickDialog(this, revisions);
    }

    private void MergeBranchToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartMergeBranchDialog(this, null);
    }

    private void toolsToolStripMenuItem_SettingsChanged(object sender, SettingsChangedEventArgs e)
    {
        HandleSettingsChanged(e.OldTranslation, e.OldCommitInfoPosition);
    }

    private void OnShowSettingsClick(object sender, EventArgs e)
    {
        var translation = AppSettings.Translation;
        var commitInfoPosition = AppSettings.CommitInfoPosition;

        // Await plugin registration
        _loadOperations.JoinPendingOperations();

        UiCommands.StartSettingsDialog(this);

        HandleSettingsChanged(translation, commitInfoPosition);
    }

    private void HandleSettingsChanged(string oldTranslation, CommitInfoPosition oldCommitInfoPosition)
    {
        Module?.InvalidateGitSettings();

        if (oldTranslation != AppSettings.Translation)
        {
            Translator.Translate(this, AppSettings.CurrentTranslation);
        }

        if (oldCommitInfoPosition != AppSettings.CommitInfoPosition)
        {
            LayoutRevisionInfo();
        }

        LoadHotkeys(HotkeySettingsName);
        RevisionGridControl.ReloadHotkeys();
        RevisionGridControl.ReloadTranslation();
        RevisionInfo.ReloadHotkeys();
        fileTree.ReloadHotkeys();
        revisionDiff.ReloadHotkeys();
        repoObjectsTree.ReloadHotkeys();
        SetShortcutKeyDisplayStringsFromHotkeySettings();
        AvatarService.UpdateAvatarInitialFontsSettings();

        // Clear the separate caches for diff/merge tools
        ThreadHelper.FileAndForget(() => new CustomDiffMergeToolProvider().ClearAsync(false));
        ThreadHelper.FileAndForget(async () =>
        {
            revisionDiff.CancelLoadCustomDifftools();
            RevisionGridControl.CancelLoadCustomDifftools();

            await new CustomDiffMergeToolProvider().ClearAsync(true);

            // The tool lists are created when the forms are init, must be redone after clearing the cache
            await this.SwitchToMainThreadAsync();
            revisionDiff.LoadCustomDifftools();
            RevisionGridControl.LoadCustomDifftools();
        });

        _dashboard?.RefreshContent();

        _gitStatusMonitor.Active = NeedsGitStatusMonitor() && Module!.IsValidGitWorkingDir();

        RefreshDefaultPullAction();
    }

    private void TagToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartCreateTagDialog(this, RevisionGridControl.LatestSelectedRevision);
    }

    private static void SaveApplicationSettings()
    {
        AppSettings.SaveSettings();
    }

    private void EditGitignoreToolStripMenuItem1Click(object sender, EventArgs e)
    {
        UiCommands.StartEditGitIgnoreDialog(this, false);
    }

    private void EditGitInfoExcludeToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartEditGitIgnoreDialog(this, true);
    }

    private void ArchiveToolStripMenuItemClick(object sender, EventArgs e)
    {
        var revisions = RevisionGridControl.GetSelectedRevisions();
        if (revisions.Count is < 1 or > 2)
        {
            MessageBoxes.SelectOnlyOneOrTwoRevisions(this);
            return;
        }

        var mainRevision = revisions[0];
        var diffRevision = revisions.Count == 2 ? revisions[1] : null;

        UiCommands.StartArchiveDialog(this, mainRevision, diffRevision);
    }

    private void EditMailMapToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartMailMapDialog(this);
    }

    private void EditLocalGitConfigToolStripMenuItemClick(object sender, EventArgs e)
    {
        var fileName = Path.Combine(Module.ResolveGitInternalPath("config"));
        UiCommands.StartFileEditorDialog(fileName, true);
    }

    private void CompressGitDatabaseToolStripMenuItemClick(object sender, EventArgs e)
    {
        FormProcess.ReadDialog(this, UiCommands, "gc", Module.WorkingDir, null, true);
    }

    private void RecoverLostObjectsToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartVerifyDatabaseDialog(this);
    }

    private void ManageRemoteRepositoriesToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartRemotesDialog(this);
    }

    private void RebaseToolStripMenuItemClick(object sender, EventArgs e)
    {
        var revisions = RevisionGridControl.GetSelectedRevisions();

        if (revisions.Count == 0 || revisions[0].IsArtificial)
        {
            return;
        }

        var onto = revisions[0].ObjectId.ToString(); // 2nd selected commit
        if (revisions.Count == 2)
        {
            // Set defaults in rebase form to rebase commits defined by the range *from* first selected commit *to* HEAD
            // *onto* 2nd selected commit
            var from = revisions[1].ObjectId.ToShortString(); // 1st selected commit (excluded from rebase)
            var to = RevisionGridControl.CurrentBranch.Value; // current branch checked out (HEAD)

            UiCommands.StartRebaseDialog(this, from, to, onto, false, false);
        }
        else
        {
            UiCommands.StartRebaseDialog(this, onto);
        }
    }

    private void CommitInfoTabControl_SelectedIndexChanged(object? sender, EventArgs e)
    {
        RefreshSelection();
        FillTerminalTab();
        if (CommitInfoTabControl.SelectedTab == DiffTabPage)
        {
            // workaround to avoid focusing the "filter files" combobox
            revisionDiff.SwitchFocus(false);
        }
        else if (CommitInfoTabControl.SelectedTab == TreeTabPage)
        {
            fileTree.SwitchFocus(false);
        }
    }

    private void ToolStripButtonPushClick(object sender, EventArgs e)
    {
        PushToolStripMenuItemClick(sender, e);
    }

    private void ManageSubmodulesToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartSubmodulesDialog(this);
        UpdateSubmodulesStructure();
    }

    private void UpdateSubmoduleToolStripMenuItemClick(object? sender, EventArgs e)
    {
        if (sender is ToolStripMenuItem toolStripMenuItem)
        {
            var submodule = toolStripMenuItem.Tag as string;
            Validates.NotNull(Module.SuperprojectModule);
            FormProcess.ShowDialog(this, UiCommands, Commands.SubmoduleUpdate(submodule),
                Module.SuperprojectModule.WorkingDir, null, true);
        }

        RefreshRevisions();
    }

    private void UpdateAllSubmodulesToolStripMenuItemClick(object? sender, EventArgs e)
    {
        UiCommands.StartUpdateSubmodulesDialog(this);
        UpdateSubmodulesStructure();
    }

    private void SynchronizeAllSubmodulesToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartSyncSubmodulesDialog(this);
        UpdateSubmodulesStructure();
    }

    private void ToolStripSplitStashButtonClick(object sender, EventArgs e)
    {
        UiCommands.StartStashDialog(this);
        UpdateStashCount();
    }

    private void StashChangesToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StashSave(this, AppSettings.IncludeUntrackedFilesInManualStash);
        UpdateStashCount();
    }

    private void StashStagedToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StashStaged(this);
        UpdateStashCount();
    }

    private void StashPopToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StashPop(this);
        UpdateStashCount();
    }

    private void ManageStashesToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartStashDialog(this);
        UpdateStashCount();
    }

    private void CreateStashToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartStashDialog(this, false);
        UpdateStashCount();
    }

    private void PluginSettingsToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartPluginSettingsDialog(this);
    }

    private void RepoSettingsToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartRepoSettingsDialog(this);
    }

    private void CloseToolStripMenuItemClick(object sender, EventArgs e)
    {
        SetWorkingDir("");
    }

    private void CleanupToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartCleanupRepositoryDialog(this);
    }

    private void SetGitModule(object? sender, GitModuleEventArgs e)
    {
        var originalWorkingDir = Module.WorkingDir;

        HideVariableMainMenuItems();
        PluginRegistry.Unregister(UiCommands);
        _gitStatusMonitor.InvalidateGitWorkingDirectoryStatus();
        _submoduleStatusProvider.Init();

        repoObjectsTree.ClearTrees();

        // Reset branch colors whenever we open a new repository
        e.GitModule.ResetRemoteColors();

        UiCommands = UiCommands.WithGitModule(e.GitModule);
        RevisionGridControl.OnRepositoryChanged();
        if (Module.IsValidGitWorkingDir())
        {
            RevisionGridControl.SuspendRefreshRevisions();
            var path = Module.WorkingDir;
            AppSettings.RecentWorkingDir = path;

            HideDashboard();

            if (!string.Equals(originalWorkingDir, Module.WorkingDir, StringComparison.Ordinal))
            {
                ChangeTerminalActiveFolder(Module.WorkingDir);

#if DEBUG
                // Current encodings
                Debug.WriteLine($"Encodings for {Module.WorkingDir}");
                Debug.WriteLine($"Files content encoding: {Module.FilesEncoding.EncodingName}");
                Debug.WriteLine($"Commit encoding: {Module.CommitEncoding.EncodingName}");
                if (Module.LogOutputEncoding.CodePage != Module.CommitEncoding.CodePage)
                {
                    Debug.WriteLine($"Log output encoding: {Module.LogOutputEncoding.EncodingName}");
                }
#endif

                // Reset the filter when switching repos

                // If we're applying custom branch or revision filters - reset them
                RevisionGridControl.ResetAllFilters();
                ToolStripFilters.ClearQuickFilters();
                revisionDiff.RepositoryChanged();
            }

            RevisionInfo.SetRevisionWithChildren(null, []);
            RevisionGridControl.ResumeRefreshRevisions();

            RefreshRevisions();

            SetShortcutKeyDisplayStringsFromHotkeySettings();
        }
        else
        {
            dashboardToolStripMenuItem.Visible = true;

            MainSplitContainer.Visible = false;
            ShowDashboard();
        }

        _loadOperations.InvokeAndForget(this, () =>
        {
            RegisterPlugins();

            revisionDiff.RegisterGitHostingPluginInBlameControl();
            fileTree.RegisterGitHostingPluginInBlameControl();
        });
    }

    private void FileExplorerToolStripMenuItemClick(object sender, EventArgs e)
    {
        OsShellUtil.OpenWithFileExplorer(Module.WorkingDir);
    }

    private void CreateBranchToolStripMenuItemClick(object sender, EventArgs e)
    {
        UiCommands.StartCreateBranchDialog(this, RevisionGridControl.LatestSelectedRevision?.ObjectId ?? default);
    }

    private void editGitAttributesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        UiCommands.StartEditGitAttributesDialog(this);
    }

    private void deleteIndexLockToolStripMenuItem_Click(object sender, EventArgs e)
    {
        try
        {
            Module.UnlockIndex(true);
        }
        catch (FileDeleteException ex)
        {
            throw new UserExternalOperationException(_indexLockCantDelete.Text,
                new ExternalOperationException(arguments: ex.FileName, workingDirectory: Module.WorkingDir,
                    innerException: ex));
        }
    }

    private void BisectClick(object sender, EventArgs e)
    {
        using (FormBisect frm = new(RevisionGridControl))
        {
            frm.ShowDialog(this);
        }

        RefreshRevisions();
    }

    private void CurrentBranchDropDownOpening(object sender, EventArgs e)
    {
        branchSelect.DropDown.SuspendLayout();
        branchSelect.DropDownItems.Clear();

        AddCheckoutBranchMenuItem();
        branchSelect.DropDownItems.Add(new ToolStripSeparator());
        AddBranchesMenuItems();

        branchSelect.DropDown.ResumeLayout();

        void AddCheckoutBranchMenuItem()
        {
            ToolStripMenuItem checkoutBranchItem = new(checkoutBranchToolStripMenuItem.Text, Images.BranchCheckout)
            {
                ShortcutKeys = GetShortcutKeys(Command.CheckoutBranch),
                ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.CheckoutBranch)
            };

            branchSelect.DropDownItems.Add(checkoutBranchItem);
            checkoutBranchItem.Click += CheckoutBranchToolStripMenuItemClick;
        }

        void AddBranchesMenuItems()
        {
            foreach (var branch in GetBranches())
            {
                if (branch.ObjectId.IsZero)
                {
                    throw new InvalidOperationException($"Branch '{branch.Name}' has no ObjectId.");
                }

                var isBranchVisible = ((ICheckRefs)RevisionGridControl).Contains(branch.ObjectId);

                var toolStripItem = branchSelect.DropDownItems.Add(branch.Name);
                var effectiveBackColor = toolStripItem.BackColor.IsEmpty
                    ? toolStripItem.GetCurrentParent()?.BackColor ?? branchSelect.DropDown.BackColor
                    : toolStripItem.BackColor;
                toolStripItem.ForeColor = isBranchVisible
                    ? branchSelect.ForeColor
                    : Color.Silver.AdaptForeColor(effectiveBackColor);
                toolStripItem.Image = (isBranchVisible ? Images.Branch : Images.EyeClosed).AdaptLightness();
                toolStripItem.Click += (s, e) => UiCommands.StartCheckoutBranch(this, toolStripItem.Text!);
            }

            IEnumerable<IGitRef> GetBranches()
            {
                // Make sure there are never more than a 100 branches added to the menu
                // Git Extensions will hang when the drop down is too large...
                return Module
                    .GetRefs(RefsFilter.Heads)
                    .Take(100);
            }
        }
    }

    private void _forkCloneMenuItem_Click(object? sender, EventArgs e)
    {
        if (PluginRegistry.GitHosters.Count > 0)
        {
            UiCommands.StartCloneForkFromHoster(this, PluginRegistry.GitHosters[0], SetGitModule);
            RefreshRevisions();
        }
        else
        {
            MessageBoxes.ShowError(this, _noReposHostPluginLoaded.Text, TranslatedStrings.Error);
        }
    }

    private void _viewPullRequestsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!TryGetRepositoryHost(out var repoHost))
        {
            return;
        }

        UiCommands.StartPullRequestsDialog(this, repoHost);
    }

    private void _createPullRequestToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!TryGetRepositoryHost(out var repoHost))
        {
            return;
        }

        UiCommands.StartCreatePullRequest(this, repoHost);
    }

    private void _addUpstreamRemoteToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (!TryGetRepositoryHost(out var repoHost))
        {
            return;
        }

        UiCommands.AddUpstreamRemote(this, repoHost);
    }

    private bool TryGetRepositoryHost([NotNullWhen(true)] out IRepositoryHostPlugin? repoHost)
    {
        repoHost = PluginRegistry.TryGetGitHosterForModule(Module);
        if (repoHost is null)
        {
            MessageBoxes.Show(this, _noReposHostFound.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return false;
        }

        return true;
    }

    public static void OpenContainingFolder(FileStatusList diffFiles, IGitModule module)
    {
        if (module.WorkingDir is not { } workindDir)
        {
            return;
        }

        if (diffFiles.SelectedFolder is { } relativePath)
        {
            OpenInContainingFolder(workindDir, relativePath.Length == 0 ? "" : $"{relativePath.Value}/");
            return;
        }

        foreach (var item in diffFiles.SelectedItems)
        {
            OpenInContainingFolder(workindDir, item.Item.Name);
        }

        return;

        static void OpenInContainingFolder(string workingDir, string relativePath)
        {
            var filePath = Path.Combine(workingDir, relativePath.ToNativePath());
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                FormBrowseUtil.ShowFileOrParentFolderInFileExplorer(filePath);
            }
        }
    }

    private void SetSplitterPositions()
    {
        _splitterManager.AddSplitter(RevisionsSplitContainer, nameof(RevisionsSplitContainer));
        _splitterManager.AddSplitter(MainSplitContainer, nameof(MainSplitContainer));
        _splitterManager.AddSplitter(RightSplitContainer, nameof(RightSplitContainer));
        _splitterManager.AddSplitter(LeftSplitContainer, nameof(LeftSplitContainer));

        revisionDiff.InitSplitterManager(_splitterManager);
        fileTree.InitSplitterManager(_splitterManager);

        _splitterManager.RestoreSplitters();
        RefreshLayoutToggleButtonStates();
        if (_isFileHistoryMode)
        {
            _fileBlameHistoryLeftPanelStartupState = MainSplitContainer.Panel1Collapsed;
            MainSplitContainer.Panel1Collapsed = true;
        }

        // Since #8849 and #8557 we have a geometry bug, which pushes the splitter up by 4px.
        // Account for this shift. This is a workaround at best in the same way as for FormCommit.
        if (!RevisionsSplitContainer.Panel2Collapsed && RevisionsSplitContainer.FixedPanel == FixedPanel.Panel2)
        {
            try
            {
                RevisionsSplitContainer.SplitterDistance -= 4;
            }
            catch (Exception)
            {
                // Catching because bad value can raise an exception
            }
        }

        LeftSplitContainer.Panel2Collapsed = !AppSettings.OutputHistoryPanelVisible.Value;

        // Account for shift by 2px as for RevisionsSplitContainer
        if (!LeftSplitContainer.Panel2Collapsed && LeftSplitContainer.FixedPanel == FixedPanel.Panel2)
        {
            try
            {
                LeftSplitContainer.SplitterDistance += 2;
            }
            catch (Exception)
            {
                // Catching because bad value can raise an exception
            }
        }
    }

    private void CommandsToolStripMenuItem_DropDownOpening(object? sender, EventArgs e)
    {
        // Most options do not make sense for artificial commits or no revision selected at all
        var selectedRevisions = RevisionGridControl.GetSelectedRevisions();
        var singleNormalCommit = selectedRevisions.Count == 1 && !selectedRevisions[0].IsArtificial;

        // Some commands like stash, undo commit etc has no relation to selections

        // Require that a single commit is selected
        // Some commands like delete branch could be available for artificial as no default is used,
        // but hide for consistency
        branchToolStripMenuItem.Enabled =
            deleteBranchToolStripMenuItem.Enabled =
                mergeBranchToolStripMenuItem.Enabled =
                    checkoutBranchToolStripMenuItem.Enabled =
                        cherryPickToolStripMenuItem.Enabled =
                            checkoutToolStripMenuItem.Enabled =
                                bisectToolStripMenuItem.Enabled =
                                    singleNormalCommit && !Module.IsBareRepository();

        rebaseToolStripMenuItem.Enabled = selectedRevisions.Count is 1 or 2 &&
                                          selectedRevisions.All(r => !r.IsArtificial) && !Module.IsBareRepository();

        tagToolStripMenuItem.Enabled =
            deleteTagToolStripMenuItem.Enabled =
                archiveToolStripMenuItem.Enabled =
                    singleNormalCommit;

        // Not operating on selected revision
        commitToolStripMenuItem.Enabled =
            undoLastCommitToolStripMenuItem.Enabled =
                runMergetoolToolStripMenuItem.Enabled =
                    stashToolStripMenuItem.Enabled =
                        resetToolStripMenuItem.Enabled =
                            cleanupToolStripMenuItem.Enabled =
                                toolStripMenuItemReflog.Enabled =
                                    applyPatchToolStripMenuItem.Enabled =
                                        !Module.IsBareRepository();
    }

    private void PullToolStripMenuItemClick(object? sender, EventArgs e)
    {
        this.ForceActivate();

        // "Pull/Fetch..." menu item always opens the dialog
        DoPull(AppSettings.FormPullAction, false);
    }

    private void ToolStripButtonPullClick(object sender, EventArgs e)
    {
        // Clicking on the Pull button toolbar button will perform the default selected action silently,
        // except if that action is to open the dialog (PullAction.None)
        var isSilent = AppSettings.DefaultPullAction != GitPullAction.None;
        var pullAction = AppSettings.DefaultPullAction != GitPullAction.None
            ? AppSettings.DefaultPullAction
            : AppSettings.FormPullAction;
        DoPull(pullAction, isSilent);
    }

    private void pullToolStripMenuItem1_Click(object sender, EventArgs e)
    {
        // "Open Pull Dialog..." toolbar menu item always open the dialog with the current default action
        DoPull(AppSettings.FormPullAction, false);
    }

    private void mergeToolStripMenuItem_Click(object sender, EventArgs e)
    {
        DoPull(GitPullAction.Merge, true);
    }

    private void rebaseToolStripMenuItem1_Click(object sender, EventArgs e)
    {
        DoPull(GitPullAction.Rebase, true);
    }

    private void fetchToolStripMenuItem_Click(object sender, EventArgs e)
    {
        DoPull(GitPullAction.Fetch, true);
    }

    private void fetchAllToolStripMenuItem_Click(object sender, EventArgs e)
    {
        DoPull(GitPullAction.FetchAll, true);
    }

    private void fetchPruneAllToolStripMenuItem_Click(object sender, EventArgs e)
    {
        DoPull(GitPullAction.FetchPruneAll, true);
    }

    private void DoPull(GitPullAction pullAction, bool isSilent)
    {
        if (isSilent)
        {
            UiCommands.StartPullDialogAndPullImmediately(this, pullAction: pullAction);
        }
        else
        {
            UiCommands.StartPullDialog(this, pullAction: pullAction);
        }
    }

    private void branchSelect_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            CheckoutBranchToolStripMenuItemClick(sender, e);
        }
    }

    private void RevisionInfo_CommandClicked(object sender, CommandEventArgs e)
    {
        // TODO this code duplicated in FormFileHistory.Blame_CommandClick
        switch (e.Command)
        {
            case "gotocommit":
                Validates.NotNull(e.Data);
                if (!Module.TryResolvePartialCommitId(e.Data, out var commitId) ||
                    !RevisionGridControl.SetSelectedRevision(commitId))
                {
                    if (commitId.IsZero)
                    {
                        return;
                    }

                    // This may occur at various filters, like AppSettings.ShowOnlyFirstParent
                    // will hide other than the first parent.
                    MessageBoxes.RevisionFilteredInGrid(this, commitId);
                }

                break;
            case "gotobranch":
            case "gototag":
                Validates.NotNull(e.Data);
                var commit = _commitDataManager.GetCommitData(e.Data);
                if (commit is null)
                {
                    break;
                }

                if (!RevisionGridControl.SetSelectedRevision(commit.ObjectId))
                {
                    MessageBoxes.RevisionFilteredInGrid(this, commit.ObjectId);
                }

                break;
            case "navigatebackward":
                RevisionGridControl.NavigateBackward();
                break;
            case "navigateforward":
                RevisionGridControl.NavigateForward();
                break;
            default:
                throw new InvalidOperationException($"unexpected internal link: {e.Command}/{e.Data}");
        }
    }

    private void SubmoduleToolStripButtonClick(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem menuSender)
        {
            return;
        }

        var path = menuSender.Tag as string;
        if (!Directory.Exists(path))
        {
            MessageBoxes.SubmoduleDirectoryDoesNotExist(this, path!);
            return;
        }

        SetWorkingDir(path);
    }

    private void toolStripButtonLevelUp_ButtonClick(object? sender, EventArgs e)
    {
        if (Module.SuperprojectModule is not null)
        {
            SetGitModule(this, new GitModuleEventArgs(Module.SuperprojectModule));
        }
        else
        {
            toolStripButtonLevelUp.ShowDropDown();
        }
    }

    /// <summary>
    ///     Adds a tab with console interface to Git over the current working copy. Recreates the terminal on tab activation if
    ///     user exits the shell.
    ///     Uses the configured console emulator (plugin or ConEmu).
    /// </summary>
    private void FillTerminalTab()
    {
        if (!AppSettings.ShowConEmuTab.Value)
        {
            return;
        }

        // If terminal control already exists, just focus it
        if (_terminal is not null)
        {
            _terminal.FocusTerminal();
            return;
        }

        if (_consoleTabPage is not null)
        {
            // Tab page already created
            return;
        }

        // Check if there are available console emulators
        if (_consoleEmulatorsRegistry.AvailableConsoleEmulators.Count == 0)
        {
            return;
        }

        _consoleTabPage = new TabPage { Text = _consoleTabCaption.Text, Name = _consoleTabCaption.Text };
        CommitInfoTabControl.Controls.Add(_consoleTabPage);

        // We have to set ImageKey after it's added to the tab control
        _consoleTabPage.ImageKey = nameof(Images.Console);

        // Delay-create the terminal window when the tab is first selected
        CommitInfoTabControl.Selecting += (sender, args) =>
        {
            if (args.TabPage != _consoleTabPage)
            {
                return;
            }

            if (_terminal is null)
            {
                _terminal = _consoleEmulatorsRegistry.CreateShellRunner();
                if (_terminal is null)
                {
                    return;
                }

                _terminal.Control.Dock = DockStyle.Fill;
                _consoleTabPage!.Controls.Add(_terminal.Control);
            }

            if (_terminal.IsShellRunning)
            {
                _terminal.FocusTerminal();
                return;
            }

            _terminal.StartShell(Module.WorkingDir);
        };
    }

    public void ChangeTerminalActiveFolder(string path)
    {
        if (_terminal?.IsShellRunning is not true)
        {
            return;
        }

        _terminal.ChangeWorkingDirectory(path);
    }

    private void menuitemSparseWorkingCopy_Click(object sender, EventArgs e)
    {
        UiCommands.StartSparseWorkingCopyDialog(this);
    }

    private void toolStripMenuItemReflog_Click(object sender, EventArgs e)
    {
        using FormReflog formReflog = new(UiCommands);
        formReflog.ShowDialog();
    }

    private void manageWorktreeToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        using FormManageWorktree formManageWorktree = new(UiCommands);
        formManageWorktree.ShowDialog(this);
        if (formManageWorktree.ShouldRefreshRevisionGrid)
        {
            RefreshRevisions();
        }
    }

    private void toolStripWorktrees_ButtonClick(object sender, EventArgs e)
    {
        manageWorktreeToolStripMenuItem_Click(sender, e);
    }

    private void toolStripWorktrees_DropDownOpening(object sender, EventArgs e)
    {
        toolStripWorktrees.DropDown.SuspendLayout();
        toolStripWorktrees.DropDownItems.Clear();

        var currentWorkingDir = Module.WorkingDir.TrimEnd(Path.DirectorySeparatorChar);
        var worktrees = Module.GetWorktrees();

        foreach (var worktree in worktrees)
        {
            var isCurrent = string.Equals(
                worktree.Path.TrimEnd(Path.DirectorySeparatorChar),
                currentWorkingDir,
                StringComparison.OrdinalIgnoreCase);

            var displayName = worktree.GetDisplayName(
                Path.GetFileName(worktree.Path.TrimEnd(Path.DirectorySeparatorChar)));
            ToolStripMenuItem item = new(displayName)
            {
                Tag = worktree.Path,
                Image = Images.WorkTree,
                Checked = isCurrent,
                Enabled = !isCurrent && !worktree.IsDeleted
            };

            if (worktree.IsDeleted)
            {
                item.ForeColor = SystemColors.GrayText;
            }

            item.Click += WorktreeToolStripMenuItem_Click;
            toolStripWorktrees.DropDownItems.Add(item);
        }

        toolStripWorktrees.DropDownItems.Add(new ToolStripSeparator());

        ToolStripMenuItem createItem = new(TranslatedStrings.CreateWorktree, Images.WorkTree);
        createItem.Click += (_, _) =>
        {
            var mainPath = worktrees.Count > 0 ? worktrees[0].Path : Module.WorkingDir;
            if (UiCommands.WorktreeCreate(this, mainPath))
            {
                RefreshRevisions();
            }
        };
        toolStripWorktrees.DropDownItems.Add(createItem);

        ToolStripMenuItem pruneItem = new(TranslatedStrings.PruneWorktrees);
        pruneItem.Click += (_, _) =>
        {
            if (UiCommands.StartCommandLineProcessDialog(this, null, "worktree prune"))
            {
                RefreshRevisions();
            }
        };
        toolStripWorktrees.DropDownItems.Add(pruneItem);

        ToolStripMenuItem manageItem = new(TranslatedStrings.ManageWorktrees);
        manageItem.Click += manageWorktreeToolStripMenuItem_Click;
        toolStripWorktrees.DropDownItems.Add(manageItem);

        toolStripWorktrees.DropDown.ResumeLayout();
    }

    private void WorktreeToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem { Tag: string path })
        {
            return;
        }

        if (!Directory.Exists(path))
        {
            MessageBoxes.ShowError(this, string.Format(TranslatedStrings.WorktreeDirectoryNotFound, path),
                TranslatedStrings.Error);
            return;
        }

        SetWorkingDir(Path.GetFullPath(path));
    }

    private void undoLastCommitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var confirmed = MessageBoxes.ConfirmSuppressible(this, _undoLastCommitText.Text, _undoLastCommitCaption.Text,
            AppSettings.DontConfirmUndoLastCommit, icon: TaskDialogIcon.Warning);

        if (confirmed)
        {
            var args = Commands.Reset(ResetMode.Soft, "HEAD~1");
            Module.GitExecutable.RunCommand(args);
            refreshToolStripMenuItem.PerformClick();
            RefreshGitStatusMonitor();
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    private void FormBrowse_DragDrop(object? sender, DragEventArgs e)
    {
        HandleDrop(e);
    }

    private void HandleDrop(DragEventArgs e)
    {
        if (TreeTabPage.Parent is null)
        {
            return;
        }

        {
            var itemPath = (e.Data!.GetData(DataFormats.Text) ?? e.Data.GetData(DataFormats.UnicodeText)) as string;
            if (GetRelativePathExistingInRepo(itemPath) is { } relativePath)
            {
                fileTree.SelectFileOrFolder(FocusView, relativePath);
                return;
            }
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string?[] paths)
        {
            return;
        }

        foreach (var path in paths)
        {
            if (GetRelativePathExistingInRepo(path) is not { } relativePath)
            {
                continue;
            }

            fileTree.SelectFileOrFolder(FocusView, relativePath);
            return;
        }

        return;

        void FocusView()
        {
            CommitInfoTabControl.SelectedTab = TreeTabPage;
        }

        RelativePath? GetRelativePathExistingInRepo(string? path)
        {
            if (path is null
                || !(File.Exists(path) || Directory.Exists(path))
                || !path.StartsWith(Module.WorkingDir, StringComparison.InvariantCultureIgnoreCase))
            {
                return null;
            }

            var workingDir = Module.WorkingDir;
            var relativePathStartIndex = workingDir.EndsWith(Path.DirectorySeparatorChar)
                ? workingDir.Length
                : workingDir.Length + 1;
            return RelativePath.From(path[relativePathStartIndex..].ToPosixPath());
        }
    }

    private static void FormBrowse_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data!.GetDataPresent(DataFormats.FileDrop)
            || e.Data.GetDataPresent(DataFormats.Text)
            || e.Data.GetDataPresent(DataFormats.UnicodeText))
        {
            e.Effect = DragDropEffects.Move;
        }
    }

    private void fileToolStripMenuItem_RecentRepositoriesCleared(object sender, EventArgs e)
    {
        _dashboard?.RefreshContent();
    }

    internal readonly struct TestAccessor(FormBrowse form)
    {
        public FullBleedTabControl CommitInfoTabControl
        {
            get => form.CommitInfoTabControl;
        }

        public TabPage DiffTabPage
        {
            get => form.DiffTabPage;
        }

        public RepoObjectsTree RepoObjectsTree
        {
            get => form.repoObjectsTree;
        }

        public RevisionDiffControl RevisionDiffControl
        {
            get => form.revisionDiff;
        }

        public RevisionDiffControl RevisionFileTreeControl
        {
            get => form.fileTree;
        }

        public RevisionGridControl RevisionGrid
        {
            get => form.RevisionGridControl;
        }

        public SplitContainer RevisionsSplitContainer
        {
            get => form.RevisionsSplitContainer;
        }

        public SplitContainer RightSplitContainer
        {
            get => form.RightSplitContainer;
        }

        public SplitterManager SplitterManager
        {
            get => form._splitterManager;
        }

        public TabPage TreeTabPage
        {
            get => form.TreeTabPage;
        }

        public FilterToolBar ToolStripFilters
        {
            get => form.ToolStripFilters;
        }

        public void RefreshRevisions()
        {
            form.RefreshRevisions();
        }
    }

    #region Mnemonics

    /*
        MENUS
        ═════
                ABEFGIJKLMOQUWXYZ
            C   Commands
            D   Dashboard
            H   Help
            N   Navigate (inserted by FormBrowseMenus)
            P   Plugins
            R   Repository
            S   Start
            T   Tools
            V   View (inserted by FormBrowseMenus)
                GitHub (inserted dynamically)

        START menu
        ══════════
                ABDEGHIJKMNPQSTUVWYZ
            C   Create new repository...
            F   Favorite repositories
            L   Clone repository...
            O   Open...
            R   Recent repositories
            X   Exit

        DASHBOARD menu
        ══════════════
            R   Refresh
            S   Recent repositories settings

        REPOSITORY menu
        ═══════════════
                DFHJLNPQVYZ
            A   Edit .gitattributes
            B   Synchronize all submodules
            C   Close (go to Dashboard)
            E   Edit .git/info/exclude
            G   Git maintenance
            I   Edit .gitignore
            K   Sparse Working Copy
            M   Edit .mailmap
            O   Repository settings
            R   Refresh
            S   Manage submodules...
            T   Remote repositories...
            U   Update all submodules
            W   Worktrees
            X   File Explorer

        REPOSITORY ▷ WORKTREES submenu
        ══════════════════════════════
            C   Create a worktree...
            M   Manage worktrees...

        REPOSITORY ▷ GIT MAINTENANCE submenu
        ════════════════════════════════════
            C   Compress git database
            R   Recover lost objects...
            D   Delete index.lock
            E   Edit .git/config

        COMMANDS menu
        ═════════════
                JQXZ
            /   Pull/Fetch...
            A   Apply patch...
            B   Create branch...
            C   Commit...
            D   Delete tag...
            E   Rebase...
            F   Format patch...
            G   Show reflog...
            H   View patch file...
            I   Bisect...
            K   Checkout branch...
            L   Delete branch...
            M   Merge branches...
            N   Manage stashes...
            O   Checkout revision...
            P   Push...
            R   Reset changes...
            S   Solve merge conflicts...
            T   Create tag...
            U   Undo last commit
            V   Archive revision...
            W   Clean working directory...
            Y   Cherry pick...

        GITHUB (Repository hosts) menu
        ══════════════════════════════
                BDEGHIJKLMNOQRSTUVWXYZ
            A   Add upstream remote
            C   Create pull requests...
            F   Fork/Clone repository
            P   View pull requests...

        PLUGINS menu
        ════════════
            S   Plugin Settings

        TOOLS menu
        ══════════
                ADEFHIJLMNOQRTUVWXYZ
            B   Git bash
            C   Git command log
            G   Git GUI
            K   GitK
            P   PuTTY
            S   Settings

        HELP menu
        ═════════
                BEFGHIJKLNOPQSVWXZ
            A   About
            C   Changelog
            D   Donate
            M   User manual
            R   Report an issue
            T   Translate
            U   Check for updates
            Y   Yes, I allow telemetry
    */

    #endregion

    #region Translation

    private readonly TranslationString _closeAll = new("Close all windows");

    private readonly TranslationString _noSubmodulesPresent = new("No submodules");
    private readonly TranslationString _topProjectModuleFormat = new("Top project: {0}");
    private readonly TranslationString _superprojectModuleFormat = new("Superproject: {0}");
    private readonly TranslationString _goToSuperProject = new("Go to superproject");

    private readonly TranslationString _indexLockCantDelete = new("Failed to delete index.lock");

    private readonly TranslationString _loading = new("Loading...");

    private readonly TranslationString _noReposHostPluginLoaded = new("No repository host plugin loaded.");

    private readonly TranslationString _noReposHostFound =
        new("Could not find any relevant repository hosts for the currently open repository.");

    private readonly TranslationString _updateCurrentSubmodule = new("Update current submodule");

    private readonly TranslationString _pullFetch = new("Fetch");
    private readonly TranslationString _pullFetchAll = new("Fetch all");
    private readonly TranslationString _pullFetchPruneAll = new("Fetch and prune all");
    private readonly TranslationString _pullMerge = new("Pull - merge");
    private readonly TranslationString _pullRebase = new("Pull - rebase");
    private readonly TranslationString _pullOpenDialog = new("Open pull dialog");

    private readonly TranslationString _buildReportTabCaption = new("Build Report");
    private readonly TranslationString _consoleTabCaption = new("Console");
    private readonly TranslationString _outputHistoryTabCaption = new("Output");

    private readonly TranslationString _commitButtonText = new("Commit");

    private readonly TranslationString _undoLastCommitText =
        new("You will still be able to find all the commit's changes in the staging area\n\nDo you want to continue?");

    private readonly TranslationString _undoLastCommitCaption = new("Undo last commit");

    #endregion

    #region IBrowseRepo

    GitRevision? IBrowseRepo.GetLatestSelectedRevision()
    {
        return RevisionGridControl.LatestSelectedRevision;
    }

    IReadOnlyList<GitRevision> IBrowseRepo.GetSelectedRevisions()
    {
        return RevisionGridControl.GetSelectedRevisions();
    }

    Point IBrowseRepo.GetQuickItemSelectorLocation()
    {
        return RevisionGridControl.GetQuickItemSelectorLocation();
    }

    void IBrowseRepo.GoToRef(string refName, bool showNoRevisionMsg, bool toggleSelection = false)
    {
        RevisionGridControl.GoToRef(refName, showNoRevisionMsg, toggleSelection);
    }

    #endregion

    #region Hotkey commands

    public static readonly string HotkeySettingsName = "Browse";

    internal enum Command
    {
        // Focus or visuals
        FocusLeftPanel = 25,
        FocusRevisionGrid = 3,
        FocusCommitInfo = 4,
        FocusDiff = 5,
        FocusFileTree = 6,
        FocusGpgInfo = 26,
        FocusGitConsole = 29,
        FocusBuildServerStatus = 30,
        FocusOutputHistoryAndToggleIfPanel = 47,
        FocusNextTab = 31,
        FocusPrevTab = 32,

        FocusFilter = 18,

        ToggleLeftPanel = 21,

        // START menu
        OpenRepo = 45,

        // DASHBOARD menu

        // REPOSITORY menu
        CloseRepository = 15,
        ManageWorkTrees = 49,

        // COMMANDS menu
        Commit = 7,
        CheckoutBranch = 10,
        PullOrFetch = 39,
        Push = 40,
        CreateBranch = 41,
        MergeBranches = 42,
        CreateTag = 43,
        Rebase = 44,

        // PLUGINS menu

        // TOOLS menu
        GitBash = 0,
        GitGui = 1,
        GitGitK = 2,
        OpenSettings = 20,

        // HELP menu

        // Toolbar
        AddNotes = 8,
        FindFileInSelectedCommit = 9,
        QuickPullOrFetch = 48, // Default user action configured in toolbar
        QuickFetch = 11,
        QuickPull = 12,
        QuickPush = 13,
        Stash = 16,
        StashStaged = 46,
        StashPop = 17,
        GoToSuperproject = 27,
        GoToSubmodule = 28,

        // Diff or File Tree tab
        OpenWithDifftool = 19,
        EditFile = 22,
        OpenAsTempFile = 23,
        OpenAsTempFileWith = 24,
        OpenWithDifftoolFirstToLocal = 33,
        OpenWithDifftoolSelectedToLocal = 34,

        // Revision grid
        OpenCommitsWithDifftool = 35,
        ToggleBetweenArtificialAndHeadCommits = 36,
        GoToChild = 37,
        GoToParent = 38

        /* deprecated: RotateApplicationIcon = 14, */
    }

    private void AddNotes()
    {
        var revision = RevisionGridControl.GetSelectedRevisionOrDefault();
        if (revision?.IsArtificial is not false)
        {
            return;
        }

        Module.EditNotes(revision.ObjectId);
        FillCommitInfo(revision);
    }

    private void FindFileInSelectedCommit()
    {
        var selectedRevisions = RevisionGridControl.GetSelectedRevisions();
        if (selectedRevisions.Count > 1 || (selectedRevisions.Count == 1 && selectedRevisions[0].IsArtificial))
        {
            var potentialRevision = selectedRevisions[0];
            var targetCommit = potentialRevision.IsArtificial
                ? RevisionGridControl.CurrentCheckout
                : potentialRevision.ObjectId;
            RevisionGridControl.SetSelectedRevision(targetCommit);
        }

        CommitInfoTabControl.SelectedTab = TreeTabPage;

        AppSettings.ShowSplitViewLayout = true;
        RefreshSplitViewLayout();

        fileTree.ExecuteCommand(RevisionDiffControl.Command.FindFile);
    }

    private void QuickFetch()
    {
        var success = ScriptsRunner.RunEventScripts(ScriptEvent.BeforeFetch, this);
        if (!success)
        {
            return;
        }

        success = FormProcess.ShowDialog(this, UiCommands, Module.FetchCmd(string.Empty, string.Empty, string.Empty),
            Module.WorkingDir, null, true);
        if (!success)
        {
            return;
        }

        ScriptsRunner.RunEventScripts(ScriptEvent.AfterFetch, this);
        RefreshRevisions();
    }

    public override bool ProcessHotkey(Keys keyData)
    {
        if (IsDesignMode || !HotkeysEnabled)
        {
            return false;
        }

        // generic handling of this form's hotkeys (upstream)
        if (base.ProcessHotkey(keyData))
        {
            return true;
        }

        // downstream (without keys for quick search and without keys for text selection and copy e.g. in CommitInfo)
        // but allow routing Ctrl+A away from RevisionGridControl in order to not select all revisions
        if (GitExtensionsControl.IsTextEditKey(keyData)
            && !(keyData == (Keys.Control | Keys.A) && RevisionGridControl.ContainsFocus))
        {
            return false;
        }

        // route to visible controls which have their own hotkeys
        return (keyData != (Keys.Control | Keys.A) && RevisionGridControl.ProcessHotkey(keyData))
               || (CommitInfoTabControl.SelectedTab == DiffTabPage && revisionDiff.ProcessHotkey(keyData))
               || (CommitInfoTabControl.SelectedTab == TreeTabPage && fileTree.ProcessHotkey(keyData));
    }

    protected override bool ExecuteCommand(int cmd)
    {
        switch ((Command)cmd)
        {
            case Command.GitBash: userShell.PerformButtonClick(); break;
            case Command.GitGui: Module.RunGui(); break;
            case Command.GitGitK: Module.RunGitK(); break;
            case Command.FocusLeftPanel: FocusLeftPanel(); break;
            case Command.FocusRevisionGrid: RevisionGridControl.Focus(); break;
            case Command.FocusCommitInfo: FocusCommitInfo(); break;
            case Command.FocusDiff:
                FocusTabOf(revisionDiff, (c, alreadyContainedFocus) => c.SwitchFocus(alreadyContainedFocus)); break;
            case Command.FocusFileTree:
                FocusTabOf(fileTree, (c, alreadyContainedFocus) => c.SwitchFocus(alreadyContainedFocus)); break;
            case Command.FocusGpgInfo
                when AppSettings.ShowGpgInformation.Value:
                FocusTabOf(revisionGpgInfo1, (c, alreadyContainedFocus) => c.Focus()); break;
            case Command.FocusGitConsole: FocusGitConsole(); break;
            case Command.FocusBuildServerStatus:
                FocusTabOf(_buildReportTabPageExtension?.Control, (c, alreadyContainedFocus) => c.Focus()); break;
            case Command.FocusOutputHistoryAndToggleIfPanel: return _outputHistoryController!.FocusAndToggleIfPanel();
            case Command.FocusNextTab: FocusNextTab(); break;
            case Command.FocusPrevTab: FocusNextTab(false); break;
            case Command.FocusFilter: ToolStripFilters.SetFocus(); break;
            case Command.OpenRepo: fileToolStripMenuItem.OpenRepositoryMenuItem.PerformClick(); break;
            case Command.Commit: UiCommands.StartCommitDialog(this); break;
            case Command.AddNotes: AddNotes(); break;
            case Command.FindFileInSelectedCommit: FindFileInSelectedCommit(); break;
            case Command.CheckoutBranch: UiCommands.StartCheckoutBranch(this); break;
            case Command.QuickFetch: QuickFetch(); break;
            case Command.QuickPull: DoPull(GitPullAction.Merge, true); break;
            case Command.QuickPullOrFetch: toolStripButtonPull.PerformButtonClick(); break;
            case Command.QuickPush: UiCommands.StartPushDialog(this, true); break;
            case Command.CloseRepository: SetWorkingDir(""); break;
            case Command.Stash: UiCommands.StashSave(this, AppSettings.IncludeUntrackedFilesInManualStash); break;
            case Command.StashStaged: UiCommands.StashStaged(this); break;
            case Command.StashPop: UiCommands.StashPop(this); break;
            case Command.OpenCommitsWithDifftool: RevisionGridControl.DiffSelectedCommitsWithDifftool(); break;
            case Command.OpenWithDifftool: OpenWithDifftool(); break;
            case Command.OpenWithDifftoolFirstToLocal: OpenWithDifftoolFirstToLocal(); break;
            case Command.OpenWithDifftoolSelectedToLocal: OpenWithDifftoolSelectedToLocal(); break;
            case Command.OpenSettings: EditSettings.PerformClick(); break;
            case Command.ToggleLeftPanel: toggleLeftPanel.PerformClick(); break;
            case Command.EditFile: EditFile(); break;
            case Command.OpenAsTempFile
                when fileTree.Visible: fileTree.ExecuteCommand(RevisionDiffControl.Command.OpenAsTempFile); break;
            case Command.OpenAsTempFileWith
                when fileTree.Visible: fileTree.ExecuteCommand(RevisionDiffControl.Command.OpenAsTempFileWith); break;
            case Command.GoToSuperproject: toolStripButtonLevelUp.PerformClick(); break;
            case Command.GoToSubmodule: toolStripButtonLevelUp.ShowDropDown(); break;
            case Command.ToggleBetweenArtificialAndHeadCommits:
                RevisionGridControl?.ExecuteCommand(RevisionGridControl.Command.ToggleBetweenArtificialAndHeadCommits);
                break;
            case Command.GoToChild:
                RestoreFileStatusListFocus(() =>
                    RevisionGridControl?.ExecuteCommand(RevisionGridControl.Command.GoToChild)); break;
            case Command.GoToParent:
                RestoreFileStatusListFocus(() =>
                    RevisionGridControl?.ExecuteCommand(RevisionGridControl.Command.GoToParent)); break;
            case Command.PullOrFetch: DoPull(AppSettings.FormPullAction, false); break;
            case Command.Push: UiCommands.StartPushDialog(this, ModifierKeys.HasFlag(Keys.Shift)); break;
            case Command.CreateBranch:
                UiCommands.StartCreateBranchDialog(this,
                    RevisionGridControl.LatestSelectedRevision?.ObjectId ?? default); break;
            case Command.MergeBranches: UiCommands.StartMergeBranchDialog(this, null); break;
            case Command.CreateTag:
                UiCommands.StartCreateTagDialog(this, RevisionGridControl.LatestSelectedRevision); break;
            case Command.Rebase: rebaseToolStripMenuItem.PerformClick(); break;
            case Command.ManageWorkTrees: manageWorktreeToolStripMenuItem.PerformClick(); break;
            default: return base.ExecuteCommand(cmd);
        }

        return true;

        void FocusLeftPanel()
        {
            if (!MainSplitContainer.Panel1Collapsed)
            {
                repoObjectsTree.Focus();
            }
        }

        void FocusCommitInfo()
        {
            if (AppSettings.CommitInfoPosition == CommitInfoPosition.BelowList)
            {
                CommitInfoTabControl.SelectedTab = CommitInfoTabPage;
            }

            RevisionInfo.Focus();
        }

        void FocusTabOf<T>(T? control, Action<T, bool> switchFocus) where T : Control
        {
            if (control is not null)
            {
                var tabPage = control.Parent as TabPage;
                if (CommitInfoTabControl.TabPages.IndexOf(tabPage!) >= 0)
                {
                    var alreadyContainedFocus = control.ContainsFocus;

                    if (CommitInfoTabControl.SelectedTab != tabPage)
                    {
                        CommitInfoTabControl.SelectedTab = tabPage;
                    }

                    switchFocus(control, alreadyContainedFocus);
                }
            }
        }

        void FocusGitConsole()
        {
            FillTerminalTab();
            if (_consoleTabPage is not null && CommitInfoTabControl.TabPages.Contains(_consoleTabPage))
            {
                CommitInfoTabControl.SelectedTab = _consoleTabPage;
            }
        }

        void FocusNextTab(bool forward = true)
        {
            var tabIndex = CommitInfoTabControl.SelectedIndex;
            tabIndex += forward ? 1 : CommitInfoTabControl.TabCount - 1;
            CommitInfoTabControl.SelectedIndex = tabIndex % CommitInfoTabControl.TabCount;
        }

        void OpenWithDifftool()
        {
            if (revisionDiff.Visible)
            {
                revisionDiff.ExecuteCommand(RevisionDiffControl.Command.OpenWithDifftool);
            }
            else if (fileTree.Visible)
            {
                fileTree.ExecuteCommand(RevisionDiffControl.Command.OpenWithDifftool);
            }
        }

        void OpenWithDifftoolFirstToLocal()
        {
            if (revisionDiff.Visible)
            {
                revisionDiff.ExecuteCommand(RevisionDiffControl.Command.OpenWithDifftoolFirstToLocal);
            }
        }

        void OpenWithDifftoolSelectedToLocal()
        {
            if (revisionDiff.Visible)
            {
                revisionDiff.ExecuteCommand(RevisionDiffControl.Command.OpenWithDifftoolSelectedToLocal);
            }
        }

        void EditFile()
        {
            if (revisionDiff.Visible)
            {
                revisionDiff.ExecuteCommand(RevisionDiffControl.Command.EditFile);
            }
            else if (fileTree.Visible)
            {
                fileTree.ExecuteCommand(RevisionDiffControl.Command.EditFile);
            }
        }

        void RestoreFileStatusListFocus(Action action)
        {
            var restoreFocus = revisionDiff.ContainsFocus;

            action();

            if (restoreFocus)
            {
                revisionDiff.SwitchFocus(false);
            }
        }
    }

    internal bool ExecuteCommand(Command cmd)
    {
        return ExecuteCommand((int)cmd);
    }

    #endregion

    #region Submodules

    private ToolStripItem CreateSubmoduleMenuItem(SubmoduleInfo info, string textFormat = "{0}")
    {
        ToolStripMenuItem item = new(string.Format(textFormat, info.Text))
        {
            Width = 200, Tag = info.Path, Image = Images.FolderSubmodule
        };

        if (info.Bold)
        {
            item.Font = new Font(item.Font, FontStyle.Bold);
        }

        item.Click += SubmoduleToolStripButtonClick;

        return item;
    }

    private static void UpdateSubmoduleMenuItemStatus(ToolStripItem item, SubmoduleInfo info, string textFormat = "{0}")
    {
        if (info.Detailed is not null)
        {
            item.Image = GetSubmoduleItemImage(info.Detailed);
            item.Text = string.Format(textFormat, info.Text + info.Detailed.AddedAndRemovedText);
        }

        return;

        static Image GetSubmoduleItemImage(DetailedSubmoduleInfo details)
        {
            return (details.Status, details.IsDirty) switch
            {
                (null, _) => Images.FolderSubmodule,
                (SubmoduleStatus.FastForward, true) => Images.SubmoduleRevisionUpDirty,
                (SubmoduleStatus.FastForward, false) => Images.SubmoduleRevisionUp,
                (SubmoduleStatus.Rewind, true) => Images.SubmoduleRevisionDownDirty,
                (SubmoduleStatus.Rewind, false) => Images.SubmoduleRevisionDown,
                (SubmoduleStatus.NewerTime, true) => Images.SubmoduleRevisionSemiUpDirty,
                (SubmoduleStatus.NewerTime, false) => Images.SubmoduleRevisionSemiUp,
                (SubmoduleStatus.OlderTime, true) => Images.SubmoduleRevisionSemiDownDirty,
                (SubmoduleStatus.OlderTime, false) => Images.SubmoduleRevisionSemiDown,
                (_, true) => Images.SubmoduleDirty,
                (_, false) => Images.FileStatusModified
            };
        }
    }

    private void UpdateSubmodulesStructure()
    {
        // Submodule status is updated on git-status updates. To make sure supermodule status is updated, update immediately (once)
        var updateStatus = AppSettings.ShowSubmoduleStatus && _gitStatusMonitor.Active;

        toolStripButtonLevelUp.ToolTipText = "";

        ThreadHelper.FileAndForget(async () =>
        {
            try
            {
                await _submoduleStatusProvider.UpdateSubmodulesStructureAsync(Module.WorkingDir,
                    TranslatedStrings.NoBranch, updateStatus);
            }
            catch (GitConfigurationException ex)
            {
                await this.SwitchToMainThreadAsync();
                MessageBoxes.ShowGitConfigurationExceptionMessage(this, ex);
            }
        });
    }

    private void SubmoduleStatusProvider_StatusUpdating(object? sender, EventArgs e)
    {
        this.InvokeAndForget(() =>
        {
            RemoveSubmoduleButtons();
            toolStripButtonLevelUp.DropDownItems.Add(_loading.Text);
        });
    }

    private void SubmoduleStatusProvider_StatusUpdated(object? sender, SubmoduleStatusEventArgs e)
    {
        this.InvokeAndForget(() =>
            {
                if (e.StructureUpdated || _currentSubmoduleMenuItems is null)
                {
                    _currentSubmoduleMenuItems = PopulateToolbar(e.Info);
                }

                UpdateSubmoduleMenuStatus(e.Info);
            },
            cancellationToken: e.Token);
    }

    private List<ToolStripItem> PopulateToolbar(SubmoduleInfoResult result)
    {
        // Second task: Populate submodule toolbar menu on UI thread.
        // Suspend before clearing dropdowns to show loading text until updated
        toolStripButtonLevelUp.DropDown.SuspendLayout();
        RemoveSubmoduleButtons();

        List<ToolStripItem> newItems =
            [.. result.OurSubmodules.Select(submodule => CreateSubmoduleMenuItem(submodule))];

        if (result.OurSubmodules.Count == 0)
        {
            newItems.Add(new ToolStripMenuItem(_noSubmodulesPresent.Text));
        }

        if (result.SuperProject is not null)
        {
            newItems.Add(new ToolStripSeparator());

            // Show top project only if it's not our super project
            if (result.TopProject is not null && result.TopProject != result.SuperProject)
            {
                newItems.Add(CreateSubmoduleMenuItem(result.TopProject, _topProjectModuleFormat.Text));
            }

            newItems.Add(CreateSubmoduleMenuItem(result.SuperProject, _superprojectModuleFormat.Text));
            newItems.AddRange(result.AllSubmodules.Select(submodule => CreateSubmoduleMenuItem(submodule)));
            toolStripButtonLevelUp.ToolTipText = _goToSuperProject.Text;
        }

        newItems.Add(new ToolStripSeparator());

        ToolStripMenuItem mi = new(updateAllSubmodulesToolStripMenuItem.Text, Images.SubmodulesUpdate);
        mi.Click += UpdateAllSubmodulesToolStripMenuItemClick;
        newItems.Add(mi);

        if (result.CurrentSubmoduleName is not null)
        {
            ToolStripMenuItem item = new(_updateCurrentSubmodule.Text)
            {
                Width = 200, Tag = Module.WorkingDir, Image = Images.FolderSubmodule
            };
            item.Click += UpdateSubmoduleToolStripMenuItemClick;
            newItems.Add(item);
        }

        // Using AddRange is critical: if you used Add to add menu items one at a
        // time, performance would be extremely slow with many submodules (> 100).
        toolStripButtonLevelUp.DropDownItems.AddRange([.. newItems]);
        toolStripButtonLevelUp.DropDown.ResumeLayout();

        return newItems;
    }

    private void UpdateSubmoduleMenuStatus(SubmoduleInfoResult result)
    {
        if (_currentSubmoduleMenuItems is null)
        {
            return;
        }

        Validates.NotNull(result.TopProject);
        var infos = result.AllSubmodules.ToDictionary(info => info.Path, info => info);
        infos[result.TopProject.Path] = result.TopProject;
        foreach (var item in _currentSubmoduleMenuItems)
        {
            var path = item.Tag as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                // not a submodule
                continue;
            }

            if (infos.TryGetValue(path, out var info))
            {
                UpdateSubmoduleMenuItemStatus(item, info);
            }
            else
            {
                DebugHelpers.Fail(
                    $"Status info for {path} ({1 + result.AllSubmodules.Count} records) has no match in current nodes ({_currentSubmoduleMenuItems.Count})");
            }
        }
    }

    private void RemoveSubmoduleButtons()
    {
        foreach (var item in toolStripButtonLevelUp.DropDownItems)
        {
            if (item is ToolStripMenuItem toolStripButton)
            {
                toolStripButton.Click -= SubmoduleToolStripButtonClick;
            }
        }

        toolStripButtonLevelUp.DropDownItems.Clear();
    }

    #endregion

    #region Layout management

    private void toggleSplitViewLayout_Click(object sender, EventArgs e)
    {
        AppSettings.ShowSplitViewLayout = !AppSettings.ShowSplitViewLayout;
        DiagnosticsClient.TrackEvent("Layout change",
            new Dictionary<string, string>
            {
                { nameof(AppSettings.ShowSplitViewLayout), AppSettings.ShowSplitViewLayout.ToString() }
            });

        RefreshSplitViewLayout();
    }

    private void toggleLeftPanel_Click(object sender, EventArgs e)
    {
        MainSplitContainer.Panel1Collapsed = !MainSplitContainer.Panel1Collapsed;
        DiagnosticsClient.TrackEvent("Layout change",
            new Dictionary<string, string> { { "ShowLeftPanel", MainSplitContainer.Panel1Collapsed.ToString() } });

        RefreshLayoutToggleButtonStates();

        if (!MainSplitContainer.Panel1Collapsed)
        {
            // Refresh the left panel, update visibility of objects separately
            // Get the "main" stash commit, including the reflog selector
            Lazy<IReadOnlyCollection<GitRevision>> getStashRevs = new(() =>
                !AppSettings.ShowStashes
                    ? []
                    : new RevisionReader(new GitModule(UiCommands.GetRequiredService<IGitExecutorProvider>(),
                        UiCommands.Module.WorkingDir)).GetStashes(CancellationToken.None));

            RefreshLeftPanel(new FilteredGitRefsProvider(UiCommands.Module).GetRefs, getStashRevs, forceRefresh: true);
            repoObjectsTree.RefreshRevisionsLoaded();
        }
    }

    private void CommitInfoPositionClick(object sender, EventArgs e)
    {
        if (!menuCommitInfoPosition.DropDownButtonPressed)
        {
            SetCommitInfoPosition((CommitInfoPosition)(
                ((int)AppSettings.CommitInfoPosition + 1) %
                Enum.GetValues<CommitInfoPosition>().Length));
        }
    }

    private void CommitInfoBelowClick(object sender, EventArgs e)
    {
        SetCommitInfoPosition(CommitInfoPosition.BelowList);
    }

    private void CommitInfoLeftwardClick(object sender, EventArgs e)
    {
        SetCommitInfoPosition(CommitInfoPosition.LeftwardFromList);
    }

    private void CommitInfoRightwardClick(object sender, EventArgs e)
    {
        SetCommitInfoPosition(CommitInfoPosition.RightwardFromList);
    }

    private void SetCommitInfoPosition(CommitInfoPosition position)
    {
        AppSettings.CommitInfoPosition = position;
        DiagnosticsClient.TrackEvent("Layout change",
            new Dictionary<string, string>
            {
                { nameof(AppSettings.CommitInfoPosition), AppSettings.CommitInfoPosition.ToString() }
            });

        LayoutRevisionInfo();
        RefreshLayoutToggleButtonStates();
    }

    private void RefreshSplitViewLayout()
    {
        RightSplitContainer.Panel2Collapsed = !AppSettings.ShowSplitViewLayout;
        DiagnosticsClient.TrackEvent("Layout change",
            new Dictionary<string, string>
            {
                { nameof(AppSettings.ShowSplitViewLayout), AppSettings.ShowSplitViewLayout.ToString() }
            });

        RefreshLayoutToggleButtonStates();
    }

    private void RefreshLayoutToggleButtonStates()
    {
        toggleLeftPanel.Checked = !MainSplitContainer.Panel1Collapsed;
        toggleSplitViewLayout.Checked = AppSettings.ShowSplitViewLayout;

        var commitInfoPositionNumber = (int)AppSettings.CommitInfoPosition;
        var selectedMenuItem = menuCommitInfoPosition.DropDownItems[commitInfoPositionNumber];
        menuCommitInfoPosition.Image = selectedMenuItem.Image;
        menuCommitInfoPosition.ToolTipText = selectedMenuItem.Text?.Replace("&", string.Empty);
    }

    private void LayoutRevisionInfo()
    {
        // Handle must be created prior to insertion
        _ = CommitInfoTabControl.Handle;

        RevisionInfo.SuspendLayout();
        CommitInfoTabControl.SuspendLayout();
        RevisionsSplitContainer.SuspendLayout();

        var commitInfoPosition = AppSettings.CommitInfoPosition;
        if (commitInfoPosition == CommitInfoPosition.BelowList)
        {
            CommitInfoTabControl.SelectedIndexChanged -= CommitInfoTabControl_SelectedIndexChanged;
            CommitInfoTabControl.InsertIfNotExists(0, CommitInfoTabPage);
            CommitInfoTabControl.SelectedIndexChanged += CommitInfoTabControl_SelectedIndexChanged;
            CommitInfoTabControl.SelectedTab = CommitInfoTabPage;

            RevisionsSplitContainer.FixedPanel = FixedPanel.Panel2;
            RevisionInfo.Parent = CommitInfoTabPage;
            RevisionGridContainer.Parent = RevisionsSplitContainer.Panel1;
            RevisionsSplitContainer.Panel2Collapsed = true;
        }
        else
        {
            // enough to fit CommitInfoHeader in most cases, when the width is (avatar + commit hash)
            var width = DpiUtil.Scale(490) + SystemInformation.VerticalScrollBarWidth;
            CommitInfoTabControl.SelectedIndexChanged -= CommitInfoTabControl_SelectedIndexChanged;
            CommitInfoTabControl.RemoveIfExists(CommitInfoTabPage);
            CommitInfoTabControl.SelectedIndexChanged += CommitInfoTabControl_SelectedIndexChanged;

            if (commitInfoPosition == CommitInfoPosition.RightwardFromList)
            {
                RevisionsSplitContainer.FixedPanel = FixedPanel.Panel2;
                RevisionsSplitContainer.SplitterDistance = Math.Max(0, RevisionsSplitContainer.Width - width);
                RevisionInfo.Parent = RevisionsSplitContainer.Panel2;
                RevisionGridContainer.Parent = RevisionsSplitContainer.Panel1;
            }
            else if (commitInfoPosition == CommitInfoPosition.LeftwardFromList)
            {
                RevisionsSplitContainer.FixedPanel = FixedPanel.Panel1;
                RevisionsSplitContainer.SplitterDistance = width;
                RevisionInfo.Parent = RevisionsSplitContainer.Panel1;
                RevisionGridContainer.Parent = RevisionsSplitContainer.Panel2;
            }
            else
            {
                throw new NotSupportedException();
            }

            RevisionsSplitContainer.Panel2Collapsed = false;
        }

        RevisionInfo.Parent.BackColor = RevisionInfo.BackColor;
        RevisionInfo.ResumeLayout(true);

        MainSplitContainer.Panel1.BackColor = OtherColors.PanelBorderColor;
        RevisionsSplitContainer.Panel1.BackColor = OtherColors.PanelBorderColor;
        RevisionsSplitContainer.Panel2.BackColor = OtherColors.PanelBorderColor;

        CommitInfoTabControl.ResumeLayout(true);
        RevisionsSplitContainer.ResumeLayout(true);
    }

    #endregion
}
