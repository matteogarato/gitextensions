using System.ComponentModel;
using GitCommands.Git;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.HelperDialogs;
using GitUI.Properties;
using ResourceManager;

namespace GitUI.UserControls;

public partial class InteractiveGitActionControl : GitModuleControl
{
    public enum GitAction
    {
        Unknown,
        None,
        Bisect,
        Rebase,
        Merge,
        Patch
    }

    private readonly TranslationString _bisect = new("Bisect");
    private readonly TranslationString _conflictsMessage = new("There are unresolved merge conflicts.");
    private readonly TranslationString _merge = new("Merge");
    private readonly TranslationString _patch = new("Patch");
    private readonly TranslationString _progressMessage = new("{0} is currently in progress.");

    private readonly TranslationString _progressWithConflictsMessage =
        new("{0} is currently in progress with merge conflicts.");

    private readonly TranslationString _rebase = new("Rebase");

    private GitAction _action;
    private bool _hasConflicts;

    public InteractiveGitActionControl()
    {
        InitializeComponent();
        BackColor.AdaptBackColor();
        InitializeComplete();
    }

    private GitModuleForm? Form
    {
        get => FindForm() as GitModuleForm;
    }

    // It is possible for a repo to be in a middle of a bisect operation and
    // be in a conflicted state. Hence detect bisect separately from the rest
    // of git actions
    public void RefreshBisect()
    {
        if (!Module.IsValidGitWorkingDir())
        {
            return;
        }

        if (Module.InTheMiddleOfBisect())
        {
            SetGitAction(GitAction.Bisect, false);
            return;
        }

        SetGitAction(GitAction.None, false);
    }

    /// <summary>
    ///     Refresh the banner in the revision grid after reactivation.
    /// </summary>
    /// <param name="checkForConflicts">Allow running Git command to check for conflicts.</param>
    public void RefreshGitAction(bool checkForConflicts)
    {
        // get the current state of the repo

        if (!Module.IsValidGitWorkingDir())
        {
            return;
        }

        bool hasConflicts;
        try
        {
            // This command can be executed seemingly in the background (selecting Browse),
            // do not notify the user (this can occur if Git is upgraded).
            // Running Git commands async when restoring may fail.
            hasConflicts = checkForConflicts && Module.InTheMiddleOfConflictedMerge(false);
        }
        catch (Win32Exception)
        {
            hasConflicts = false;
        }

        if (Module.InTheMiddleOfRebase())
        {
            SetGitAction(GitAction.Rebase, hasConflicts);
            return;
        }

        if (Module.InTheMiddleOfMerge())
        {
            SetGitAction(GitAction.Merge, hasConflicts);
            return;
        }

        if (Module.InTheMiddleOfPatch())
        {
            SetGitAction(GitAction.Patch, hasConflicts);
            return;
        }

        SetGitAction(GitAction.None, hasConflicts);
    }

    private void SetGitAction(GitAction action, bool hasConflicts)
    {
        if (action == _action && hasConflicts == _hasConflicts)
        {
            // nothing to do
            return;
        }

        _action = action;
        _hasConflicts = hasConflicts;

        // remove old controls
        ButtonContainer.Controls.Clear();

        if (_action == GitAction.None && !_hasConflicts)
        {
            Visible = false;
            return;
        }

        IconBox.Image = _hasConflicts ? Images.SolveMerge : Resources.information;
        BackColor = (_hasConflicts ? Color.Orange : Color.LightSkyBlue).AdaptBackColor();
        TextLabel.SetForeColorForBackColor();

        var actionStr = "";

        switch (_action)
        {
            case GitAction.Bisect:
                actionStr = _bisect.Text;
                ButtonContainer.Controls.Add(MoreButton);
                break;
            case GitAction.Rebase:
                actionStr = _rebase.Text;
                ButtonContainer.Controls.Add(_hasConflicts ? ResolveButton : ContinueButton);
                ButtonContainer.Controls.Add(AbortButton);
                ButtonContainer.Controls.Add(MoreButton);
                break;
            case GitAction.Merge:
                actionStr = _merge.Text;
                ButtonContainer.Controls.Add(_hasConflicts ? ResolveButton : ContinueButton);
                ButtonContainer.Controls.Add(AbortButton);
                break;
            case GitAction.Patch:
                actionStr = _patch.Text;
                ButtonContainer.Controls.Add(_hasConflicts ? ResolveButton : ContinueButton);
                ButtonContainer.Controls.Add(AbortButton);
                ButtonContainer.Controls.Add(MoreButton);
                break;
            case GitAction.None:
                // can only get here if hasConflicts so add resolve button
                ButtonContainer.Controls.Add(ResolveButton);
                break;
        }

        TextLabel.Text = _action == GitAction.None
            ? _conflictsMessage.Text
            : string.Format(
                _hasConflicts ? _progressWithConflictsMessage.Text : _progressMessage.Text,
                actionStr);

        this.AdjustForDpiScaling();

        Visible = true;
    }

    private void ResolveButton_Click(object sender, EventArgs e)
    {
        Form?.UiCommands.StartResolveConflictsDialog(this);
    }

    private void ContinueButton_Click(object sender, EventArgs e)
    {
        if (Form is null)
        {
            return;
        }

        switch (_action)
        {
            case GitAction.Rebase:
                FormProcess.ShowDialog(Form, UiCommands, Commands.ContinueRebase(), Module.WorkingDir, null, true);
                break;
            case GitAction.Merge:
                FormProcess.ShowDialog(Form, UiCommands, Commands.ContinueMerge(), Module.WorkingDir, null, true);
                break;
            case GitAction.Patch:
                FormProcess.ShowDialog(Form, UiCommands, Commands.Resolved(), Module.WorkingDir, null, true);
                break;
            default:
                return;
        }

        Form.UiCommands.RepoChangedNotifier.Notify();
    }

    private void AbortButton_Click(object sender, EventArgs e)
    {
        if (Form is null)
        {
            return;
        }

        switch (_action)
        {
            case GitAction.Rebase:
                FormProcess.ShowDialog(Form, UiCommands, Commands.AbortRebase(), Module.WorkingDir, null, true);
                break;
            case GitAction.Merge:
                FormProcess.ShowDialog(Form, UiCommands, Commands.AbortMerge(), Module.WorkingDir, null, true);
                break;
            case GitAction.Patch:
                FormProcess.ShowDialog(Form, UiCommands, Commands.Abort(), Module.WorkingDir, null, true);
                break;
            default:
                return;
        }

        Form.UiCommands.RepoChangedNotifier.Notify();
    }

    private void MoreButton_Click(object sender, EventArgs e)
    {
        if (Form is null)
        {
            return;
        }

        switch (_action)
        {
            case GitAction.Bisect:
                if (Form is not FormBrowse formBrowse)
                {
                    return;
                }

                using (FormBisect frm = new(formBrowse.RevisionGridControl))
                {
                    frm.ShowDialog(this);
                }

                Form.UiCommands.RepoChangedNotifier.Notify();
                break;
            case GitAction.Rebase:
                Form.UiCommands.StartTheContinueRebaseDialog(Form);
                break;
            case GitAction.Patch:
                Form.UiCommands.StartApplyPatchDialog(Form);
                break;
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor
    {
        private readonly InteractiveGitActionControl _interactiveGitActionControl;

        internal TestAccessor(InteractiveGitActionControl interactiveGitActionControl)
        {
            _interactiveGitActionControl = interactiveGitActionControl;
        }

        internal GitAction Action
        {
            get => _interactiveGitActionControl._action;
        }

        internal bool HasConflicts
        {
            get => _interactiveGitActionControl._hasConflicts;
        }

        internal bool Visible
        {
            get => _interactiveGitActionControl.Visible;
        }

        internal ControlCollection Controls
        {
            get => _interactiveGitActionControl.ButtonContainer.Controls;
        }

        internal Button ResolveButton
        {
            get => _interactiveGitActionControl.ResolveButton;
        }

        internal Button ContinueButton
        {
            get => _interactiveGitActionControl.ContinueButton;
        }

        internal Button AbortButton
        {
            get => _interactiveGitActionControl.AbortButton;
        }

        internal Button MoreButton
        {
            get => _interactiveGitActionControl.MoreButton;
        }

        internal void SetGitAction(GitAction action, bool conflicts)
        {
            _interactiveGitActionControl.SetGitAction(action, conflicts);
        }
    }
}
