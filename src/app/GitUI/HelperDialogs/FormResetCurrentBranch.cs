using System.ComponentModel;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI.Theming;
using ResourceManager;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.HelperDialogs;

public partial class FormResetCurrentBranch : GitModuleForm
{
    public enum ResetType
    {
        Soft,
        Mixed,
        Keep,
        Merge,
        Hard
    }

    private readonly TranslationString _branchInfo = new("Reset branch '{0}' to revision:");
    private readonly TranslationString _resetCaption = new("Reset branch");

    private readonly TranslationString _resetHardWarning =
        new("You are about to discard ALL local changes, are you sure?");

    private FormResetCurrentBranch(IGitUiCommands commands, GitRevision revision, ResetType resetType)
        : base(commands)
    {
        Revision = revision;

        InitializeComponent();
        Soft.SetForeColorForBackColor();
        Mixed.SetForeColorForBackColor();
        Keep.ForeColor = Mixed.ForeColor;
        Merge.ForeColor = Mixed.ForeColor;
        Hard.SetForeColorForBackColor();
        InitializeComplete();

        switch (resetType)
        {
            case ResetType.Soft:
                Soft.Checked = true;
                break;
            case ResetType.Mixed:
                Mixed.Checked = true;
                break;
            case ResetType.Keep:
                Keep.Checked = true;
                break;
            case ResetType.Merge:
                Merge.Checked = true;
                break;
            case ResetType.Hard:
                Hard.Checked = true;
                break;
        }
    }

    public GitRevision Revision { get; }

    public static FormResetCurrentBranch Create(IGitUiCommands commands, GitRevision revision,
        ResetType resetType = ResetType.Soft)
    {
        return new FormResetCurrentBranch(commands,
            revision ?? throw new NotSupportedException(TranslatedStrings.NoRevision), resetType);
    }

    private void FormResetCurrentBranch_Load(object sender, EventArgs e)
    {
        _NO_TRANSLATE_BranchInfo.Text = string.Format(_branchInfo.Text, Module.GetSelectedBranch());
        commitSummaryUserControl1.Revision = Revision;
    }

    private void Ok_Click(object sender, EventArgs e)
    {
        var updateSubmodules = AppSettings.UpdateSubmodulesOnCheckout is true && Module.HasSubmodules() &&
                               Revision.ObjectId != Module.GetCurrentCheckout();

        if (Soft.Checked)
        {
            FormProcess.ShowDialog(this, UiCommands, Commands.Reset(ResetMode.Soft, Revision.Guid, quiet: false),
                Module.WorkingDir, null, true);
        }
        else if (Mixed.Checked)
        {
            FormProcess.ShowDialog(this, UiCommands, Commands.Reset(ResetMode.Mixed, Revision.Guid, quiet: false),
                Module.WorkingDir, null, true);
        }
        else if (Hard.Checked)
        {
            if (MessageBoxes.Show(this, _resetHardWarning.Text, _resetCaption.Text, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Exclamation) == DialogResult.Yes)
            {
                var currentCheckout = Module.GetCurrentCheckout();
                var success = FormProcess.ShowDialog(this, UiCommands,
                    Commands.Reset(ResetMode.Hard, Revision.Guid, quiet: false), Module.WorkingDir, null, true);
                if (success)
                {
                    if (currentCheckout != Revision.ObjectId)
                    {
                        UiCommands.UpdateSubmodules(this);
                    }
                }
            }
            else
            {
                return;
            }
        }
        else if (Merge.Checked)
        {
            FormProcess.ShowDialog(this, UiCommands, Commands.Reset(ResetMode.Merge, Revision.Guid, quiet: false),
                Module.WorkingDir, null, true);
        }
        else if (Keep.Checked)
        {
            FormProcess.ShowDialog(this, UiCommands, Commands.Reset(ResetMode.Keep, Revision.Guid, quiet: false),
                Module.WorkingDir, null, true);
        }

        if (updateSubmodules)
        {
            UiCommands.StartUpdateSubmodulesDialog(this);
        }

        UiCommands.RepoChangedNotifier.Notify();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Cancel_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void FormResetCurrentBranch_HelpButtonClicked(object sender, CancelEventArgs e)
    {
        string? helpSection = default;
        if (Soft.Checked)
        {
            helpSection = "--soft";
        }
        else if (Mixed.Checked)
        {
            helpSection = "--mixed";
        }
        else if (Keep.Checked)
        {
            helpSection = "--keep";
        }
        else if (Merge.Checked)
        {
            helpSection = "--merge";
        }
        else if (Hard.Checked)
        {
            helpSection = "--hard";
        }

        OsShellUtil.OpenUrlInDefaultBrowser(
            @$"https://git-scm.com/docs/git-reset#Documentation/git-reset.txt-{helpSection}");
    }
}
