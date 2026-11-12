using System.ComponentModel;
using System.Text;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Translations;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.Editor;
using GitUI.Plugin;
using GitUI.Properties;
using GitUI.Theming;
using GitUI.UserControls;
using GitUI.UserControls.RevisionGrid;
using ResourceManager;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommandsDialogs;

public sealed partial class FormFileHistory : GitModuleForm, IRevisionGridFileUpdate
{
    private const string _formBrowseName = "FormBrowse";
    private readonly AsyncLoader _asyncLoader = new();

    private readonly TranslationString _buildReportTabCaption = new("Build Report");
    private readonly ICommitDataManager _commitDataManager;
    private readonly CancellationTokenSequence _customDiffToolsSequence = new();
    private readonly TranslationString _fileNotFound = new(" - Git could not identify the file {0}");
    private readonly FormBrowseMenus _formBrowseMenus;
    private readonly IFullPathResolver _fullPathResolver;
    private readonly CancellationTokenSequence _viewChangesSequence = new();

    private BuildReportTabPageExtension? _buildReportTabPageExtension;
    private string? _commitInfoTabPageText;

    /// <summary>
    ///     Open FileHistory form.
    /// </summary>
    /// <param name="commands">commands in the current form.</param>
    /// <param name="fileName">name in repo of file to view.</param>
    /// <param name="revision">initial selected commit.</param>
    /// <param name="filterByRevision">add filter.</param>
    /// <param name="showBlame">show blame initially instead of diff view.</param>
    public FormFileHistory(IGitUiCommands commands, string fileName, GitRevision? revision = null,
        bool filterByRevision = false, bool showBlame = false)
        : base(commands)
    {
        InitializeComponent();
        ConfigureTabControl();

        ToolStripFilters.Bind(() => Module, RevisionGrid);

        var toolForeColor = SystemColors.WindowText;
        var toolBackColor = Color.Transparent;
        BackColor = AppColor.PanelBackground.GetThemeColor();
        ForeColor = toolForeColor;
        ToolStripFilters.BackColor = toolBackColor;
        ToolStripFilters.ForeColor = toolForeColor;
        ToolStripFilters.InitToolStripStyles(toolForeColor, toolBackColor);

        _formBrowseMenus = new FormBrowseMenus(FileHistoryContextMenu);
        _formBrowseMenus.ResetMenuCommandSets();
        _formBrowseMenus.AddMenuCommandSet(MainMenuItem.NavigateMenu, RevisionGrid.MenuCommands.NavigateMenuCommands);
        _formBrowseMenus.AddMenuCommandSet(MainMenuItem.ViewMenu, RevisionGrid.MenuCommands.ViewMenuCommands);
        _formBrowseMenus.InsertRevisionGridMainMenuItems(toolStripSeparator4);

        _commitDataManager = new CommitDataManager(() => Module);
        _fullPathResolver = new FullPathResolver(() => Module.WorkingDir);

        CommitDiff.EscapePressed += Close;
        View.EscapePressed += Close;
        Diff.EscapePressed += Close;
        Blame.EscapePressed += Close;

        copyToClipboardToolStripMenuItem.SetRevisionFunc(() => RevisionGrid.GetSelectedRevisions());

        InitializeComplete();

        Blame.ConfigureRepositoryHostPlugin(PluginRegistry.TryGetGitHosterForModule(Module));

        RevisionGrid.SelectedId = revision is null ? default : revision.ObjectId;
        RevisionGrid.ShowBuildServerInfo = true;
        RevisionGrid.FilePathByObjectId = [];

        // Replace Windows path separator to Linux path separator.
        // This is needed to keep the file history working when started from file tree in
        // browse dialog.
        FileName = fileName.RemoveQuotes().ToPosixPath();

        SetTitle();

        Diff.ExtraDiffArgumentsChanged += (sender, e) => UpdateSelectedFileViewers();

        var isSubmodule = GitModule.IsValidGitWorkingDir(_fullPathResolver.Resolve(FileName));

        if (isSubmodule)
        {
            tabControl1.RemoveIfExists(BlameTab);
        }

        RevisionGrid.SelectionChanged += FileChangesSelectionChanged;
        RevisionGrid.DisableContextMenu();

        var blameTabExists = tabControl1.Contains(BlameTab);

        UpdateFollowHistoryMenuItems();

        showFullHistoryToolStripMenuItem.Checked = AppSettings.FullHistoryInFileHistory;
        simplifyMergesToolStripMenuItem.Checked = AppSettings.SimplifyMergesInFileHistory;
        simplifyMergesToolStripMenuItem.Enabled = AppSettings.FullHistoryInFileHistory;

        loadHistoryOnShowToolStripMenuItem.Checked = AppSettings.LoadFileHistoryOnShow;
        loadBlameOnShowToolStripMenuItem.Checked = AppSettings.LoadBlameOnShow && blameTabExists;
        saveAsToolStripMenuItem.Visible = !isSubmodule;

        toolStripBlameOptions.Visible = blameTabExists;
        if (blameTabExists)
        {
            ignoreWhitespaceToolStripMenuItem.Checked = AppSettings.IgnoreWhitespaceOnBlame;
            detectMoveAndCopyInAllFilesToolStripMenuItem.Checked = AppSettings.DetectCopyInAllOnBlame;
            detectMoveAndCopyInThisFileToolStripMenuItem.Checked = AppSettings.DetectCopyInFileOnBlame;
            displayAuthorFirstToolStripMenuItem.Checked = AppSettings.BlameDisplayAuthorFirst;
            showAuthorAvatarToolStripMenuItem.Checked = AppSettings.BlameShowAuthorAvatar;
            showAuthorToolStripMenuItem.Checked = AppSettings.BlameShowAuthor;
            showAuthorDateToolStripMenuItem.Checked = AppSettings.BlameShowAuthorDate;
            showAuthorTimeToolStripMenuItem.Checked = AppSettings.BlameShowAuthorTime;
            showLineNumbersToolStripMenuItem.Checked = AppSettings.BlameShowLineNumbers;
            showOriginalFilePathToolStripMenuItem.Checked = AppSettings.BlameShowOriginalFilePath;
        }

        if (filterByRevision && revision?.ObjectId is not null)
        {
            ToolStripFilters.SetRevisionFilter(revision.Guid);
        }

        tabControl1.SelectedTab = blameTabExists && showBlame ? BlameTab : DiffTab;

        return;

        void ConfigureTabControl()
        {
            tabControl1.ImageList = new ImageList
                {
                    ColorDepth = ColorDepth.Depth32Bit,
                    ImageSize = DpiUtil.Scale(new Size(16, 16)),
                    Images = { Images.CommitSummary, Images.Diff, Images.ViewFile, Images.Blame }
                }
                .FixImageTransparencyRegression();

            tabControl1.TabPages[0].ImageIndex = 0;
            tabControl1.TabPages[1].ImageIndex = 1;
            tabControl1.TabPages[2].ImageIndex = 2;
            tabControl1.TabPages[3].ImageIndex = 3;
        }
    }

    private string FileName { get; }

    bool IRevisionGridFileUpdate.SelectFileInRevision(ObjectId commitId, RelativePath ignoredFilename)
    {
        return RevisionGrid.SetSelectedRevision(commitId);
    }

    /// <summary>
    ///     Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _asyncLoader.Dispose();
            _customDiffToolsSequence.Dispose();
            _viewChangesSequence.Dispose();

            // if the form was instantiated by the translation app, all of the following would be null
            _formBrowseMenus?.Dispose();

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnRuntimeLoad(EventArgs e)
    {
        base.OnRuntimeLoad(e);

        var autoLoad = (tabControl1.SelectedTab == BlameTab && AppSettings.LoadBlameOnShow) ||
                       AppSettings.LoadFileHistoryOnShow;

        if (autoLoad)
        {
            LoadFileHistory();
        }
        else
        {
            RevisionGrid.Visible = false;
        }

        LoadCustomDifftools();
    }

    public void LoadCustomDifftools()
    {
        List<CustomDiffMergeTool> menus =
        [
            new(openWithDifftoolToolStripMenuItem, OpenWithDifftoolToolStripMenuItem_Click!),
            new(diffToolRemoteLocalStripMenuItem, diffToolRemoteLocalStripMenuItem_Click!)
        ];

        new CustomDiffMergeToolProvider().LoadCustomDiffMergeTools(Module, menus, components, true,
            cancellationToken: _customDiffToolsSequence.Next());
    }

    private void LoadFileHistory()
    {
        RevisionGrid.Visible = true;

        if (string.IsNullOrEmpty(FileName))
        {
            return;
        }

        RevisionGrid.SetAndApplyPathFilter(FileName.Quote());
    }

    private string? GetFileNameForRevision(GitRevision rev)
    {
        if (RevisionGrid.FilePathByObjectId is null)
        {
            return null;
        }

        var objectId = rev.IsArtificial ? RevisionGrid.CurrentCheckout : rev.ObjectId;

        return RevisionGrid.GetRevisionFileName(FileName, objectId);
    }

    private void FileChangesSelectionChanged(object? sender, EventArgs e)
    {
        UpdateSelectedFileViewers();
    }

    private void SetTitle(string? alternativeFileName = null)
    {
        var str = new StringBuilder()
            .Append("File History - ")
            .Append(FileName);

        if (!string.IsNullOrEmpty(alternativeFileName) && alternativeFileName != FileName)
        {
            str.Append(" (").Append(alternativeFileName).Append(')');
        }

        str.Append(" - ").Append(PathUtil.GetDisplayPath(Module.WorkingDir));

        Text = str.ToString();
    }

    private void UpdateSelectedFileViewers(bool force = false)
    {
        var selectedRevisions = RevisionGrid.GetSelectedRevisions();

        if (selectedRevisions.Count == 0)
        {
            return;
        }

        var revision = selectedRevisions[0];
        var children = RevisionGrid.GetRevisionChildren(revision.ObjectId);
        var fileName = GetFileNameForRevision(revision) ?? FileName;
        var isFolder = fileName.EndsWith('/');
        var fileAvailable
            = !isFolder && (revision.IsArtificial
                ? File.Exists(fileName)
                : !Module.GetFileBlobHash(fileName, revision.ObjectId).IsZero);

        SetTitle(fileName);

        _commitInfoTabPageText ??= CommitInfoTabPage.Text;
        CommitInfoTabPage.Text
            = _commitInfoTabPageText
              + (isFolder || fileAvailable ? "" : string.Format(_fileNotFound.Text, fileName.Quote()));

        TabPage? preferredTab = null;
        if (revision.IsArtificial)
        {
            CommitInfoTabPage.Parent = null;
            preferredTab = DiffTab;
        }
        else
        {
            if (CommitInfoTabPage.Parent is null)
            {
                tabControl1.TabPages.Insert(0, CommitInfoTabPage);
            }
        }

        if (!fileAvailable)
        {
            // Note that artificial commits for object type tree (folder) will be handled here too,
            // i.e. no tab at all is visible
            DiffTab.Parent = null;
            preferredTab = CommitInfoTabPage;
        }
        else
        {
            if (DiffTab.Parent is null)
            {
                var index = tabControl1.TabPages.IndexOf(CommitInfoTabPage);
                DebugHelpers.Assert(index != -1, "TabControl should contain commit info tab page");
                tabControl1.TabPages.Insert(index + 1, DiffTab);
            }
        }

        if (revision.IsArtificial || !fileAvailable)
        {
            BlameTab.Parent = null;
            ViewTab.Parent = null;
        }
        else
        {
            if (ViewTab.Parent is null)
            {
                var index = tabControl1.TabPages.IndexOf(DiffTab);
                DebugHelpers.Assert(index != -1, "TabControl should contain diff tab page");
                tabControl1.TabPages.Insert(index + 1, ViewTab);
            }

            if (BlameTab.Parent is null)
            {
                var index = tabControl1.TabPages.IndexOf(ViewTab);
                DebugHelpers.Assert(index != -1, "TabControl should contain view tab page");
                tabControl1.TabPages.Insert(index + 1, BlameTab);
            }
        }

        if (tabControl1.SelectedTab?.Parent is null && preferredTab is not null)
        {
            tabControl1.SelectedTab = preferredTab;
        }

        if (tabControl1.SelectedTab == BlameTab)
        {
            _ = Blame.LoadBlameAsync(revision, children, fileName, RevisionGrid, this, BlameTab, Diff.Encoding,
                force: force, cancellationTokenSequence: _viewChangesSequence);
        }
        else if (tabControl1.SelectedTab == ViewTab)
        {
            Validates.NotNull(fileName);
            View.Encoding = Diff.Encoding;
            GitItemStatus file = new(fileName) { IsTracked = true };
            var cancellationToken = _viewChangesSequence.Next();
            View.GetUpdateTreeId(file, revision.ObjectId, cancellationToken);
            _ = View.ViewGitItemAsync(file, revision.ObjectId, cancellationToken: cancellationToken);
        }
        else if (tabControl1.SelectedTab == DiffTab)
        {
            Validates.NotNull(fileName);
            GitItemStatus file = new(fileName) { IsTracked = true };
            var cancellationToken = _viewChangesSequence.Next();
            View.GetUpdateTreeId(file, revision.ObjectId, cancellationToken);
            FileStatusItem item = new(
                selectedRevisions.Count > 1 ? selectedRevisions[^1] : null,
                selectedRevisions.Count > 0 ? selectedRevisions[0] : null!,
                file);
            _ = Diff.ViewChangesAsync(item, defaultText: TranslatedStrings.NoChanges,
                cancellationToken: cancellationToken);
        }
        else if (tabControl1.SelectedTab == CommitInfoTabPage)
        {
            CommitDiff.SetRevision(revision.ObjectId, fileName);
        }

        _buildReportTabPageExtension ??=
            new BuildReportTabPageExtension(() => Module, tabControl1, _buildReportTabCaption.Text);

        _buildReportTabPageExtension.FillBuildReport(selectedRevisions.Count == 1 ? revision : null);
    }

    private void TabControl1SelectedIndexChanged(object sender, EventArgs e)
    {
        FileChangesSelectionChanged(sender, e);
    }

    private void FileChangesDoubleClick(object sender, EventArgs e)
    {
        RevisionGrid.ViewSelectedRevisions();
    }

    private void OpenWithDifftoolToolStripMenuItem_Click(object? sender, EventArgs e)
    {
        OpenFilesWithDiffTool(RevisionDiffKind.DiffAb, sender);
    }

    private void OpenFilesWithDiffTool(RevisionDiffKind diffKind, object? sender)
    {
        var item = sender as ToolStripMenuItem;
        if (item?.DropDownItems != null)
        {
            // "main menu" clicked, cancel dropdown manually, invoke default mergetool
            item.HideDropDown();
            item.Owner?.Hide();
        }

        var toolName = item?.Tag as string;
        var selectedRevisions = RevisionGrid.GetSelectedRevisions();
        var orgFileName = selectedRevisions.Count != 0
            ? GetFileNameForRevision(selectedRevisions[0])
            : null;

        UiCommands.OpenWithDifftool(this, selectedRevisions, FileName, orgFileName, diffKind, true, toolName);
    }

    private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var selectedRev = RevisionGrid.GetSelectedRevisionOrDefault();
        if (selectedRev is null)
        {
            return;
        }

        var orgFileName = GetFileNameForRevision(selectedRev) ?? FileName;

        var fullName = _fullPathResolver.Resolve(orgFileName);
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return;
        }

        fullName = fullName.ToNativePath();
        using SaveFileDialog fileDialog = new()
        {
            InitialDirectory = Path.GetDirectoryName(fullName),
            FileName = Path.GetFileName(fullName),
            DefaultExt = Path.GetExtension(fullName),
            AddExtension = true
        };
        fileDialog.Filter =
            "Current format (*." +
            fileDialog.DefaultExt + ")|*." +
            fileDialog.DefaultExt +
            "|All files (*.*)|*.*";
        if (fileDialog.ShowDialog(this) == DialogResult.OK)
        {
            Module.SaveBlobAs(fileDialog.FileName, $"{selectedRev.Guid}:\"{orgFileName}\"");
        }
    }

    private void followFileHistoryToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.FollowRenamesInFileHistory = !AppSettings.FollowRenamesInFileHistory;
        UpdateFollowHistoryMenuItems();

        LoadFileHistory();
    }

    private void UpdateFollowHistoryMenuItems()
    {
        followFileHistoryToolStripMenuItem.Checked = AppSettings.FollowRenamesInFileHistory;
        followFileHistoryRenamesToolStripMenuItem.Enabled = AppSettings.FollowRenamesInFileHistory;
        followFileHistoryRenamesToolStripMenuItem.Checked = AppSettings.FollowRenamesInFileHistoryExactOnly;
    }

    private void showFullHistoryToolStripMenuItem_Click(object sender, EventArgs e)
    {
        ToggleFullHistoryFlag();
    }

    private void simplifyMergesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        ToggleSimplifyMergesFlag();
    }

    private void ToggleSimplifyMergesFlag()
    {
        AppSettings.SimplifyMergesInFileHistory = !AppSettings.SimplifyMergesInFileHistory;
        simplifyMergesToolStripMenuItem.Checked = AppSettings.SimplifyMergesInFileHistory;

        if (AppSettings.FullHistoryInFileHistory)
        {
            LoadFileHistory();
        }
    }

    private void ToggleFullHistoryFlag()
    {
        AppSettings.FullHistoryInFileHistory = !AppSettings.FullHistoryInFileHistory;
        showFullHistoryToolStripMenuItem.Checked = AppSettings.FullHistoryInFileHistory;

        simplifyMergesToolStripMenuItem.Enabled = AppSettings.FullHistoryInFileHistory;

        LoadFileHistory();
    }

    private void cherryPickThisCommitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var selectedRevisions = RevisionGrid.GetSelectedRevisions();
        if (selectedRevisions.Count == 1)
        {
            UiCommands.StartCherryPickDialog(this, selectedRevisions[0]);
        }
    }

    private void revertCommitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        var selectedRevisions = RevisionGrid.GetSelectedRevisions();
        if (selectedRevisions.Count == 1)
        {
            UiCommands.StartRevertCommitDialog(this, selectedRevisions[0]);
        }
    }

    private void FileHistoryContextMenuOpening(object sender, CancelEventArgs e)
    {
        var selectedRevisions = RevisionGrid.GetSelectedRevisions();

        diffToolRemoteLocalStripMenuItem.Enabled =
            selectedRevisions.Count == 1 && selectedRevisions[0].ObjectId != ObjectId.WorkTreeId &&
            File.Exists(_fullPathResolver.Resolve(FileName));
        openWithDifftoolToolStripMenuItem.Enabled =
            selectedRevisions.Count is >= 1 and <= 2;
        manipulateCommitToolStripMenuItem.Enabled =
            selectedRevisions.Count == 1 && !selectedRevisions[0].IsArtificial;
        saveAsToolStripMenuItem.Enabled = selectedRevisions.Count == 1;
        copyToClipboardToolStripMenuItem.Enabled =
            selectedRevisions.Count >= 1 && !selectedRevisions[0].IsArtificial;
    }

    private void diffToolRemoteLocalStripMenuItem_Click(object? sender, EventArgs e)
    {
        OpenFilesWithDiffTool(RevisionDiffKind.DiffBLocal, sender);
    }

    private void toolStripSplitLoad_ButtonClick(object sender, EventArgs e)
    {
        LoadFileHistory();
    }

    private void loadHistoryOnShowToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.LoadFileHistoryOnShow = !AppSettings.LoadFileHistoryOnShow;
        loadHistoryOnShowToolStripMenuItem.Checked = AppSettings.LoadFileHistoryOnShow;
    }

    private void loadBlameOnShowToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.LoadBlameOnShow = !AppSettings.LoadBlameOnShow;
        loadBlameOnShowToolStripMenuItem.Checked = AppSettings.LoadBlameOnShow;
    }

    private void Blame_CommandClick(object sender, CommandEventArgs e)
    {
        if (e.Command == "gotocommit")
        {
            Validates.NotNull(e.Data);
            if (Module.TryResolvePartialCommitId(e.Data, out var commitId))
            {
                if (!RevisionGrid.SetSelectedRevision(commitId))
                {
                    MessageBoxes.RevisionFilteredInGrid(this, commitId);
                }
            }
        }
        else if (e.Command == "gotobranch" || e.Command == "gototag")
        {
            Validates.NotNull(e.Data);
            var commit = _commitDataManager.GetCommitData(e.Data);
            if (commit is not null)
            {
                if (!RevisionGrid.SetSelectedRevision(commit.ObjectId))
                {
                    MessageBoxes.RevisionFilteredInGrid(this, commit.ObjectId);
                }
            }
        }
        else if (e.Command == "navigatebackward")
        {
            RevisionGrid.NavigateBackward();
        }
        else if (e.Command == "navigateforward")
        {
            RevisionGrid.NavigateForward();
        }
    }

    private void followFileHistoryRenamesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.FollowRenamesInFileHistoryExactOnly = !AppSettings.FollowRenamesInFileHistoryExactOnly;
        UpdateFollowHistoryMenuItems();
        LoadFileHistory();
    }

    private void ignoreWhitespaceToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.IgnoreWhitespaceOnBlame = !AppSettings.IgnoreWhitespaceOnBlame;
        ignoreWhitespaceToolStripMenuItem.Checked = AppSettings.IgnoreWhitespaceOnBlame;
        UpdateSelectedFileViewers(true);
    }

    private void detectMoveAndCopyInAllFilesToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.DetectCopyInAllOnBlame = !AppSettings.DetectCopyInAllOnBlame;
        detectMoveAndCopyInAllFilesToolStripMenuItem.Checked = AppSettings.DetectCopyInAllOnBlame;
        UpdateSelectedFileViewers(true);
    }

    private void detectMoveAndCopyInThisFileToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.DetectCopyInFileOnBlame = !AppSettings.DetectCopyInFileOnBlame;
        detectMoveAndCopyInThisFileToolStripMenuItem.Checked = AppSettings.DetectCopyInFileOnBlame;
        UpdateSelectedFileViewers(true);
    }

    private void displayAuthorFirstToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameDisplayAuthorFirst = !AppSettings.BlameDisplayAuthorFirst;
        displayAuthorFirstToolStripMenuItem.Checked = AppSettings.BlameDisplayAuthorFirst;
        UpdateSelectedFileViewers(true);
    }

    private void showAuthorToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameShowAuthor = !AppSettings.BlameShowAuthor;
        showAuthorToolStripMenuItem.Checked = AppSettings.BlameShowAuthor;

        if (!AppSettings.BlameShowAuthor)
        {
            showAuthorDateToolStripMenuItem.Checked = true;
            AppSettings.BlameShowAuthorDate = true;
        }

        UpdateSelectedFileViewers(true);
    }

    private void showAuthorDateToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameShowAuthorDate = !AppSettings.BlameShowAuthorDate;
        showAuthorDateToolStripMenuItem.Checked = AppSettings.BlameShowAuthorDate;

        showAuthorTimeToolStripMenuItem.Enabled = AppSettings.BlameShowAuthorDate;

        if (!AppSettings.BlameShowAuthorDate)
        {
            showAuthorToolStripMenuItem.Checked = true;
            AppSettings.BlameShowAuthor = true;
        }

        UpdateSelectedFileViewers(true);
    }

    private void showAuthorTimeToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameShowAuthorTime = !AppSettings.BlameShowAuthorTime;
        showAuthorTimeToolStripMenuItem.Checked = AppSettings.BlameShowAuthorTime;
        UpdateSelectedFileViewers(true);
    }

    private void showLineNumbersToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameShowLineNumbers = !AppSettings.BlameShowLineNumbers;
        showLineNumbersToolStripMenuItem.Checked = AppSettings.BlameShowLineNumbers;
        Blame.UpdateShowLineNumbers();
        UpdateSelectedFileViewers(true);
    }

    private void showOriginalFilePathToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameShowOriginalFilePath = !AppSettings.BlameShowOriginalFilePath;
        showOriginalFilePathToolStripMenuItem.Checked = AppSettings.BlameShowOriginalFilePath;
        UpdateSelectedFileViewers(true);
    }

    private void showAuthorAvatarToolStripMenuItem_Click(object sender, EventArgs e)
    {
        AppSettings.BlameShowAuthorAvatar = !AppSettings.BlameShowAuthorAvatar;
        showAuthorAvatarToolStripMenuItem.Checked = AppSettings.BlameShowAuthorAvatar;
        UpdateSelectedFileViewers(true);
    }

    private void GitcommandLogToolStripMenuItemClick(object sender, EventArgs e)
    {
        FormGitCommandLog.ShowOrActivate(this);
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(FormFileHistory form)
    {
        public RevisionGridControl RevisionGrid
        {
            get => form.RevisionGrid;
        }

        public FileViewer FileViewer
        {
            get => form.View;
        }

        public void SelectViewTab()
        {
            form.tabControl1.SelectedTab = form.ViewTab;
        }
    }

    #region Translation

    public override void AddTranslationItems(ITranslation translation)
    {
        base.AddTranslationItems(translation);
        TranslationUtils.AddTranslationItemsFromFields(_formBrowseName, ToolStripFilters, translation);
    }

    public override void TranslateItems(ITranslation translation)
    {
        base.TranslateItems(translation);
        TranslationUtils.TranslateItemsFromFields(_formBrowseName, ToolStripFilters, translation);
    }

    #endregion
}
