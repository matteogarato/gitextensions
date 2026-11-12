using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI.Theming;
using GitUI.HelperDialogs;
using GitUI.UserControls;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public partial class FormApplyPatch : GitModuleForm
{
    private static readonly List<PatchFile> _skipped = [];

    public FormApplyPatch(IGitUiCommands commands)
        : base(commands)
    {
        InitializeComponent();
        InitializeComplete();

        PatchGrid.SetSkipped(_skipped);

        SolveMergeConflicts.BackColor = OtherColors.MergeConflictsColor;
        SolveMergeConflicts.SetForeColorForBackColor();
    }

    private void FormApplyPatchLoad(object sender, EventArgs e)
    {
        EnableButtons();
    }

    public void SetPatchFile(string name)
    {
        PatchFileMode.Checked = true;
        PatchFile.Text = name;
    }

    public void SetPatchDir(string name)
    {
        PatchDirMode.Checked = true;
        PatchDir.Text = name;
    }

    private void EnableButtons()
    {
        if (Module.InTheMiddleOfPatch())
        {
            Apply.Enabled = false;
            IgnoreWhitespace.Enabled = false;
            SignOff.Enabled = false;
            PatchFileMode.Enabled = false;
            PatchDirMode.Enabled = false;
            AddFiles.Enabled = true;
            Resolved.Enabled = !Module.InTheMiddleOfConflictedMerge();
            Mergetool.Enabled = Module.InTheMiddleOfConflictedMerge();
            Skip.Enabled = true;
            Abort.Enabled = true;

            PatchFile.Enabled = false;
            PatchFile.ReadOnly = false;
            BrowsePatch.Enabled = false;
            PatchDir.Enabled = false;
            PatchDir.ReadOnly = false;
            BrowseDir.Enabled = false;
        }
        else
        {
            PatchFile.Enabled = PatchFileMode.Checked;
            PatchFile.ReadOnly = !PatchFileMode.Checked;
            BrowsePatch.Enabled = PatchFileMode.Checked;
            PatchDir.Enabled = PatchDirMode.Checked;
            PatchDir.ReadOnly = !PatchDirMode.Checked;
            BrowseDir.Enabled = PatchDirMode.Checked;

            Apply.Enabled = true;
            IgnoreWhitespace.Enabled = true;
            SignOff.Enabled = true;
            PatchFileMode.Enabled = true;
            PatchDirMode.Enabled = true;
            AddFiles.Enabled = false;
            Resolved.Enabled = false;
            Mergetool.Enabled = false;
            Skip.Enabled = false;
            Abort.Enabled = false;
        }

        if (PatchGrid.PatchFiles is null || PatchGrid.PatchFiles.Count == 0)
        {
            PatchGrid.Initialize();
        }
        else
        {
            PatchGrid.RefreshGrid();
        }

        SolveMergeConflicts.Visible = Module.InTheMiddleOfConflictedMerge();

        Resolved.Text = _conflictResolvedText.Text;
        Mergetool.Text = _conflictMergetoolText.Text;
        ContinuePanel.BackColor = Color.Transparent;
        MergeToolPanel.BackColor = Color.Transparent;

        if (Module.InTheMiddleOfConflictedMerge())
        {
            Mergetool.Text = ">" + _conflictMergetoolText.Text + "<";
            Mergetool.Focus();
            AcceptButton = Mergetool;
            MergeToolPanel.BackColor = SystemColors.ControlText;
        }
        else if (Module.InTheMiddleOfPatch())
        {
            Resolved.Text = ">" + _conflictResolvedText.Text + "<";
            Resolved.Focus();
            AcceptButton = Resolved;
            ContinuePanel.BackColor = SystemColors.ControlText;
        }
    }

    private string SelectPatchFile(string initialDirectory)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = _selectPatchFileFilter.Text + "|*.patch",
            InitialDirectory = initialDirectory,
            Title = _selectPatchFileCaption.Text
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : PatchFile.Text;
    }

    private void BrowsePatch_Click(object sender, EventArgs e)
    {
        PatchFile.Text = SelectPatchFile(@".");
    }

    private void Apply_Click(object sender, EventArgs e)
    {
        var patchFile = PatchFile.Text;
        var dirText = PatchDir.Text;
        var ignoreWhiteSpace = IgnoreWhitespace.Checked;
        var signOff = SignOff.Checked;

        if (string.IsNullOrEmpty(patchFile) && string.IsNullOrEmpty(dirText))
        {
            MessageBoxes.Show(this, _noFileSelectedText.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        using (WaitCursorScope.Enter())
        {
            _skipped.Clear();

            if (PatchFileMode.Checked)
            {
                var arguments = IsDiffFile(patchFile)
                    ? Commands.ApplyDiffPatch(ignoreWhiteSpace, patchFile, Module.GetPathForGitExecution)
                    : Commands.ApplyMailboxPatch(signOff, ignoreWhiteSpace, patchFile, Module.GetPathForGitExecution);

                FormProcess.ShowDialog(this, UiCommands, arguments, Module.WorkingDir, null, true);
            }
            else
            {
                // No need for PathUtil.GetRepoPath(), file streamed
                var arguments = Commands.ApplyMailboxPatch(signOff, ignoreWhiteSpace);

                Module.ApplyPatch(dirText, arguments);
            }

            UiCommands.RepoChangedNotifier.Notify();

            EnableButtons();

            if (!Module.InTheMiddleOfAction() && !Module.InTheMiddleOfPatch())
            {
                Close();
            }
        }
    }

    // look into patch file and try to figure out if it's a raw diff (i.e from git diff -p)
    // only looks at start, as all we want is to tell from automail format
    // returns false on any problem, never throws
    private static bool IsDiffFile(string path)
    {
        try
        {
            using StreamReader sr = new(path);
            var line = sr.ReadLine();

            return line is not null && (line.StartsWith("diff ") || line.StartsWith("Index: "));
        }
        catch
        {
            return false;
        }
    }

    private void Mergetool_Click(object sender, EventArgs e)
    {
        UiCommands.StartResolveConflictsDialog(this);
        EnableButtons();
    }

    private void Skip_Click(object sender, EventArgs e)
    {
        using (WaitCursorScope.Enter())
        {
            var applyingPatch = PatchGrid.PatchFiles!.FirstOrDefault(p => p.IsNext);
            if (applyingPatch is not null)
            {
                applyingPatch.IsSkipped = true;
                _skipped.Add(applyingPatch);
            }

            FormProcess.ShowDialog(this, UiCommands, Commands.Skip(), Module.WorkingDir, null, true);
            EnableButtons();
        }
    }

    private void Resolved_Click(object sender, EventArgs e)
    {
        using (WaitCursorScope.Enter())
        {
            FormProcess.ShowDialog(this, UiCommands, Commands.Resolved(), Module.WorkingDir, null, true);
            EnableButtons();
        }
    }

    private void Abort_Click(object sender, EventArgs e)
    {
        using (WaitCursorScope.Enter())
        {
            FormProcess.ShowDialog(this, UiCommands, Commands.Abort(), Module.WorkingDir, null, true);
            _skipped.Clear();
            EnableButtons();
        }
    }

    private void AddFiles_Click(object sender, EventArgs e)
    {
        UiCommands.StartAddFilesDialog(this);
    }

    private void MergePatch_Load(object sender, EventArgs e)
    {
        PatchFile.Select();

        Text = _applyPatchMsgBox.Text + " (" + Module.WorkingDir + ")";
        IgnoreWhitespace.Checked = AppSettings.ApplyPatchIgnoreWhitespace;
        SignOff.Checked = AppSettings.ApplyPatchSignOff;
    }

    private void BrowseDir_Click(object sender, EventArgs e)
    {
        var userSelectedPath = OsShellUtil.PickFolder(this);

        if (userSelectedPath is not null)
        {
            PatchDir.Text = userSelectedPath;
        }
    }

    private void PatchFileMode_CheckedChanged(object sender, EventArgs e)
    {
        EnableButtons();
    }

    private void SolveMergeConflicts_Click(object sender, EventArgs e)
    {
        Mergetool_Click(sender, e);
    }

    private void IgnoreWhitespace_CheckedChanged(object sender, EventArgs e)
    {
        AppSettings.ApplyPatchIgnoreWhitespace = IgnoreWhitespace.Checked;
    }

    private void SignOff_CheckedChanged(object sender, EventArgs e)
    {
        AppSettings.ApplyPatchSignOff = SignOff.Checked;
    }

    #region Mnemonics

    // Available: CEGHJLMNPQTUVXYZ
    // A add files
    // B abort
    // D directory
    // F file
    // I ignore whitespace
    // K skip patch
    // O sign-off
    // R Browse file
    // S solve conflicts
    // W Browse dir

    #endregion

    #region Translation

    private readonly TranslationString _conflictResolvedText =
        new("Conflicts resolved");

    private readonly TranslationString _conflictMergetoolText =
        new("&Solve conflicts");

    private readonly TranslationString _selectPatchFileFilter =
        new("Patch file (*.Patch)");

    private readonly TranslationString _selectPatchFileCaption =
        new("Select patch file");

    private readonly TranslationString _noFileSelectedText =
        new("Please select a patch to apply");

    private readonly TranslationString _applyPatchMsgBox =
        new("Apply patch");

    #endregion
}
