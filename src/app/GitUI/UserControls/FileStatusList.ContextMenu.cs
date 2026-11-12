using System.Text;
using GitCommands;
using GitCommands.Git;
using GitCommands.Git.Extended;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.CommandsDialogs;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.HelperDialogs;
using GitUI.ScriptsEngine;
using GitUI.UserControls.RevisionGrid;
using ResourceManager;

namespace GitUI.UserControls;

partial class FileStatusList
{
    private readonly TranslationString _assumeUnchangedToolTip = new(
        "Tell git to not check the status of this file for performance benefits."
        + Environment.NewLine + "Use this feature when a file is big and never change."
        + Environment.NewLine +
        "Git will never check if the file has changed that will improve status check performance.");

    private readonly CancellationTokenSequence _customDiffToolsSequence = new();
    private readonly TranslationString _deleteFailed = new("Delete file failed");

    private readonly TranslationString _deleteSelectedFiles =
        new("Are you sure you want to delete the selected file(s)?");

    private readonly TranslationString _deleteSelectedFilesCaption = new("Delete");
    private readonly IFindFilePredicateProvider _findFilePredicateProvider = new FindFilePredicateProvider();
    private readonly TranslationString _firstRevision = new("First: A ");
    private readonly CancellationTokenSequence _interactiveAddResetChunkSequence = new();

    private readonly IFileStatusListContextMenuController _itemContextMenuController =
        new FileStatusListContextMenuController();

    private readonly TranslationString _multipleDescription = new("<multiple>");
    private readonly TranslationString _newName = new("New name");

    private readonly RememberFileContextMenuController _rememberFileContextMenuController =
        RememberFileContextMenuController.Default;

    private readonly TranslationString _resetSelectedChangesText =
        new("Are you sure you want to reset all selected files to {0}?");

    private readonly TranslationString _saveFileFilterAllFiles = new("All files");
    private readonly TranslationString _saveFileFilterCurrentFormat = new("Current format");
    private readonly TranslationString _selectedRevision = new("Second: B ");

    private readonly TranslationString _skipWorktreeToolTip = new(
        "Hide already tracked files that will change but that you don\'t want to commit."
        + Environment.NewLine + "Suitable for some config files modified locally.");

    private readonly TranslationString _stopTrackingFail = new("Fail to stop tracking the file '{0}'.");
    /* Mnemonics
        A Save as
        B Blame
        C Find in commit files, Commit submodule
        D Open with difftool
        E Edit, Reset submodule, Select all (on folder)
        F Find file
        G Filter in grid
        H History
        I Show in folder
        J
        K Skip worktree
        L Delete, Show "Find in commit files"
        M Move, Assume unchanged
        N Open temp
        O Open (on file), Open (submodule), Collapse all (folder)
        P Copy paths
        Q
        R Reset files
        S Stage, Sort and group by, Collapse root folders (file tree only)
        T Show in file tree, Stash submodule
        U Unstage, Update submodule
        V Open in VS
        W Open temp with
        X Add file to .git/info/exclude, Expand all (on folder)
        Y Cherry pick
        Z
        . Add file to .gitignore

        Without mnemonic but with hotkey
        - Open with

        Not important
        - Stop tracking this file
        - Reset chunk of file
        - Interactive add
    */

    private Action? _blame;
    private Action? _cherryPickChanges;
    private Action? _filterFileInGrid;

    private Func<GitRevision?>? _getCurrentRevision;
    private Func<int>? _getLineNumber;
    private Func<string>? _getSelectedText;
    private Func<bool>? _getSupportLinePatching;
    private Action<bool>? _openInFileTreeTabAsBlame;
    private Action? _refreshParent;
    private Action? _stage;
    private Action? _unstage;

    private void AddFileToGitIgnore_Click(object sender, EventArgs e)
    {
        AddFileToIgnoreFile(false);
    }

    private void AddFileToGitInfoExclude_Click(object sender, EventArgs e)
    {
        AddFileToIgnoreFile(true);
    }

    private void AddFileToIgnoreFile(bool localExclude)
    {
        string[] fileNames = SelectedFolder is { Length: > 0 } selectedFolder
            ? [$"/{selectedFolder}/"]
            : [.. SelectedItems.Select(item => "/" + item.Item.Name)];
        if (fileNames.Length > 0 && UiCommands.StartAddToGitIgnoreDialog(this, localExclude, fileNames))
        {
            RequestRefresh();
        }
    }

    private void AssumeUnchanged_Click(object sender, EventArgs e)
    {
        Module.AssumeUnchangedFiles([.. SelectedItems.Items()], tsmiAssumeUnchanged.Checked, out _);
        RequestRefresh();
    }

    public void BindContextMenu(Action refreshParent, bool canAutoRefresh, Action? stage, Action? unstage)
    {
        _refreshParent = refreshParent;
        _stage = stage;
        _unstage = unstage;

        btnRefresh.Click += (s, e) => refreshParent();
        btnRefresh.Visible = canAutoRefresh;
        tsmiRefreshOnFormFocus.Visible = canAutoRefresh;
        sepToolbar.Visible = canAutoRefresh;

        tsmiShowIgnoredFiles.Visible = canAutoRefresh;
        tsmiShowAssumeUnchangedFiles.Visible = canAutoRefresh;
        tsmiShowUntrackedFiles.Checked = Module.GetEffectiveSetting("status.showuntrackedfiles") != "no";

        tsmiStageFile.Font = new Font(tsmiStageFile.Font, FontStyle.Bold);
        tsmiUnstageFile.Font = new Font(tsmiUnstageFile.Font, FontStyle.Bold);
    }

    public void BindContextMenu(Action cherryPickChanges, Func<bool> getSupportLinePatching)
    {
        _cherryPickChanges = cherryPickChanges;
        _getSupportLinePatching = getSupportLinePatching;

        tsmiCherryPickChanges.Visible = true;
    }

    public void BindContextMenu(
        Action? blame,
        Action cherryPickChanges,
        Action filterFileInGrid,
        Action refreshParent,
        Action<bool>? openInFileTreeTabAsBlame,
        Func<GitRevision?>? getCurrentRevision,
        Func<int> getLineNumber,
        Func<string>? getSelectedText,
        Func<bool> getSupportLinePatching)
    {
        _blame = blame;
        _cherryPickChanges = cherryPickChanges;
        _filterFileInGrid = filterFileInGrid;
        _openInFileTreeTabAsBlame = openInFileTreeTabAsBlame;
        _refreshParent = refreshParent;
        _getCurrentRevision = getCurrentRevision;
        _getLineNumber = getLineNumber;
        _getSelectedText = getSelectedText;
        _getSupportLinePatching = getSupportLinePatching;

        tsmiCherryPickChanges.Visible = true;
        tsmiShowInFileTree.Visible = true;
        tsmiFilterFileInGrid.Visible = true;

        if (CanUseFindInCommitFilesGitGrep)
        {
            tsmiOpenFindInCommitFilesGitGrepDialog.Visible = true;
            tsmiShowFindInCommitFilesGitGrep.Visible = true;
        }
    }

    private void Blame_Click(object sender, EventArgs e)
    {
        if (_blame is not null)
        {
            _blame?.Invoke();
        }
        else
        {
            StartFileHistoryDialog(true);
        }
    }

    public void CancelLoadCustomDifftools()
    {
        _customDiffToolsSequence.CancelCurrent();
    }

    /// <summary>
    ///     Return whether it is possible to reset to the first commit.
    /// </summary>
    /// <param name="parentId">The parent commit id.</param>
    /// <param name="selectedItems">The selected file status items.</param>
    /// <returns><see langword="true" /> if it is possible to reset to first id.</returns>
    private static bool CanResetToFirst(ObjectId parentId, IEnumerable<FileStatusItem> selectedItems)
    {
        return CanResetToSecond(parentId) ||
               (parentId == ObjectId.IndexId && selectedItems.SecondIds().All(i => i == ObjectId.WorkTreeId));
    }

    /// <summary>
    ///     Return whether it is possible to reset to the second (selected) commit.
    /// </summary>
    /// <param name="resetId">The selected commit id.</param>
    /// <returns><see langword="true" /> if it is possible to reset to first id.</returns>
    private static bool CanResetToSecond(ObjectId resetId)
    {
        return !resetId.IsZeroOrArtificial;
    }

    private void CherryPickChanges_Click(object sender, EventArgs e)
    {
        _cherryPickChanges?.Invoke();
    }

    private void CommitSubmoduleChanges_Click(object sender, EventArgs e)
    {
        string[] submodules = [.. SelectedItems.Where(it => it.Item.IsSubmodule).Select(it => it.Item.Name).Distinct()];
        foreach (var name in submodules)
        {
            var submodulCommands =
                UiCommands.WithWorkingDirectory(_fullPathResolver.Resolve(name.EnsureTrailingPathSeparator()));
            submodulCommands.StartCommitDialog(this);
        }

        RequestRefresh();
    }

    private void DeleteFile_Click(object sender, EventArgs e)
    {
        try
        {
            FileStatusItem[] selected = [.. SelectedItems];
            if (selected.Length == 0 || !selected[0].SecondRevision.IsArtificial ||
                MessageBoxes.Show(this, _deleteSelectedFiles.Text, _deleteSelectedFilesCaption.Text,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) !=
                DialogResult.Yes)
            {
                return;
            }

            StoreNextItemToSelect();

            try
            {
                DeleteFromFilesystem(selected);
            }
            finally
            {
                RequestRefresh();
            }
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(this, _deleteFailed.Text + Environment.NewLine + ex.Message, TranslatedStrings.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        return;

        void DeleteFromFilesystem(IEnumerable<FileStatusItem> items)
        {
            items = items.Where(item => !item.Item.IsSubmodule);

            // If any file is staged, it must be unstaged
            Module.BatchUnstageFiles(items.Where(item => item.Item.Staged == StagedStatus.Index)
                .Select(item => item.Item));

            foreach (var item in items)
            {
                var path = _fullPathResolver.Resolve(item.Item.Name);
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
                else
                {
                    File.Delete(path!);
                }
            }
        }
    }

    /// <summary>
    ///     Provide a description for the first selected or parent to the "primary" selected last.
    /// </summary>
    /// <returns>A description of the selected parent.</returns>
    private string? DescribeRevisions(List<GitRevision> parents)
    {
        return parents.Count switch
        {
            1 => GetDescriptionForRevision(parents[0]?.ObjectId ?? default(ObjectId)),
            > 1 => _multipleDescription.Text,
            _ => null
        };
    }

    private void DiffFirstToLocal_Click(object? sender, EventArgs e)
    {
        OpenFilesWithDiffTool(RevisionDiffKind.DiffALocal, sender);
    }

    private void DiffFirstToSelected_Click(object? sender, EventArgs e)
    {
        OpenFilesWithDiffTool(RevisionDiffKind.DiffAb, sender);
    }

    private void DiffSelectedToLocal_Click(object? sender, EventArgs e)
    {
        OpenFilesWithDiffTool(RevisionDiffKind.DiffBLocal, sender);
    }

    private void DiffTwoSelected_Click(object? sender, EventArgs e)
    {
        var item = sender as ToolStripMenuItem;
        if (item?.DropDownItems is not null)
        {
            // "main menu" clicked, cancel dropdown manually, invoke default difftool
            item.HideDropDown();
        }

        var toolName = item?.Tag as string;
        List<FileStatusItem> diffFiles = [.. SelectedItems];
        if (diffFiles.Count != 2)
        {
            return;
        }

        // The order is always the order in the list, not clicked order, but the (last) selected is known
        var firstIndex = FocusedItem == diffFiles[0] ? 1 : 0;
        var secondIndex = 1 - firstIndex;

        // Fallback to first revision if second revision cannot be used
        var isFirstItemSecondRev =
            _rememberFileContextMenuController.ShouldEnableFirstItemDiff(diffFiles[firstIndex], true);
        var first = _rememberFileContextMenuController.GetGitCommit(Module.GetFileBlobHash, diffFiles[firstIndex],
            isFirstItemSecondRev);
        var isSecondItemSecondRev =
            _rememberFileContextMenuController.ShouldEnableSecondItemDiff(diffFiles[secondIndex], true);
        var second = _rememberFileContextMenuController.GetGitCommit(Module.GetFileBlobHash, diffFiles[secondIndex],
            isSecondItemSecondRev);

        Module.OpenFilesWithDifftool(first, second, toolName);
    }

    private void DiffWithRemembered_Click(object? sender, EventArgs e)
    {
        var item = sender as ToolStripMenuItem;
        if (item?.DropDownItems is not null)
        {
            // "main menu" clicked, cancel dropdown manually, invoke default difftool
            item.HideDropDown();
        }

        var toolName = item?.Tag as string;

        // For first item, the second revision is explicitly remembered
        var first = _rememberFileContextMenuController.GetGitCommit(Module.GetFileBlobHash,
            _rememberFileContextMenuController.RememberedDiffFileItem, true);

        // Fallback to first revision if second cannot be used
        var isSecond = _rememberFileContextMenuController.ShouldEnableSecondItemDiff(SelectedItem, true);
        var second =
            _rememberFileContextMenuController.GetGitCommit(Module.GetFileBlobHash, SelectedItem, isSecond);

        Module.OpenFilesWithDifftool(first, second, toolName);
    }

    private void EditWorkingDirectoryFile_Click(object sender, EventArgs e)
    {
        if (SelectedItem is null)
        {
            return;
        }

        var fileName = _fullPathResolver.Resolve(SelectedItem.Item.Name);
        UiCommands.StartFileEditorDialog(fileName, lineNumber: GetLineNumber());
        RequestRefresh();
    }

    internal bool ExecuteCommand(RevisionDiffControl.Command cmd)
    {
        return ExecuteCommand((int)cmd);
    }

    protected override bool ExecuteCommand(int cmd)
    {
        if ((FilterFilesByNameRegexFocused || FindInCommitFilesGitGrepFocused) && IsTextEditKey(GetShortcutKeys(cmd)))
        {
            return false;
        }

        UpdateStatusOfMenuItems();

        switch ((RevisionDiffControl.Command)cmd)
        {
            case RevisionDiffControl.Command.DeleteSelectedFiles: tsmiDeleteFile.PerformClick(); break;
            case RevisionDiffControl.Command.ShowHistory: tsmiFileHistory.PerformClick(); break;
            case RevisionDiffControl.Command.Blame: tsmiBlame.PerformClick(); break;
            case RevisionDiffControl.Command.OpenWithDifftool: OpenFilesWithDiffTool(RevisionDiffKind.DiffAb); break;
            case RevisionDiffControl.Command.OpenWithDifftoolFirstToLocal:
                OpenFilesWithDiffTool(RevisionDiffKind.DiffALocal); break;
            case RevisionDiffControl.Command.OpenWithDifftoolSelectedToLocal:
                OpenFilesWithDiffTool(RevisionDiffKind.DiffBLocal); break;
            case RevisionDiffControl.Command.OpenWorkingDirectoryFile:
                tsmiOpenWorkingDirectoryFile.PerformClick(); break;
            case RevisionDiffControl.Command.OpenWorkingDirectoryFileWith:
                tsmiOpenWorkingDirectoryFileWith.PerformClick(); break;
            case RevisionDiffControl.Command.EditFile: tsmiEditWorkingDirectoryFile.PerformClick(); break;
            case RevisionDiffControl.Command.OpenAsTempFile: tsmiOpenRevisionFile.PerformClick(); break;
            case RevisionDiffControl.Command.OpenAsTempFileWith: tsmiOpenRevisionFileWith.PerformClick(); break;
            case RevisionDiffControl.Command.ResetSelectedFiles: return ResetSelectedFilesWithConfirmation();
            case RevisionDiffControl.Command.StageSelectedFile: tsmiStageFile.PerformClick(); break;
            case RevisionDiffControl.Command.UnStageSelectedFile: tsmiUnstageFile.PerformClick(); break;
            case RevisionDiffControl.Command.ShowFileTree: tsmiShowInFileTree.PerformClick(); break;
            case RevisionDiffControl.Command.FilterFileInGrid: tsmiFilterFileInGrid.PerformClick(); break;
            case RevisionDiffControl.Command.SelectFirstGroupChanges: return SelectFirstGroupChangesIfFocused();
            case RevisionDiffControl.Command.FindFile: tsmiFindFile.PerformClick(); break;
            case RevisionDiffControl.Command.FindInCommitFilesUsingGitGrepDiffTab:
                if (_isFileTreeMode)
                {
                    return false;
                }

                tsmiOpenFindInCommitFilesGitGrepDialog.PerformClick();
                break;
            case RevisionDiffControl.Command.FindInCommitFilesUsingGitGrepFileTreeTab:
                if (!_isFileTreeMode)
                {
                    return false;
                }

                tsmiOpenFindInCommitFilesGitGrepDialog.PerformClick();
                break;
            case RevisionDiffControl.Command.OpenInVisualStudio: tsmiOpenInVisualStudio.PerformClick(); break;
            case RevisionDiffControl.Command.AddFileToGitIgnore: return AddFileToGitIgnore();
            case RevisionDiffControl.Command.RenameMove: tsmiMove.PerformClick(); break;
            default: return base.ExecuteCommand(cmd);
        }

        return true;

        bool AddFileToGitIgnore()
        {
            if (!Focused)
            {
                return false;
            }

            tsmiAddFileToGitIgnore.PerformClick();
            return true;
        }

        bool ResetSelectedFilesWithConfirmation()
        {
            if (!Focused)
            {
                return false;
            }

            InitResetFileToToolStripMenuItem();
            if (!tsmiResetFileToParent.Enabled)
            {
                // Hotkey executed when menu is disabled
                return true;
            }

            // Reset to first (parent)
            ResetSelectedItemsWithConfirmation(true);
            return true;
        }

        bool SelectFirstGroupChangesIfFocused()
        {
            if (!Focused)
            {
                return false;
            }

            SelectedItems = FirstGroupItems;
            return true;
        }
    }

    private void FileHistory_Click(object sender, EventArgs e)
    {
        StartFileHistoryDialog(false);
    }

    private void FilterFileInGrid_Click(object sender, EventArgs e)
    {
        _filterFileInGrid?.Invoke();
    }

    private void FindFile_Click(object sender, EventArgs e)
    {
        if (LoadingFiles.Visible)
        {
            this.InvokeAndForget(async () =>
            {
                while (LoadingFiles.Visible)
                {
                    await Task.Delay(100);
                }

                FindFile_Click(this, EventArgs.Empty);
            });
            return;
        }

        var candidates = GitItemStatuses;

        IEnumerable<GitItemStatus> FindDiffFilesMatches(string name)
        {
            var predicate = _findFilePredicateProvider.Get(name, Module.WorkingDir);
            return candidates.Where(item => predicate(item.Name) || predicate(item.OldName));
        }

        GitItemStatus? selectedItem;
        using (SearchWindow<GitItemStatus> searchWindow = new(FindDiffFilesMatches) { Owner = FindForm() })
        {
            searchWindow.ShowDialog(this);
            selectedItem = searchWindow.SelectedItem;
        }

        if (selectedItem is not null)
        {
            SelectedGitItem = selectedItem;
        }
    }

    private ContextMenuDiffToolInfo GetContextMenuDiffToolInfo()
    {
        // Some items are not supported if more than one revision is selected
        List<GitRevision> revisions = [.. SelectedItems.SecondRevs()];
        var selectedRev = revisions.Count == 1 ? revisions[0] : null;

        List<ObjectId> parentIds = [.. SelectedItems.FirstIds()];
        var firstIsParent = _gitRevisionTester.AllFirstAreParentsToSelected(parentIds, selectedRev);
        var localExists = _gitRevisionTester.AnyLocalFileExists(SelectedItems.Select(i => i.Item));

        var allAreNew = SelectedItems.All(i => i.Item.IsNew);
        var allAreDeleted = SelectedItems.All(i => i.Item.IsDeleted);

        return new ContextMenuDiffToolInfo(
            selectedRev,
            parentIds,
            allAreNew,
            allAreDeleted,
            firstIsParent,
            localExists);
    }

    private int GetLineNumber()
    {
        return _getLineNumber is not null
            ? _getLineNumber()
            : int.Parse(FindScriptOptionsProvider().GetValues(ScriptOptionsProvider.LineNumber).FirstOrDefault("0"));
    }

    private static ContextMenuSelectionInfo GetSelectionInfo(FileStatusItem[] selectedItems,
        RelativePath? selectedFolder, bool isBareRepository, bool supportLinePatching,
        IFullPathResolver fullPathResolver)
    {
        // Some items are not supported if more than one revision is selected
        List<GitRevision> revisions = [.. selectedItems.SecondRevs()];
        var selectedRev = revisions.Count == 1 ? revisions[0] : null;

        // First (A) is parent if one revision selected or if parent, then selected
        List<ObjectId> parentIds = [.. selectedItems.FirstIds()];

        // Combined diff, range diff etc are for display only, no manipulations
        var isStatusOnly = selectedItems.Any(item => item.Item.IsRangeDiff || item.Item.IsStatusOnly);
        var isDisplayOnlyDiff = parentIds.Contains(ObjectId.CombinedDiffId) || isStatusOnly;
        var selectedGitItemCount = selectedItems.Length;

        var isAnyTracked = selectedItems.Any(item => item.Item.IsTracked);
        var isAnyIndex = selectedItems.Any(item => item.Item.Staged == StagedStatus.Index);
        var isAnyWorkTree = selectedItems.Any(item => item.Item.Staged == StagedStatus.WorkTree);
        var supportPatches = selectedGitItemCount == 1 && supportLinePatching;
        var isDeleted = selectedItems.Any(item => item.Item.IsDeleted);
        var isAnySubmodule = selectedItems.Any(item => item.Item.IsSubmodule);
        (var allFilesExist, var allDirectoriesExist, var allFilesOrUntrackedDirectoriesExist) =
            FileOrUntrackedDirExists(selectedItems, fullPathResolver);

        ContextMenuSelectionInfo selectionInfo = new(
            selectedRev,
            selectedFolder,
            isDisplayOnlyDiff,
            isStatusOnly,
            selectedGitItemCount,
            isAnyIndex,
            isAnyWorkTree,
            isBareRepository,
            allFilesExist,
            allDirectoriesExist,
            allFilesOrUntrackedDirectoriesExist,
            isAnyTracked,
            supportPatches,
            isDeleted,
            isAnySubmodule);
        return selectionInfo;

        static (bool allFilesExist, bool allDirectoriesExist, bool allFilesOrUntrackedDirectoriesExist)
            FileOrUntrackedDirExists(FileStatusItem[] items, IFullPathResolver fullPathResolver)
        {
            var allFilesExist = items.Length != 0;
            var allDirectoriesExist = allFilesExist;
            var allFilesOrUntrackedDirectoriesExist = allFilesExist;
            foreach (var item in items)
            {
                var path = fullPathResolver.Resolve(item.Item.Name);
                var fileExists = File.Exists(path);
                var directoryExists = Directory.Exists(path);
                allFilesExist &= fileExists;
                allDirectoriesExist &= directoryExists;
                var fileOrUntrackedDirectoryExists = fileExists || (!item.Item.IsTracked && allDirectoriesExist);
                allFilesOrUntrackedDirectoriesExist &= fileOrUntrackedDirectoryExists;

                if (!allFilesExist && !allDirectoriesExist && !allFilesOrUntrackedDirectoriesExist)
                {
                    break;
                }
            }

            return (allFilesExist, allDirectoriesExist, allFilesOrUntrackedDirectoriesExist);
        }
    }

    public void InitResetFileToToolStripMenuItem()
    {
        // Multiple parent/child can be selected, only the the first is shown.
        // The only artificial commit that can be reset to is Index<-WorkTree
        var selectedId = SelectedItems.SecondIds().FirstOrDefault();
        var parentId = SelectedItems.FirstIds().FirstOrDefault();

        var canResetToSecond = CanResetToSecond(selectedId);
        tsmiResetFileToSelected.Enabled = canResetToSecond;
        tsmiResetFileToSelected.Visible = canResetToSecond;
        if (canResetToSecond)
        {
            tsmiResetFileToSelected.Text =
                _selectedRevision + GetDescriptionForRevision(selectedId);
        }

        var canResetToFirst = CanResetToFirst(parentId, SelectedItems);
        tsmiResetFileToParent.Enabled = canResetToFirst;
        tsmiResetFileToParent.Visible = canResetToFirst;
        if (canResetToFirst)
        {
            tsmiResetFileToParent.Text =
                _firstRevision + GetDescriptionForRevision(parentId);
        }

        var canReset = canResetToSecond || canResetToFirst;
        tsmiResetFileTo.Enabled = canReset;
    }

    private void InteractiveAdd_Click(object sender, EventArgs e)
    {
        if (SelectedGitItem is not { } item)
        {
            return;
        }

        var token = _interactiveAddResetChunkSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            await Module.AddInteractiveAsync(item);
            await this.SwitchToMainThreadAsync(token);
            RequestRefresh();
        });
    }

    public void LoadCustomDifftools()
    {
        List<CustomDiffMergeTool> menus =
        [
            new(tsmiDiffFirstToSelected, DiffFirstToSelected_Click),
            new(tsmiDiffSelectedToLocal, DiffSelectedToLocal_Click),
            new(tsmiDiffFirstToLocal, DiffFirstToLocal_Click),
            new(tsmiDiffWithRemembered, DiffWithRemembered_Click),
            new(tsmiDiffTwoSelected, DiffTwoSelected_Click)
        ];

        new CustomDiffMergeToolProvider().LoadCustomDiffMergeTools(Module, menus, components, true,
            cancellationToken: _customDiffToolsSequence.Next());
    }

    private void Move_Click(object sender, EventArgs e)
    {
        var oldName = SelectedGitItem?.Name;
        var isFolder = oldName is null;
        oldName ??= SelectedFolder?.Value;
        if (oldName is null)
        {
            return;
        }

        var title = tsmiMove.Text.RemoveMnemonicMarker();
        using var prompt = UiCommands.GetRequiredService<ISimplePromptCreator>()
            .Create(title, _newName.Text, oldName);

        // Repeat if name not different or if failed, let the user cancel explicitly
        while (prompt.ShowDialog(this) == DialogResult.OK)
        {
            MoveCommand.Arguments arguments = new(isFolder, oldName, prompt.UserInput);
            MoveCommand moveCommand = new(Module.GitExecutable);
            if (!moveCommand.Validate(arguments))
            {
                continue;
            }

            try
            {
                moveCommand.Execute(arguments);
                return;
            }
            catch (Exception exception)
            {
                MessageBoxes.ShowError(this, exception.Message, title);
            }
            finally
            {
                RequestRefresh();
            }
        }
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
        OpenFilesWithDiffTool(diffKind, toolName);
    }

    private void OpenFilesWithDiffTool(RevisionDiffKind diffKind, string? toolName = null)
    {
        using (WaitCursorScope.Enter())
        {
            foreach (var item in SelectedItems)
            {
                if (item.FirstRevision?.ObjectId == ObjectId.CombinedDiffId)
                {
                    // CombinedDiff cannot be viewed in a difftool
                    // Disabled in menus but can be activated from shortcuts, just ignore
                    continue;
                }

                // If item.FirstRevision is null, compare to root commit
                GitRevision?[] revs = [item.SecondRevision, item.FirstRevision];
                UiCommands.OpenWithDifftool(this, revs, item.Item.Name, item.Item.OldName, diffKind,
                    item.Item.IsTracked, toolName);
            }
        }
    }

    private void OpenFindInCommitFilesGitGrepDialog_Click(object sender, EventArgs e)
    {
        ShowFindInCommitFileGitGrepDialog(_getSelectedText?.Invoke() ?? "");
    }

    private void OpenInVisualStudio_Click(object sender, EventArgs e)
    {
        if (VisualStudioIntegration.IsVisualStudioInstalled && SelectedItemAbsolutePath is { } itemName)
        {
            VisualStudioIntegration.OpenFile(itemName, GetLineNumber());
        }
    }

    private void OpenRevisionFile_Click(object sender, EventArgs e)
    {
        SaveSelectedItemToTempFile(fileName => OsShellUtil.Open(fileName));
    }

    private void OpenRevisionFileWith_Click(object sender, EventArgs e)
    {
        SaveSelectedItemToTempFile(OsShellUtil.OpenAs);
    }

    private void OpenWithDifftool_DropDownOpening(object sender, EventArgs e)
    {
        var selectionInfo = GetContextMenuDiffToolInfo();
        List<GitRevision> revisions = [.. SelectedItems.SecondRevs()];

        if (revisions.Count != 0)
        {
            tsmiSecondDiffCaption.Text = _selectedRevision + (DescribeRevisions(revisions) ?? string.Empty);
            tsmiSecondDiffCaption.Visible = true;
            MenuUtil.SetAsCaptionMenuItem(tsmiSecondDiffCaption, ItemContextMenu);

            tsmiFirstDiffCaption.Text =
                _firstRevision + (DescribeRevisions([.. SelectedItems.FirstRevs()]) ?? string.Empty);
            tsmiFirstDiffCaption.Visible = true;
            MenuUtil.SetAsCaptionMenuItem(tsmiFirstDiffCaption, ItemContextMenu);
        }
        else
        {
            tsmiFirstDiffCaption.Visible = false;
            tsmiSecondDiffCaption.Visible = false;
        }

        tsmiDiffFirstToSelected.Enabled = _itemContextMenuController.ShouldShowMenuFirstToSelected(selectionInfo);
        tsmiDiffFirstToLocal.Enabled = _itemContextMenuController.ShouldShowMenuFirstToLocal(selectionInfo);
        tsmiDiffSelectedToLocal.Enabled = _itemContextMenuController.ShouldShowMenuSelectedToLocal(selectionInfo);
        tsmiDiffFirstToLocal.Visible
            = tsmiDiffSelectedToLocal.Visible
                = !_itemContextMenuController.ShouldHideToLocal(selectionInfo);

        List<FileStatusItem> diffFiles = [.. SelectedItems];
        sepDifftoolRemember.Visible = diffFiles.Count == 1 || diffFiles.Count == 2;

        // The order is always the order in the list, not clicked order, but the (last) selected is known
        var firstIndex = diffFiles.Count == 2 && FocusedItem == diffFiles[0] ? 1 : 0;
        var secondIndex = 1 - firstIndex;

        tsmiDiffTwoSelected.Visible = diffFiles.Count == 2;
        tsmiDiffTwoSelected.Enabled =
            diffFiles.Count == 2
            && _rememberFileContextMenuController.ShouldEnableFirstItemDiff(diffFiles[firstIndex])
            && _rememberFileContextMenuController.ShouldEnableSecondItemDiff(diffFiles[secondIndex]);

        tsmiDiffWithRemembered.Visible = diffFiles.Count == 1 &&
                                         _rememberFileContextMenuController.RememberedDiffFileItem is not null;
        tsmiDiffWithRemembered.Enabled =
            diffFiles.Count == 1
            && diffFiles[0] != _rememberFileContextMenuController.RememberedDiffFileItem
            && _rememberFileContextMenuController.ShouldEnableSecondItemDiff(diffFiles[0]);
        tsmiDiffWithRemembered.Text =
            _rememberFileContextMenuController.RememberedDiffFileItem is not null
                ? string.Format(TranslatedStrings.DiffSelectedWithRememberedFile,
                    _rememberFileContextMenuController.RememberedDiffFileItem.Item.Name)
                : string.Empty;

        tsmiRememberSecondRevDiff.Visible = diffFiles.Count == 1;
        tsmiRememberSecondRevDiff.Enabled = diffFiles.Count == 1
                                            && _rememberFileContextMenuController.ShouldEnableFirstItemDiff(
                                                diffFiles[0], true);

        tsmiRememberFirstRevDiff.Visible = diffFiles.Count == 1;
        tsmiRememberFirstRevDiff.Enabled = diffFiles.Count == 1
                                           && _rememberFileContextMenuController.ShouldEnableFirstItemDiff(diffFiles[0],
                                               false);
    }

    private void OpenWorkingDirectoryFile_Click(object sender, EventArgs e)
    {
        if (SelectedGitItem is { } gitItemStatus
            && _fullPathResolver.Resolve(gitItemStatus.Name) is { } fileName)
        {
            OsShellUtil.Open(fileName.ToNativePath());
        }
    }

    private void OpenWorkingDirectoryFileWith_Click(object sender, EventArgs e)
    {
        if (SelectedGitItem is { } gitItemStatus
            && _fullPathResolver.Resolve(gitItemStatus.Name) is { } fileName)
        {
            OsShellUtil.OpenAs(fileName.ToNativePath());
        }
    }

    public void RepositoryChanged()
    {
        _rememberFileContextMenuController.RememberedDiffFileItem = null;
    }

    public void ReloadHotkeys()
    {
        HotkeysEnabled = true;
        LoadHotkeys(RevisionDiffControl.HotkeySettingsName);
        tsmiDeleteFile.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.DeleteSelectedFiles);
        tsmiFileHistory.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(RevisionDiffControl.Command.ShowHistory);
        tsmiBlame.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(RevisionDiffControl.Command.Blame);
        tsmiDiffFirstToSelected.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenWithDifftool);
        tsmiDiffFirstToLocal.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenWithDifftoolFirstToLocal);
        tsmiDiffSelectedToLocal.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenWithDifftoolSelectedToLocal);
        tsmiOpenWorkingDirectoryFile.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenWorkingDirectoryFile);
        tsmiOpenWorkingDirectoryFileWith.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenWorkingDirectoryFileWith);
        tsmiEditWorkingDirectoryFile.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.EditFile);
        tsmiOpenRevisionFile.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenAsTempFile);
        tsmiOpenRevisionFileWith.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenAsTempFileWith);
        tsmiResetFileToParent.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.ResetSelectedFiles);
        tsmiStageFile.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.StageSelectedFile);
        tsmiUnstageFile.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.UnStageSelectedFile);
        tsmiShowInFileTree.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.ShowFileTree);
        tsmiFilterFileInGrid.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.FilterFileInGrid);
        tsmiFindFile.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(RevisionDiffControl.Command.FindFile);
        tsmiOpenFindInCommitFilesGitGrepDialog.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(_isFileTreeMode
            ? RevisionDiffControl.Command.FindInCommitFilesUsingGitGrepFileTreeTab
            : RevisionDiffControl.Command.FindInCommitFilesUsingGitGrepDiffTab);
        tsmiOpenInVisualStudio.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.OpenInVisualStudio);
        tsmiAddFileToGitIgnore.ShortcutKeyDisplayString =
            GetShortcutKeyDisplayString(RevisionDiffControl.Command.AddFileToGitIgnore);
        tsmiMove.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(RevisionDiffControl.Command.RenameMove);
    }

    private void RememberFirstRevDiff_Click(object sender, EventArgs e)
    {
        if (SelectedItem?.FirstRevision is null)
        {
            return;
        }

        FileStatusItem item = new(
            SelectedItem.SecondRevision,
            SelectedItem.FirstRevision,
            SelectedItem.Item);
        if (!string.IsNullOrWhiteSpace(SelectedItem.Item.OldName))
        {
            (SelectedItem.Item.Name, SelectedItem.Item.OldName) = (SelectedItem.Item.OldName, SelectedItem.Item.Name);
        }

        _rememberFileContextMenuController.RememberedDiffFileItem = item;
    }

    private void RememberSecondRevDiff_Click(object sender, EventArgs e)
    {
        _rememberFileContextMenuController.RememberedDiffFileItem = SelectedItem;
    }

    private void RequestRefresh()
    {
        if (_refreshParent is not null)
        {
            _refreshParent.Invoke();
        }
        else
        {
            btnRefresh.PerformClick();
        }
    }

    private void ResetChunkOfFile_Click(object sender, EventArgs e)
    {
        if (SelectedGitItem is not { } item)
        {
            return;
        }

        var token = _interactiveAddResetChunkSequence.Next();
        ThreadHelper.FileAndForget(async () =>
        {
            await Module.ResetInteractiveAsync(item);
            await this.SwitchToMainThreadAsync(token);
            RequestRefresh();
        });
    }

    private void ResetFile_Click(object? sender, EventArgs e)
    {
        if (sender == tsmiResetFileTo)
        {
            sender = tsmiResetFileToParent;
            if (!tsmiResetFileToParent.Enabled)
            {
                return;
            }
        }

        ResetSelectedItemsWithConfirmation(sender == tsmiResetFileToParent);
    }

    /// <summary>
    ///     Checks whether resetting any of the items could delete a file from the working directory,
    ///     i.e. whether offering the option to delete new files can have any effect.
    /// </summary>
    /// <param name="items">The items which are about to be reset.</param>
    /// <param name="fullPathResolver">Resolves the working directory path of an item.</param>
    /// <returns><see langword="true" /> if a file which may be deleted exists; otherwise <see langword="false" />.</returns>
    internal static bool HasFilesWhichMayBeDeleted(IReadOnlyList<FileStatusItem> items,
        IFullPathResolver fullPathResolver)
    {
        // A changed file is never deleted, and nothing can be deleted for a path which does not exist
        // in the working directory, e.g. when restoring a file deleted in the selected revision.
        // The name of a rename is inverted when resetting to the selected revision, so check both names.
        foreach (var item in items)
        {
            if (item.Item.IsChanged)
            {
                continue;
            }

            if (PathExists(item.Item.Name) || (item.Item.OldName is { } oldName && PathExists(oldName)))
            {
                return true;
            }
        }

        return false;

        bool PathExists(string name)
        {
            var path = fullPathResolver.Resolve(name);
            return File.Exists(path) || Directory.Exists(path);
        }
    }

    public void ResetSelectedItemsWithConfirmation(bool resetToParent)
    {
        FileStatusItem[] items = [.. SelectedItems];

        // The "new" state could change when resetting, allow user to tick the checkbox.
        // If there are only changed files, it is safe to disable the checkboc (also for restting to selected).
        var hasNewFiles = HasFilesWhichMayBeDeleted(items, _fullPathResolver);
        var hasExistingFiles = items.Any(item => !(item.Item.IsUncommittedAdded || IsRenamedIndexItem(item)));

        var revDescription = resetToParent
            ? $"{_firstRevision}{DescribeRevisions([.. items.FirstRevs()])}"
            : $"{_selectedRevision}{DescribeRevisions([.. items.SecondRevs()])}";
        var confirmationMessage = string.Format(_resetSelectedChangesText.Text, revDescription);

        var resetType =
            FormResetChanges.ShowResetDialog(ParentForm, hasExistingFiles, hasNewFiles, confirmationMessage);
        if (resetType == FormResetChanges.ActionEnum.Cancel)
        {
            return;
        }

        var resetAndDelete = resetType == FormResetChanges.ActionEnum.ResetAndDelete;
        ResetSelectedItemsTo(resetToParent, resetAndDelete);

        return;

        static bool IsRenamedIndexItem(FileStatusItem item)
        {
            return item.Item.IsRenamed && item.Item.Staged == StagedStatus.Index;
        }

        void ResetSelectedItemsTo(bool resetToParent, bool resetAndDelete)
        {
            FileStatusItem[] selectedItems = [.. SelectedItems];

            if (selectedItems.Length == 0)
            {
                return;
            }

            try
            {
                foreach (var id in resetToParent ? selectedItems.FirstIds() : selectedItems.SecondIds())
                {
                    if (resetToParent ? !CanResetToFirst(id, selectedItems) : !CanResetToSecond(id))
                    {
                        // Cannot reset to artificial commit, may be included in multi selections
                        continue;
                    }

                    GitItemStatus[] resetItems =
                    [
                        .. resetToParent
                            ? selectedItems.Items()
                            : selectedItems.Items().Select(item => item.InvertStatus())
                    ];
                    Module.ResetChanges(id, resetItems, resetAndDelete, _fullPathResolver, out var output,
                        null);

                    if (output.Length > 0)
                    {
                        MessageBoxes.Show(this, output.ToString(), TranslatedStrings.ResetChangesCaption,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            finally
            {
                RequestRefresh();
            }
        }
    }

    private void ResetSubmoduleChanges_Click(object sender, EventArgs e)
    {
        string[] submodules = [.. SelectedItems.Where(it => it.Item.IsSubmodule).Select(it => it.Item.Name).Distinct()];

        // Show a form asking the user if they want to reset the changes.
        var resetType = FormResetChanges.ShowResetDialog(this, true, true);
        if (resetType == FormResetChanges.ActionEnum.Cancel)
        {
            return;
        }

        foreach (var name in submodules)
        {
            var module = Module.GetSubmodule(name);
            module.ResetAllChanges(resetType == FormResetChanges.ActionEnum.ResetAndDelete);
        }

        RequestRefresh();
    }

    private void SaveAs_Click(object sender, EventArgs e)
    {
        List<FileStatusItem> files = [.. SelectedItems];

        Func<string, string?>? userSelection = null;
        if (files.Count == 1)
        {
            userSelection = fullName =>
            {
                using SaveFileDialog dialog = new()
                {
                    InitialDirectory = Path.GetDirectoryName(fullName),
                    FileName = Path.GetFileName(fullName),
                    DefaultExt = Path.GetExtension(fullName),
                    AddExtension = true
                };
                dialog.Filter =
                    $"{_saveFileFilterCurrentFormat.Text}(*.{dialog.DefaultExt})|*.{dialog.DefaultExt}|{_saveFileFilterAllFiles.Text}(*.*)|*.*";

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return dialog.FileName;
                }

                return null;
            };
        }
        else if (files.Count > 1)
        {
            userSelection = baseSourceDirectory =>
            {
                using FolderBrowserDialog dialog = new()
                {
                    InitialDirectory = baseSourceDirectory, ShowNewFolderButton = true
                };

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }

                return null;
            };
        }

        if (userSelection is not null)
        {
            _revisionDiffController.SaveFiles(files, userSelection);
        }
    }

    private void SaveSelectedItemToTempFile(Action<string> onSaved)
    {
        var item = SelectedItem;
        if (item?.Item.Name is null)
        {
            return;
        }

        ThreadHelper.FileAndForget(async () =>
        {
            var blob = Module.GetFileBlobHash(item.Item.Name, item.SecondRevision.ObjectId);

            if (blob.IsZero)
            {
                return;
            }

            var fileName = PathUtil.GetFileName(item.Item.Name);
            fileName = (Path.GetTempPath() + fileName).ToNativePath();
            await Module.SaveBlobAsAsync(fileName, blob.ToString());

            onSaved(fileName);
        });
    }

    private void ShowFindInCommitFilesGitGrep_Click(object sender, EventArgs e)
    {
        AppSettings.ShowFindInCommitFilesGitGrep.Value = tsmiShowFindInCommitFilesGitGrep.Checked;
        SetFindInCommitFilesGitGrepVisibility(AppSettings.ShowFindInCommitFilesGitGrep.Value);
    }

    private void ShowInFileTree_Click(object sender, EventArgs e)
    {
        if (!_isFileTreeMode)
        {
            _openInFileTreeTabAsBlame?.Invoke(false);
        }
    }

    private void ShowInFolder_Click(object sender, EventArgs e)
    {
        FormBrowse.OpenContainingFolder(this, Module);
    }

    private void SkipWorktree_Click(object sender, EventArgs e)
    {
        Module.SkipWorktreeFiles([.. SelectedItems.Items()], tsmiSkipWorktree.Checked, out _);
        RequestRefresh();
    }

    private void StageFile_Click(object sender, EventArgs e)
    {
        if (_stage is not null)
        {
            _stage();
            return;
        }

        GitItemStatus[] files =
            [.. SelectedItems.Where(item => item.Item.Staged == StagedStatus.WorkTree).Select(i => i.Item)];
        Module.StageFiles(files, out _);
        RequestRefresh();
    }

    private void StartFileHistoryDialog(bool showBlame)
    {
        (var fileName, var revision) = SelectedFolder is { } relativePath
            ? (relativePath.Length == 0 ? null : relativePath.Value, _getCurrentRevision?.Invoke())
            : SelectedItem is { } item && item.Item.IsTracked
                ? (item.Item.Name, item.SecondRevision)
                : (null, null);
        if (fileName is null)
        {
            return;
        }

        UiCommands.StartFileHistoryDialog(this, fileName, revision, showBlame: showBlame);
    }

    private void StashSubmoduleChanges_Click(object sender, EventArgs e)
    {
        string[] submodules = [.. SelectedItems.Where(it => it.Item.IsSubmodule).Select(it => it.Item.Name).Distinct()];
        foreach (var name in submodules)
        {
            var uiCmds = UiCommands.WithGitModule(Module.GetSubmodule(name));
            uiCmds.StashSave(this, AppSettings.IncludeUntrackedFilesInManualStash);
        }

        RequestRefresh();
    }

    private void StopTracking_Click(object sender, EventArgs e)
    {
        if (SelectedGitItem?.Name is not { } filename)
        {
            return;
        }

        if (Module.StopTrackingFile(filename))
        {
            RequestRefresh();
        }
        else
        {
            MessageBoxes.Show(string.Format(_stopTrackingFail.Text, filename), TranslatedStrings.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnstageFile_Click(object sender, EventArgs e)
    {
        if (_unstage is not null)
        {
            _unstage();
            return;
        }

        GitItemStatus[] files =
            [.. SelectedItems.Where(item => item.Item.Staged == StagedStatus.Index).Select(i => i.Item)];
        Module.BatchUnstageFiles(files);
        RequestRefresh();
    }

    public void UpdateStatusOfMenuItems()
    {
        FileStatusItem[] selectedItems = [.. SelectedItems];
        var selectionInfo = GetSelectionInfo(selectedItems, SelectedFolder,
            Module.IsBareRepository(), _getSupportLinePatching?.Invoke() ?? false, _fullPathResolver);

        // Many options have no meaning for artificial commits or submodules
        // Hide the obviously no action options when single selected, handle them in actions if multi select

        // open submodule is added in FileStatusList
        tsmiFileHistory.Font =
            ((SelectedItem?.Item.IsSubmodule ?? false) && AppSettings.OpenSubmoduleDiffInSeparateWindow) ||
            _openInFileTreeTabAsBlame is null
                ? new Font(tsmiFileHistory.Font, FontStyle.Regular)
                : new Font(tsmiFileHistory.Font, FontStyle.Bold);

        tsmiUpdateSubmodule.Visible
            = tsmiResetSubmoduleChanges.Visible
                = tsmiStashSubmoduleChanges.Visible
                    = tsmiCommitSubmoduleChanges.Visible
                        = sepSubmodule.Visible
                            = _revisionDiffController.ShouldShowSubmoduleMenus(selectionInfo);

        tsmiStageFile.Enabled
            = tsmiStageFile.Visible
                = _revisionDiffController.ShouldShowMenuStage(selectionInfo);
        tsmiUnstageFile.Enabled
            = tsmiUnstageFile.Visible
                = _revisionDiffController.ShouldShowMenuUnstage(selectionInfo);
        InitResetFileToToolStripMenuItem();
        if (!_revisionDiffController.ShouldShowResetFileMenus(selectionInfo))
        {
            tsmiResetFileTo.Enabled = false;
        }

        tsmiCherryPickChanges.Visible = _revisionDiffController.ShouldShowMenuCherryPick(selectionInfo);

        sepFile.Visible = _revisionDiffController.ShouldShowDifftoolMenus(selectionInfo)
                          || _revisionDiffController.ShouldShowMenuDeleteFile(selectionInfo)
                          || _revisionDiffController.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo)
                          || _revisionDiffController.ShouldShowMenuOpenRevision(selectionInfo);

        tsmiOpenWithDifftool.Enabled = _revisionDiffController.ShouldShowDifftoolMenus(selectionInfo);
        tsmiOpenWorkingDirectoryFileWith.Visible =
            _revisionDiffController.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo);
        tsmiOpenRevisionFile.Visible = _revisionDiffController.ShouldShowMenuOpenRevision(selectionInfo);
        tsmiOpenRevisionFile.Enabled = _revisionDiffController.ShouldShowMenuShowInFileTree(selectionInfo);
        tsmiOpenRevisionFileWith.Visible = _revisionDiffController.ShouldShowMenuOpenRevision(selectionInfo);
        tsmiOpenRevisionFileWith.Enabled = _revisionDiffController.ShouldShowMenuShowInFileTree(selectionInfo);
        tsmiSaveAs.Visible = _revisionDiffController.ShouldShowMenuSaveAs(selectionInfo);
        tsmiShowInFolder.Visible = _revisionDiffController.ShouldShowMenuShowInFolder(selectionInfo);
        tsmiEditWorkingDirectoryFile.Visible =
            _revisionDiffController.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo);
        tsmiOpenInVisualStudio.Visible =
            _revisionDiffController.ShouldShowMenuEditWorkingDirectoryFile(selectionInfo) &&
            VisualStudioIntegration.IsVisualStudioInstalled;
        tsmiMove.Visible = _revisionDiffController.ShouldShowMenuMove(selectionInfo);
        tsmiDeleteFile.Text = ResourceManager.TranslatedStrings.GetDeleteFile(selectionInfo.SelectedGitItemCount);
        tsmiDeleteFile.Enabled = _revisionDiffController.ShouldShowMenuDeleteFile(selectionInfo);
        tsmiDeleteFile.Visible = tsmiDeleteFile.Enabled;

        tsmiCopyPaths.Enabled = _revisionDiffController.ShouldShowMenuCopyFileName(selectionInfo);
        tsmiShowInFolder.Enabled = selectedItems.Any(item =>
            _fullPathResolver.Resolve(item.Item.Name) is { } filePath &&
            FormBrowseUtil.FileOrParentDirectoryExists(filePath));

        tsmiShowInFileTree.Visible = !_isFileTreeMode && _openInFileTreeTabAsBlame is not null &&
                                     _revisionDiffController.ShouldShowMenuShowInFileTree(selectionInfo);
        tsmiFilterFileInGrid.Enabled = _filterFileInGrid is not null &&
                                       _revisionDiffController.ShouldShowMenuFileHistory(selectionInfo);
        tsmiFileHistory.Enabled = _revisionDiffController.ShouldShowMenuFileHistory(selectionInfo);
        tsmiBlame.Enabled = AppSettings.UseDiffViewerForBlame.Value || _blame is null
            ? _revisionDiffController.ShouldShowMenuBlame(selectionInfo)
            : _revisionDiffController.ShouldShowMenuShowInFileTree(selectionInfo);
        if (!tsmiBlame.Enabled)
        {
            tsmiBlame.Checked = false;
        }

        sepScripts.Visible = ItemContextMenu.AddUserScripts(tsmiRunScript, ExecuteCommand,
            script => script.OnEvent == ScriptEvent.ShowInFileList, UiCommands);

        tsmiShowFindInCommitFilesGitGrep.Checked = FindInCommitFilesGitGrepVisible;

        var isSubmodule = selectionInfo.SelectedGitItemCount == 1 && selectionInfo.IsAnySubmodule;
        var isSingleFile = selectionInfo.SelectedGitItemCount == 1 && !isSubmodule;

        var canResetAddInteractively = selectionInfo.IsAnyItemWorkTree && isSingleFile;
        tsmiResetChunkOfFile.Visible = canResetAddInteractively;
        tsmiInteractiveAdd.Visible = canResetAddInteractively;

        var canOpenFile = selectionInfo.SelectedGitItemCount == 1 && selectionInfo.AllFilesExist;
        tsmiOpenWorkingDirectoryFile.Visible = canOpenFile;
        tsmiOpenWorkingDirectoryFileWith.Visible = canOpenFile;

        var canIgnoreFiles = selectionInfo.IsAnyItemWorkTree && !isSubmodule;
        var canStopTracking = isSingleFile && selectionInfo.IsAnyTracked;

        sepIgnore.Visible = canIgnoreFiles || canStopTracking;

        tsmiAddFileToGitIgnore.Visible = canIgnoreFiles;
        tsmiAddFileToGitInfoExclude.Visible = canIgnoreFiles;
        tsmiSkipWorktree.Visible = canIgnoreFiles && selectionInfo.IsAnyTracked;
        tsmiAssumeUnchanged.Visible = canIgnoreFiles && selectionInfo.IsAnyTracked;
        tsmiSkipWorktree.Checked = selectedItems.Any(item => item.Item.IsSkipWorktree);
        tsmiAssumeUnchanged.Checked = selectedItems.Any(item => item.Item.IsAssumeUnchanged);

        tsmiStopTracking.Visible = canStopTracking;

        tsmiSkipWorktree.ToolTipText = _skipWorktreeToolTip.Text;
        tsmiAssumeUnchanged.ToolTipText = _assumeUnchangedToolTip.Text;
    }

    private void UpdateSubmodule_Click(object sender, EventArgs e)
    {
        string[] submodules = [.. SelectedItems.Where(it => it.Item.IsSubmodule).Select(it => it.Item.Name).Distinct()];
        FormProcess.ShowDialog(FindForm() as FormBrowse, UiCommands, Commands.SubmoduleUpdate(submodules),
            Module.WorkingDir, null, true);
        RequestRefresh();
    }
}
