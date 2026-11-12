using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtensions.Extensibility.Settings;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.HelperDialogs;
using GitUI.Properties;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI.CommandsDialogs;

/// <summary>Form to merge a branch into the current branch.</summary>
public partial class FormMergeBranch : GitModuleForm
{
    private readonly ICommitMessageManager _commitMessageManager;
    private readonly string? _defaultBranch;

    private readonly TranslationString _formMergeBranchHoverShowImageLabelText =
        new("Hover to see scenario when fast forward is possible.");

    /// <summary>Initializes <see cref="FormMergeBranch" />.</summary>
    /// <param name="defaultBranch">Branch to merge into the current branch.</param>
    public FormMergeBranch(IGitUiCommands commands, string? defaultBranch)
        : base(commands)
    {
        InitializeComponent();
        helpImageDisplayUserControl1.Image1 = Images.HelpCommandMerge.AdaptLightness();
        helpImageDisplayUserControl1.Image2 = Images.HelpCommandMergeFastForward.AdaptLightness();
        InitializeComplete();

        _commitMessageManager = new CommitMessageManager(this, Module.WorkingDirGitDir, Module.CommitEncoding);

        currentBranchLabel.Font = new Font(currentBranchLabel.Font, FontStyle.Bold);
        noCommit.Checked = AppSettings.DontCommitMerge;

        helpImageDisplayUserControl1.IsOnHoverShowImage2NoticeText = _formMergeBranchHoverShowImageLabelText.Text;
        helpImageDisplayUserControl1.Visible = !AppSettings.DontShowHelpImages;
        _defaultBranch = defaultBranch;

        var effectiveSettings = Module.GetEffectiveSettings();
        var detachedSettings = effectiveSettings.Detached();

        noFastForward.Checked = detachedSettings.NoFastForwardMerge;

        addLogMessages.Checked = DetailedSettings.AddMergeLogMessages.ValueOrDefault(effectiveSettings);
        nbMessages.Value = DetailedSettings.MergeLogMessagesCount.ValueOrDefault(effectiveSettings);

        advanced.Checked = AppSettings.AlwaysShowAdvOpt;
        advanced_CheckedChanged(this, EventArgs.Empty);

        Branches.Select();
    }

    private void FormMergeBranchLoad(object sender, EventArgs e)
    {
        var selectedHead = Module.GetSelectedBranch();
        currentBranchLabel.Text = selectedHead;

        // Offer rebase on refs also for tags (but not stash, notes etc)
        Branches.BranchesToSelect = Module.GetRefs(RefsFilter.Heads | RefsFilter.Remotes | RefsFilter.Tags);

        if (_defaultBranch is not null)
        {
            Branches.SetSelectedText(_defaultBranch);
        }
        else
        {
            var merge = Module.GetRemoteBranch(selectedHead);
            if (!string.IsNullOrEmpty(merge))
            {
                Branches.SetSelectedText(merge);
            }
        }

        Branches.Select();
    }

    private void OkClick(object sender, EventArgs e)
    {
        var detachedSettings = Module.GetEffectiveSettings().Detached();

        detachedSettings.NoFastForwardMerge = noFastForward.Checked;
        AppSettings.DontCommitMerge = noCommit.Checked;

        var success = ScriptsRunner.RunEventScripts(ScriptEvent.BeforeMerge, this);
        if (!success)
        {
            return;
        }

        string? mergeMessagePath = null;
        if (addMergeMessage.Checked)
        {
            // [!] Do not reset the last commit message stored in AppSettings.LastCommitMessage

            ThreadHelper.JoinableTaskFactory.Run(() => _commitMessageManager.WriteCommitMessageToFileAsync(
                mergeMessage.Text, CommitMessageType.Merge,
                false,
                false));
            mergeMessagePath = _commitMessageManager.MergeMessagePath;
        }

        var command = Commands.MergeBranch(Branches.GetSelectedText(),
            fastForward.Checked,
            squash.Checked,
            noCommit.Checked,
            _NO_TRANSLATE_mergeStrategy.Text,
            allowUnrelatedHistories.Checked,
            mergeMessagePath,
            Module.GetPathForGitExecution,
            addLogMessages.Checked ? (int)nbMessages.Value : null);
        success = FormProcess.ShowDialog(this, UiCommands, command, Module.WorkingDir, null, true,
            out var commandOutput);

        var wasConflict = MergeConflictHandler.HandleMergeConflicts(UiCommands, this, !noCommit.Checked)
                          || Module.CanContinueAction(commandOutput);

        if (success || wasConflict)
        {
            ScriptsRunner.RunEventScripts(ScriptEvent.AfterMerge, this);
            UiCommands.RepoChangedNotifier.Notify();
            Close();
        }
    }

    private void NonDefaultMergeStrategy_CheckedChanged(object sender, EventArgs e)
    {
        _NO_TRANSLATE_mergeStrategy.Visible = NonDefaultMergeStrategy.Checked;
        strategyHelp.Visible = NonDefaultMergeStrategy.Checked;

        if (!advanced.Checked)
        {
            _NO_TRANSLATE_mergeStrategy.Text = "";
        }
    }

    private void strategyHelp_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        OsShellUtil.OpenUrlInDefaultBrowser(
            UserManual.UserManual.UrlFor("branches", "advanced-merge-options"));
    }

    private void advanced_CheckedChanged(object sender, EventArgs e)
    {
        advancedPanel.Visible = advanced.Checked;
        NonDefaultMergeStrategy_CheckedChanged(this, EventArgs.Empty);

        if (!advanced.Checked)
        {
            NonDefaultMergeStrategy.Checked = false;
            squash.Checked = false;
            allowUnrelatedHistories.Checked = false;
            addMergeMessage.Checked = false;
        }
    }

    private void fastForward_CheckedChanged(object sender, EventArgs e)
    {
        helpImageDisplayUserControl1.IsOnHoverShowImage2 = true;
    }

    private void noFastForward_CheckedChanged(object sender, EventArgs e)
    {
        helpImageDisplayUserControl1.IsOnHoverShowImage2 = false;
        squash.Enabled = !noFastForward.Checked;

        if (noFastForward.Checked)
        {
            squash.Checked = false;
        }
    }

    private void addMessages_CheckedChanged(object sender, EventArgs e)
    {
        nbMessages.Enabled = addLogMessages.Checked;

        DetailedSettings.AddMergeLogMessages[Module.GetEffectiveSettings()] = addLogMessages.Checked;
    }

    private void addMergeMessage_CheckedChanged(object sender, EventArgs e)
    {
        mergeMessage.Enabled = addMergeMessage.Checked;
    }

    private void nbMessages_ValueChanged(object sender, EventArgs e)
    {
        DetailedSettings.MergeLogMessagesCount[Module.GetEffectiveSettings()] = Convert.ToInt32(nbMessages.Value);
    }
}
