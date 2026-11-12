using GitCommands;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using ResourceManager;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;
using GitStash = GitExtensions.Extensibility.Git.GitStash;

namespace GitUI.CommandsDialogs;

public sealed partial class FormStash : GitModuleForm
{
    private readonly AsyncLoader _asyncLoader = new();
    private readonly TranslationString _currentWorkingDirChanges = new("Current working directory changes");
    private readonly TranslationString _noStashes = new("There are no stashes.");

    private readonly CancellationTokenSequence _viewChangesSequence = new();
    private GitStash? _currentWorkingDirStashItem;
    private int _lastSelectedStashIndex = -1;

    public FormStash(IGitUiCommands commands, string? initialStash = null)
        : base(commands)
    {
        InitializeComponent();
        Stashed.Bind(() => RefreshAll());
        Stashed.BindContextMenu(View.CherryPickAllChanges, () => View.SupportLinePatching);
        View.ExtraDiffArgumentsChanged += delegate { StashedSelectedIndexChanged(this, EventArgs.Empty); };
        View.TopScrollReached += FileViewer_TopScrollReached;
        View.BottomScrollReached += FileViewer_BottomScrollReached;
        if (initialStash is not null)
        {
            var initialIndex = initialStash.SubstringAfter('{').SubstringUntil('}');
            if (int.TryParse(initialIndex, out var res))
            {
                _lastSelectedStashIndex = res + 1;
            }
        }

        CompleteTheInitialization();
    }

    public bool ManageStashes { get; set; }

    private void CompleteTheInitialization()
    {
        KeyPreview = true;
        View.EscapePressed += () => DialogResult = DialogResult.Cancel;

        // The buttons and the check boxes above them have to fit even when the Windows accessibility
        // text size enlarges the system font without changing the DPI, which DpiUtil cannot see.
        // Keep the scaled width as a lower bound, so that the layout tuned for high DPI is preserved.
        splitContainer1.SplitterDistance =
            Math.Max(DpiUtil.Scale(280), tableLayoutPanel1.GetPreferredSize(Size.Empty).Width);
        InitializeComplete();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && e.Modifiers == Keys.None)
        {
            var focusedControl = this.FindFocusedControl();

            switch (focusedControl)
            {
                case ComboBox { DroppedDown: true } comboBox:
                    comboBox.DroppedDown = false;
                    break;
                case TextBoxBase { SelectionLength: > 0 } textBox:
                    textBox.SelectionLength = 0;
                    break;
                default:
                    DialogResult = DialogResult.Cancel;
                    break;
            }

            // do not let the modal form react itself on this preview of the Escape key press
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape && e.Modifiers == Keys.None)
        {
            // do not let the modal form react itself on this preview of the Escape key press
            e.SuppressKeyPress = true;
            e.Handled = true;
        }

        base.OnKeyUp(e);
    }

    /// <summary>
    ///     Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        _asyncLoader.Dispose();
        if (disposing)
        {
            _viewChangesSequence.Dispose();
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void FormStashFormClosing(object sender, FormClosingEventArgs e)
    {
        AppSettings.StashKeepIndex = StashKeepIndex.Checked;
        AppSettings.IncludeUntrackedFilesInManualStash = chkIncludeUntrackedFiles.Checked;
    }

    private void FormStashLoad(object sender, EventArgs e)
    {
        StashKeepIndex.Checked = AppSettings.StashKeepIndex;
        chkIncludeUntrackedFiles.Checked = AppSettings.IncludeUntrackedFilesInManualStash;

        ResizeStashesWidth();
    }

    private void Initialize()
    {
        List<GitStash> stashedItems = [.. Module.GetStashes(false)];

        _currentWorkingDirStashItem = new GitStash(-1, _currentWorkingDirChanges.Text);

        stashedItems.Insert(0, _currentWorkingDirStashItem);

        HotkeysEnabled = true;
        LoadHotkeys(HotkeySettingsName);

        Stashes.Text = "";
        StashMessage.Text = "";
        Stashes.SelectedItem = null;
        Stashes.ComboBox.DisplayMember = nameof(GitStash.Summary);
        Stashes.Items.Clear();
        foreach (var stashedItem in stashedItems)
        {
            Stashes.Items.Add(stashedItem);
        }

        if (_lastSelectedStashIndex > 0)
        {
            // Last operation was a drop, select next index
            if (_lastSelectedStashIndex >= Stashes.Items.Count)
            {
                _lastSelectedStashIndex--;
            }

            Stashes.SelectedIndex = _lastSelectedStashIndex;
            _lastSelectedStashIndex = -1;
        }
        else if (ManageStashes && Stashes.Items.Count > 1)
        {
            // more than just the default ("Current working directory changes")
            Stashes.SelectedIndex = 1; // -> auto-select first non-default

            // First load done, show worktree on next refresh
            ManageStashes = false;
        }
        else if (Stashes.Items.Count > 0)
        {
            // (no stashes) -> select default ("Current working directory changes")
            Stashes.SelectedIndex = 0;
        }
    }

    private void InitializeSoft()
    {
        var gitStash = Stashes.SelectedItem as GitStash;

        Stashed.ClearDiffs();

        Loading.Visible = true;
        Loading.IsAnimating = true;
        Stashes.Enabled = false;
        StashMessage.ReadOnly = true;
        if (gitStash == _currentWorkingDirStashItem)
        {
            StashMessage.ReadOnly = false;
            _asyncLoader.LoadAsync(() => Module.GetAllChangedFiles(), LoadGitItemStatuses);
            Clear.Enabled = false; // disallow Drop  (of current working directory)
            Apply.Enabled = false; // disallow Apply (of current working directory)
        }
        else if (gitStash is not null)
        {
            _asyncLoader.LoadAsync(() => Module.GetStashDiffFiles(gitStash.Name), LoadGitItemStatuses);
            Clear.Enabled = true; // allow Drop
            Apply.Enabled = true; // allow Apply
        }
    }

    private void FileViewer_TopScrollReached(object? sender, EventArgs e)
    {
        Stashed.SelectPreviousVisibleItem();
        View.ScrollToBottom();
    }

    private void FileViewer_BottomScrollReached(object? sender, EventArgs e)
    {
        Stashed.SelectNextVisibleItem();
        View.ScrollToTop();
    }

    private void LoadGitItemStatuses(IReadOnlyList<GitItemStatus> gitItemStatuses)
    {
        var gitStash = (GitStash)Stashes.SelectedItem!;
        if (gitStash == _currentWorkingDirStashItem)
        {
            // FileStatusList has no interface for both worktree<-index, index<-HEAD at the same time
            // Must be handled when displaying
            var headId = Module.RevParse("HEAD");
            GitRevision workTreeRev = new(ObjectId.WorkTreeId) { ParentIds = new[] { ObjectId.IndexId } };
            if (headId.IsZero)
            {
                // Likely a detached head
                Stashed.SetDiffs(null, workTreeRev, gitItemStatuses);
            }
            else
            {
                GitRevision headRev = new(headId);
                GitRevision indexRev = new(ObjectId.IndexId) { ParentIds = new[] { headId } };
                List<GitItemStatus> indexItems = [.. gitItemStatuses.Where(item => item.Staged == StagedStatus.Index)];
                List<GitItemStatus> workTreeItems =
                    [.. gitItemStatuses.Where(item => item.Staged != StagedStatus.Index)];
                Stashed.SetStashDiffs(headRev, indexRev, ResourceManager.TranslatedStrings.Index, indexItems,
                    workTreeRev, ResourceManager.TranslatedStrings.Workspace, workTreeItems);
            }
        }
        else
        {
            var firstId = Module.RevParse(gitStash.Name + "^");
            var firstRev = firstId.IsZero ? null : new GitRevision(firstId);

            var selectedId = Module.RevParse(gitStash.Name);
            if (selectedId.IsZero)
            {
                throw new InvalidOperationException("selectedId must not be zero");
            }

            GitRevision secondRev = new(selectedId);
            if (!firstId.IsZero)
            {
                secondRev.ParentIds = new[] { firstId };
            }

            Stashed.SetDiffs(firstRev, secondRev, gitItemStatuses);
        }

        Loading.Visible = false;
        Loading.IsAnimating = false;
        Stashes.Enabled = true;
    }

    private void ResizeStashesWidth()
    {
        const int spacingBetweenLabelAndComboBox = 5;
        var compensationForOddToolbarScaling = DpiUtil.IsFractional ? 1 : 0;
        Stashes.Width = toolStrip1.Width - DpiUtil.Scale(spacingBetweenLabelAndComboBox, true) -
                        compensationForOddToolbarScaling - showToolStripLabel.Width;
    }

    private void StashedSelectedIndexChanged(object sender, EventArgs e)
    {
        var cancellationToken = _viewChangesSequence.Next();
        this.InvokeAndForget(() => View.ViewChangesAsync(Stashed.SelectedItem, cancellationToken),
            cancellationToken: cancellationToken);
        EnablePartialStash();
    }

    private void StashClick(object sender, EventArgs e)
    {
        using (WaitCursorScope.Enter())
        {
            var msg = !string.IsNullOrWhiteSpace(StashMessage.Text) ? " " + StashMessage.Text.Trim() : string.Empty;
            UiCommands.StashSave(this, chkIncludeUntrackedFiles.Checked, StashKeepIndex.Checked, msg);
            Initialize();
        }
    }

    private void StashSelectedFiles_Click(object sender, EventArgs e)
    {
        using (WaitCursorScope.Enter())
        {
            var msg = !string.IsNullOrWhiteSpace(StashMessage.Text) ? " " + StashMessage.Text.Trim() : string.Empty;
            UiCommands.StashSave(this, chkIncludeUntrackedFiles.Checked, StashKeepIndex.Checked, msg,
                Stashed.SelectedItems.Select(i => i.Item.Name).ToList());
            Initialize();
        }
    }

    private void ClearClick(object sender, EventArgs e)
    {
        using (new WaitCursorScope())
        {
            var stashName = GetStashName();
            if (!AppSettings.DontConfirmStashDrop)
            {
                TaskDialogPage page = new()
                {
                    Text = TranslatedStrings.AreYouSure,
                    Caption = TranslatedStrings.StashDropConfirmTitle,
                    Heading = TranslatedStrings.CannotBeUndone,
                    Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
                    Icon = TaskDialogIcon.Information,
                    Verification = new TaskDialogVerificationCheckBox { Text = TranslatedStrings.DontShowAgain },
                    SizeToContent = true
                };

                var result = TaskDialog.ShowDialog(Handle, page);

                if (result == TaskDialogButton.Yes)
                {
                    _lastSelectedStashIndex = Stashes.SelectedIndex;
                    UiCommands.StashDrop(this, stashName);
                    Initialize();
                }

                if (page.Verification.Checked)
                {
                    AppSettings.DontConfirmStashDrop = true;
                }
            }
            else
            {
                _lastSelectedStashIndex = Stashes.SelectedIndex;
                UiCommands.StashDrop(this, stashName);
                Initialize();
            }
        }
    }

    private string GetStashName()
    {
        return ((GitStash)Stashes.SelectedItem!).Name;
    }

    private void ApplyClick(object sender, EventArgs e)
    {
        UiCommands.StashApply(this, GetStashName());
        Initialize();
    }

    private void StashesSelectedIndexChanged(object sender, EventArgs e)
    {
        EnablePartialStash();

        using (WaitCursorScope.Enter())
        {
            InitializeSoft();

            if (Stashes.SelectedItem is GitStash gitStash)
            {
                StashMessage.Text = gitStash != _currentWorkingDirStashItem ? gitStash.Message : "";
            }

            if (Stashes.Items.Count == 1)
            {
                StashMessage.Text = _noStashes.Text;
            }
        }
    }

    private void EnablePartialStash()
    {
        StashSelectedFiles.Enabled = Stashes.SelectedIndex == 0 && Stashed.SelectedItems.Any();
    }

    private void Stashes_DropDown(object sender, EventArgs e)
    {
        Stashes.ResizeDropDownWidth(
            Stashes.Size.Width,
            splitContainer1.Width - (2 * showToolStripLabel.Width),
            false);
    }

    private void RefreshAll(bool force = false)
    {
        if (!force && Stashes.SelectedIndex != 0)
        {
            // Worktree not select, not relevant
            return;
        }

        using (WaitCursorScope.Enter())
        {
            Initialize();
        }
    }

    private void FormStashShown(object sender, EventArgs e)
    {
        // shown when form is first displayed
        RefreshAll(true);
    }

    private void splitContainer1_SplitterMoved(object sender, SplitterEventArgs e)
    {
        ResizeStashesWidth();
    }

    private void FormStash_Resize(object sender, EventArgs e)
    {
        ResizeStashesWidth();
    }

    private void View_KeyUp(object sender, KeyEventArgs e)
    {
        // Close Stash form with escape button while pointer focus is in FileViewer(diff view)
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    #region Hotkey commands

    public static readonly string HotkeySettingsName = "Stash";

    internal enum Command
    {
        NextStash = 0,
        PreviousStash = 1,
        Refresh = 2
    }

    private bool ChangeSelectedStash(bool next = true)
    {
        // Move in list similar to RevGrid, so newest is first in list
        var index = Stashes.SelectedIndex + (next ? -1 : 1);

        if (index >= Stashes.Items.Count || index < 0)
        {
            return false;
        }

        Stashes.SelectedIndex = index;
        return true;
    }

    protected override bool ExecuteCommand(int cmd)
    {
        switch ((Command)cmd)
        {
            case Command.NextStash: return ChangeSelectedStash();
            case Command.PreviousStash: return ChangeSelectedStash(false);
            case Command.Refresh:
                RefreshAll();
                return true;
            default: return base.ExecuteCommand(cmd);
        }
    }

    #endregion
}
