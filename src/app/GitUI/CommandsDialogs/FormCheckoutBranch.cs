using System.ComponentModel;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public partial class FormCheckoutBranch : GitExtensionsDialog
{
    private readonly IGitBranchNameNormaliser _branchNameNormaliser;

    private readonly IReadOnlyList<ObjectId>? _containObjectIds;
    private readonly Dictionary<Control, int> _controls = [];
    private readonly GitBranchNameOptions _gitBranchNameOptions = new(AppSettings.AutoNormaliseSymbol);
    private readonly TranslationString _invalidBranchName = new("An existing branch must be selected.");
    private readonly bool _isLoading;
    private readonly string _rbResetBranchDefaultText;
    private bool? _isDirtyDir;
    private string _localBranchName = "";

    private IReadOnlyList<IGitRef>? _localBranches;
    private string _newLocalBranchName = "";
    private IReadOnlyList<IGitRef>? _remoteBranches;
    private string _remoteName = "";

    public FormCheckoutBranch(IGitUiCommands commands, string branch, bool remote,
        IReadOnlyList<ObjectId>? containObjectIds = null)
        : base(commands, true)
    {
        _branchNameNormaliser = commands.GetRequiredService<IGitBranchNameNormaliser>();
        InitializeComponent();
        InitializeComplete();
        _rbResetBranchDefaultText = rbResetBranch.Text;

        ApplyLayout();
        Shown += FormCheckoutBranchShown;
        _isLoading = true;

        try
        {
            _containObjectIds = containObjectIds;

            LocalBranch.Checked = !remote;
            Remotebranch.Checked = remote;

            PopulateBranches();

            // Set current branch after initialize, because initialize will reset it
            if (!string.IsNullOrEmpty(branch))
            {
                Branches.Items.Add(branch);
                Branches.SelectedItem = branch;
            }

            if (_containObjectIds is not null)
            {
                if (Branches.Items.Count == 0)
                {
                    LocalBranch.Checked = remote;
                    Remotebranch.Checked = !remote;
                    PopulateBranches();
                }
            }

            // The dirty check is very expensive on large repositories. Without this setting
            // the checkout branch dialog is too slow.
            if (AppSettings.CheckForUncommittedChangesInCheckoutBranch)
            {
                _isDirtyDir = Module.IsDirtyDir();
            }
            else
            {
                _isDirtyDir = null;
            }

            localChangesGB.Visible = HasUncommittedChanges;
            ChangesMode = AppSettings.CheckoutBranchAction;
            rbCreateBranchWithCustomName.Checked = AppSettings.CreateLocalBranchForRemote;
        }
        finally
        {
            _isLoading = false;
        }

        return;

        void FormCheckoutBranchShown(object? sender, EventArgs e)
        {
            Shown -= FormCheckoutBranchShown;
            RecalculateSizeConstraints();
        }
    }

    private LocalChangesAction ChangesMode
    {
        get
        {
            if (rbReset.Checked)
            {
                return LocalChangesAction.Reset;
            }

            if (rbMerge.Checked)
            {
                return LocalChangesAction.Merge;
            }

            if (rbStash.Checked)
            {
                return LocalChangesAction.Stash;
            }

            return LocalChangesAction.DontChange;
        }
        set
        {
            rbReset.Checked = value == LocalChangesAction.Reset;
            rbMerge.Checked = value == LocalChangesAction.Merge;
            rbStash.Checked = value == LocalChangesAction.Stash;
            rbDontChange.Checked = value == LocalChangesAction.DontChange;
        }
    }

    private bool HasUncommittedChanges
    {
        get => _isDirtyDir ?? true;
    }

    public DialogResult DoDefaultActionOrShow(IWin32Window? owner)
    {
        var localBranchSelected = !string.IsNullOrWhiteSpace(Branches.Text) && !Remotebranch.Checked;
        if (!AppSettings.AlwaysShowCheckoutBranchDlg && localBranchSelected &&
            (!HasUncommittedChanges || AppSettings.UseDefaultCheckoutBranchAction))
        {
            return PerformCheckout(owner);
        }

        return ShowDialog(owner);
    }

    private void ApplyLayout()
    {
        Control[] controls1 =
        [
            tlpnlBranches,
            horLine,
            tlpnlRemoteOptions,
            localChangesGB
        ];

        localChangesGB.AutoSize = true;
        localChangesGB.Dock = DockStyle.Fill;

        Width = tlpnlMain.PreferredSize.Width + 50;

        int height;
        for (var i = 0; i < controls1.Length; i++)
        {
            var margin = controls1[i].Margin;
            height = controls1[i].Height + margin.Top + margin.Bottom;
            _controls.Add(controls1[i], height);

            tlpnlMain.RowStyles[i].Height = height;
            tlpnlMain.RowStyles[i].SizeType = SizeType.Absolute;

            controls1[i].Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            controls1[i].AutoSize = true;
        }

        tlpnlMain.RowStyles[2].Height = Remotebranch.Checked ? _controls[tlpnlRemoteOptions] : 0;
        tlpnlMain.Height -= _controls[tlpnlRemoteOptions];
    }

    private void PopulateBranches()
    {
        Branches.Items.Clear();

        IEnumerable<string> branchNames;

        if (_containObjectIds is null)
        {
            var branches = LocalBranch.Checked ? GetLocalBranches() : GetRemoteBranches();

            branchNames = branches.Select(b => b.Name);
        }
        else
        {
            branchNames = GetContainsObjectIdBranches();
        }

        Branches.Items.AddRange([.. branchNames.Where(name => !string.IsNullOrWhiteSpace(name))]);

        if (_containObjectIds is not null && Branches.Items.Count == 1)
        {
            Branches.SelectedIndex = 0;
        }
        else
        {
            Branches.Text = null;
        }

        IReadOnlyList<string> GetContainsObjectIdBranches()
        {
            HashSet<string> result = [];
            if (_containObjectIds.Count > 0)
            {
                var branches = Module.GetAllBranchesWhichContainGivenCommit(_containObjectIds[0],
                        LocalBranch.Checked,
                        !LocalBranch.Checked,
                        default)
                    .Where(a => !DetachedHeadParser.IsDetachedHead(a) &&
                                !a.EndsWith("/HEAD"));
                result.UnionWith(branches);
            }

            for (var index = 1; index < _containObjectIds.Count; index++)
            {
                var containObjectId = _containObjectIds[index];
                var branches =
                    Module.GetAllBranchesWhichContainGivenCommit(containObjectId,
                            LocalBranch.Checked,
                            !LocalBranch.Checked,
                            default)
                        .Where(a => !DetachedHeadParser.IsDetachedHead(a) &&
                                    !a.EndsWith("/HEAD"));
                result.IntersectWith(branches);
            }

            return result.ToList();
        }
    }

    private void OkClick(object sender, EventArgs e)
    {
        DialogResult = PerformCheckout(this);
        if (DialogResult == DialogResult.OK)
        {
            Close();
        }
    }

    private DialogResult PerformCheckout(IWin32Window? owner)
    {
        // Ok button set as the "AcceptButton" for the form
        // if the user hits [Enter] at any point, we need to trigger txtCustomBranchName Leave event
        Ok.Focus();

        var branchName = Branches.Text.Trim();
        var isRemote = Remotebranch.Checked;
        string? newBranchName = null;
        var newBranchMode = CheckoutNewBranchMode.DontCreate;

        if (isRemote)
        {
            if (rbCreateBranchWithCustomName.Checked)
            {
                newBranchName = txtCustomBranchName.Text.Trim();
                newBranchMode = CheckoutNewBranchMode.Create;
                if (string.IsNullOrWhiteSpace(newBranchName))
                {
                    MessageBoxes.Show(_customBranchNameIsEmpty.Text, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return DialogResult.None;
                }

                if (!Module.CheckBranchFormat(newBranchName))
                {
                    MessageBoxes.Show(string.Format(_customBranchNameIsNotValid.Text, newBranchName), Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.None;
                    return DialogResult.None;
                }
            }
            else if (rbResetBranch.Checked)
            {
                var localBranchRef = GetLocalBranchRef(_localBranchName);
                var remoteBranchRef = GetRemoteBranchRef(branchName);
                if (localBranchRef is not null && remoteBranchRef is not null && !localBranchRef.ObjectId.IsZero &&
                    !remoteBranchRef.ObjectId.IsZero)
                {
                    var mergeBaseId = Module.GetMergeBase(localBranchRef.ObjectId, remoteBranchRef.ObjectId);
                    var isResetFastForward = localBranchRef.ObjectId == mergeBaseId;

                    if (!isResetFastForward)
                    {
                        var mergeBaseText = mergeBaseId.IsZero
                            ? "merge base"
                            : mergeBaseId.ToShortString();

                        var warningMessage = string.Format(_resetNonFastForwardBranch.Text, _localBranchName,
                            mergeBaseText);

                        if (MessageBoxes.Show(this, warningMessage, _resetCaption.Text, MessageBoxButtons.YesNo,
                                MessageBoxIcon.Exclamation) == DialogResult.No)
                        {
                            DialogResult = DialogResult.None;
                            return DialogResult.None;
                        }
                    }
                }

                newBranchMode = CheckoutNewBranchMode.Reset;
                newBranchName = _localBranchName;
            }
            else
            {
                newBranchMode = CheckoutNewBranchMode.DontCreate;
            }
        }

        var localChanges = ChangesMode;
        if (localChanges != LocalChangesAction.Reset && chkSetLocalChangesActionAsDefault.Checked)
        {
            AppSettings.CheckoutBranchAction = localChanges;
        }

        if ((!Visible && !AppSettings.UseDefaultCheckoutBranchAction) || !HasUncommittedChanges)
        {
            localChanges = LocalChangesAction.DontChange;
        }

        var stash = false;
        if (localChanges == LocalChangesAction.Stash)
        {
            if (_isDirtyDir is null && Visible)
            {
                _isDirtyDir = Module.IsDirtyDir();
            }

            stash = _isDirtyDir == true;
            if (stash)
            {
                UiCommands.StashSave(owner, AppSettings.IncludeUntrackedFilesInAutoStash);
            }
        }

        var originalId = Module.GetCurrentCheckout();

        var success = ScriptsRunner.RunEventScripts(ScriptEvent.BeforeCheckout, this);
        if (!success)
        {
            return DialogResult.Cancel;
        }

        if (UiCommands.StartCommandLineProcessDialog(owner,
                Commands.CheckoutBranch(branchName, isRemote, localChanges, newBranchMode, newBranchName)))
        {
            if (stash)
            {
                var messageBoxResult = AppSettings.AutoPopStashAfterCheckoutBranch;
                if (messageBoxResult is null)
                {
                    TaskDialogPage page = new()
                    {
                        Text = _applyStashedItemsAgain.Text,
                        Caption = _applyStashedItemsAgainCaption.Text,
                        Icon = TaskDialogIcon.Information,
                        Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
                        Verification =
                            new TaskDialogVerificationCheckBox { Text = TranslatedStrings.DontShowAgain },
                        AllowCancel = true,
                        SizeToContent = true
                    };

                    var answer = TaskDialog.ShowDialog(Handle, page);
                    messageBoxResult = answer == TaskDialogButton.Yes;

                    // Dismissing the dialog is not an answer, so it must not be remembered as one
                    if (page.Verification.Checked && answer != TaskDialogButton.Cancel)
                    {
                        AppSettings.AutoPopStashAfterCheckoutBranch = messageBoxResult;
                    }
                }

                if (messageBoxResult ?? false)
                {
                    UiCommands.StashPop(this);
                }
            }

            var currentId = Module.GetCurrentCheckout();

            if (originalId != currentId)
            {
                UiCommands.UpdateSubmodules(this);
            }

            ScriptsRunner.RunEventScripts(ScriptEvent.AfterCheckout, this);

            return DialogResult.OK;
        }

        return DialogResult.None;

        IGitRef GetLocalBranchRef(string name)
        {
            return GetLocalBranches()
                .FirstOrDefault(head => head.Name.Equals(name, StringComparison.OrdinalIgnoreCase))!;
        }

        IGitRef GetRemoteBranchRef(string name)
        {
            return GetRemoteBranches()
                .FirstOrDefault(head => head.Name.Equals(name, StringComparison.OrdinalIgnoreCase))!;
        }
    }

    private void LocalBranchCheckedChanged(object sender, EventArgs e)
    {
        // We only need to refresh the dialog once -> RemoteBranchCheckedChanged will trigger this
        ////BranchTypeChanged();
    }

    private void RemoteBranchCheckedChanged(object sender, EventArgs e)
    {
        RecalculateSizeConstraints();

        if (!_isLoading)
        {
            PopulateBranches();
        }

        Branches_SelectedIndexChanged(sender, e);
    }

    private void rbCreateBranchWithCustomName_CheckedChanged(object sender, EventArgs e)
    {
        txtCustomBranchName.Enabled = rbCreateBranchWithCustomName.Checked;
        if (rbCreateBranchWithCustomName.Checked)
        {
            txtCustomBranchName.SelectAll();
        }
    }

    private void Branches_SelectedIndexChanged(object sender, EventArgs e)
    {
        lbChanges.Text = "";

        var branch = Branches.Text;
        if (string.IsNullOrWhiteSpace(branch) || !Remotebranch.Checked)
        {
            _remoteName = string.Empty;
            _localBranchName = string.Empty;
            _newLocalBranchName = string.Empty;
        }
        else
        {
            _remoteName = GitRefName.GetRemoteName(branch, Module.GetRemoteNames());
            _localBranchName = Module.GetLocalTrackingBranchName(_remoteName, branch) ?? "";
            var remoteBranchName = _remoteName.Length > 0 ? branch[(_remoteName.Length + 1)..] : branch;
            _newLocalBranchName = string.Concat(_remoteName, "_", remoteBranchName);
            var i = 2;
            while (LocalBranchExists(_newLocalBranchName))
            {
                _newLocalBranchName = string.Concat(_remoteName, "_", _localBranchName, "_", i.ToString());
                i++;
            }
        }

        var existsLocalBranch = LocalBranchExists(_localBranchName);

        rbResetBranch.Text = existsLocalBranch ? _rbResetBranchDefaultText : _createBranch.Text;
        branchName.Text = "'" + _localBranchName + "'";
        txtCustomBranchName.Text = _newLocalBranchName;

        if (string.IsNullOrWhiteSpace(branch))
        {
            lbChanges.Text = "";
        }
        else
        {
            ThreadHelper.FileAndForget(async () =>
            {
                // not applicable if there is no checkout yet
                var aheadBehindInfo = "";

                var currentCheckout = Module.GetCurrentCheckout();
                if (!currentCheckout.IsZero)
                {
                    aheadBehindInfo = Module.GetCommitCountString(currentCheckout, branch);
                }

                await this.SwitchToMainThreadAsync();

                lbChanges.Text = aheadBehindInfo;
            });
        }

        return;

        bool LocalBranchExists(string name)
        {
            return GetLocalBranches().Any(head => head.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }

    private IEnumerable<IGitRef> GetLocalBranches()
    {
        return _localBranches ??= Module.GetRefs(RefsFilter.Heads);
    }

    private IEnumerable<IGitRef> GetRemoteBranches()
    {
        return _remoteBranches ??= Module.GetRefs(RefsFilter.Remotes);
    }

    private void FormCheckoutBranch_Activated(object sender, EventArgs e)
    {
        Branches.Focus();
    }

    private void RecalculateSizeConstraints()
    {
        SuspendLayout();
        MinimumSize = MaximumSize = Size.Empty;

        tlpnlRemoteOptions.Visible = Remotebranch.Checked;
        tlpnlMain.RowStyles[2].Height = Remotebranch.Checked ? _controls[tlpnlRemoteOptions] : 0;
        tlpnlMain.Height = _controls.Select(c => c.Key.Visible ? c.Value : 0).Sum() + tlpnlMain.Padding.Top +
                           tlpnlMain.Padding.Bottom;
        var height = ControlsPanel.Height + MainPanel.Padding.Top + MainPanel.Padding.Bottom
                     + tlpnlMain.Height + tlpnlMain.Margin.Top + tlpnlMain.Margin.Bottom + DpiUtil.Scale(30);

        MinimumSize = new Size(tlpnlMain.PreferredSize.Width + DpiUtil.Scale(70), height);
        MaximumSize = new Size(Screen.PrimaryScreen!.Bounds.Width, height);
        Size = new Size(Width, height);
        ResumeLayout();
    }

    private void rbReset_CheckedChanged(object sender, EventArgs e)
    {
        chkSetLocalChangesActionAsDefault.Enabled = !rbReset.Checked;
        if (rbReset.Checked)
        {
            chkSetLocalChangesActionAsDefault.Checked = false;
        }
    }

    private void txtCustomBranchName_Leave(object sender, EventArgs e)
    {
        if (!AppSettings.AutoNormaliseBranchName || !txtCustomBranchName.Text.Any(PathUtil.IsValidPathChar))
        {
            return;
        }

        var caretPosition = txtCustomBranchName.SelectionStart;
        var normalisedBranchName = _branchNameNormaliser.Normalise(txtCustomBranchName.Text, _gitBranchNameOptions);
        txtCustomBranchName.Text = normalisedBranchName;
        txtCustomBranchName.SelectionStart = caretPosition;
    }

    private void Branches_Validating(object sender, CancelEventArgs e)
    {
        e.Cancel = Branches.SelectedIndex == -1 || !Branches.Items.Contains(Branches.Text);
        Errors.SetError(Branches, e.Cancel ? _invalidBranchName.ToString() : "");
    }

    private void Branches_TextChanged(object sender, EventArgs e)
    {
        Errors.SetError(Branches, "");
    }

    #region Translation

    private readonly TranslationString _customBranchNameIsEmpty =
        new("Custom branch name is empty.\nEnter valid branch name or select predefined value.");

    private readonly TranslationString _customBranchNameIsNotValid =
        new("“{0}” is not valid branch name.\nEnter valid branch name or select predefined value.");

    private readonly TranslationString _createBranch =
        new("Cr&eate local branch with same name:");

    private readonly TranslationString _applyStashedItemsAgainCaption =
        new("Auto stash");

    private readonly TranslationString _applyStashedItemsAgain =
        new("Apply stashed items to working directory again?");

    private readonly TranslationString _resetNonFastForwardBranch =
        new(
            "You are going to reset the “{0}” branch to a new location discarding ALL the commited changes since the {1} revision.\n\nAre you sure?");

    private readonly TranslationString _resetCaption = new("Reset branch");

    #endregion
}
