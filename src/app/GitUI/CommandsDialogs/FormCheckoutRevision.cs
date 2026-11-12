using System.Diagnostics;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.HelperDialogs;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public partial class FormCheckoutRevision : GitExtensionsDialog
{
    private readonly TranslationString _noRevisionSelectedMsgBox = new("Select 1 revision to checkout.");
    private readonly TranslationString _noRevisionSelectedMsgBoxCaption = new("Checkout");

    public FormCheckoutRevision(IGitUiCommands commands)
        : base(commands, false)
    {
        InitializeComponent();
        InitializeComplete();
    }

    public void SetRevision(string? commitHash)
    {
        commitPickerSmallControl1.SetSelectedCommitHash(commitHash);
    }

    private void OkClick(object sender, EventArgs e)
    {
        try
        {
            var selectedObjectId = commitPickerSmallControl1.SelectedObjectId;

            if (selectedObjectId.IsZero)
            {
                MessageBoxes.Show(this, _noRevisionSelectedMsgBox.Text, _noRevisionSelectedMsgBoxCaption.Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var checkedOutObjectId = Module.GetCurrentCheckout();

            DebugHelpers.Assert(!checkedOutObjectId.IsZero, "checkedOutObjectId is not zero");

            var success = ScriptsRunner.RunEventScripts(ScriptEvent.BeforeCheckout, this);
            if (!success)
            {
                return;
            }

            string command =
                Commands.Checkout(selectedObjectId.ToString(), Force.Checked ? LocalChangesAction.Reset : 0);
            success = FormProcess.ShowDialog(this, UiCommands, command, Module.WorkingDir, null, true);
            if (success)
            {
                if (selectedObjectId != checkedOutObjectId)
                {
                    UiCommands.UpdateSubmodules(this);
                }
            }

            ScriptsRunner.RunEventScripts(ScriptEvent.AfterCheckout, this);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            Trace.WriteLine(ex.Message);
        }
    }
}
