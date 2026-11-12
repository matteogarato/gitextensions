using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.Properties;

namespace GitUI.CommandsDialogs;

partial class FormBrowse
{
    // This file is dedicated to init logic for FormBrowse revisiong grid control

    private void InitRevisionGrid(ObjectId selectedId, ObjectId firstId, bool isFileHistoryMode)
    {
        RevisionGridControl.ArtificialChanged += (_, _) => RefreshGitStatusMonitor();

        RevisionGridControl.IndexWatcher.Changed += (_, args) =>
        {
            var indexChanged = args.IsIndexChanged;
            this.InvokeAndForget(() =>
                RefreshButton.Image = indexChanged && AppSettings.ShowGitStatusInBrowseToolbar &&
                                      Module.IsValidGitWorkingDir()
                    ? Images.ReloadRevisionsDirty
                    : Images.ReloadRevisions);
        };

        RevisionGridControl.MenuCommands.MenuChanged += (sender, e) => _formBrowseMenus.OnMenuCommandsPropertyChanged();

        RevisionGridControl.FilterChanged += (sender, e) =>
        {
            Text = _appTitleGenerator.Generate(Module.WorkingDir, Module.IsValidGitWorkingDir(),
                RevisionGridControl.CurrentBranch.Value, TranslatedStrings.NoBranch, e.PathFilter);

            // PathFilter is a free text field and may contain wildcards, quoting is optional.
            // This is will adjust the string at least for paths added from context menus.
            var path = e.PathFilter;
            if (path?.Length is > 1 && path[0] == '"' && path[^1] == '"')
            {
                path = path[1..^1];
            }

            if (!string.IsNullOrWhiteSpace(path))
            {
                var relativePath = RelativePath.From(path);
                revisionDiff.FallbackFollowedFile = relativePath;
                fileTree.FallbackFollowedFile = relativePath;
            }
        };

        var firstTimeInFileHistoryMode = isFileHistoryMode;
        RevisionGridControl.RevisionsLoading += (sender, e) =>
        {
            // Open diff in "filehistory" mode
            if (firstTimeInFileHistoryMode)
            {
                firstTimeInFileHistoryMode = false;
                CommitInfoTabControl.SelectedTab = DiffTabPage;
            }

            if (sender is null || MainSplitContainer.Panel1Collapsed)
            {
                // - the event is either not originated from the revision grid, or
                // - the left panel is hidden
                return;
            }

            RefreshLeftPanel(e.GetRefs, e.GetStashRevs, e.ForceRefresh);
        };

        RevisionGridControl.RevisionsLoaded += (sender, e) =>
        {
            UiCommands.GetRequiredService<IRepositoryHistoryUiService>().TriggerBranchNameCacheUpdate();

            if (sender is null || MainSplitContainer.Panel1Collapsed)
            {
                // - the event is either not originated from the revision grid, or
                // - the left panel is hidden
                return;
            }

            repoObjectsTree.RefreshRevisionsLoaded();
        };

        RevisionGridControl.SelectionChanged += (sender, e) =>
        {
            _selectedRevisionUpdatedTargets = UpdateTargets.None;
            RefreshSelection();
        };

        RevisionGridControl.ToggledBetweenArtificialAndHeadCommits += (s, e) =>
        {
            if (!revisionDiff.Visible)
            {
                CommitInfoTabControl.SelectedTab = DiffTabPage;
            }

            if (revisionDiff.Visible)
            {
                // force focus of file list
                revisionDiff.SwitchFocus(false);
            }
        };

        RevisionGridControl.SelectedId = selectedId;
        RevisionGridControl.FirstId = firstId;

        RevisionGridControl.SelectInLeftPanel = SelectInLeftPanel;

        return;

        void SelectInLeftPanel(string gitRef)
        {
            if (MainSplitContainer.Panel1Collapsed)
            {
                toggleLeftPanel.PerformClick();
            }

            repoObjectsTree.SelectGitRef(gitRef);
            repoObjectsTree.Focus();
        }
    }
}
