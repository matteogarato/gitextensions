using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitUI.HelperDialogs;

namespace GitUI.CommandsDialogs;

public sealed partial class FormRenameBranch : GitModuleForm
{
    private readonly IGitBranchNameNormaliser _branchNameNormaliser;
    private readonly GitBranchNameOptions _gitBranchNameOptions = new(AppSettings.AutoNormaliseSymbol);
    private readonly string _oldName;

    public FormRenameBranch(IGitUiCommands commands, string defaultBranch)
        : base(commands)
    {
        _branchNameNormaliser = commands.GetRequiredService<IGitBranchNameNormaliser>();

        InitializeComponent();
        InitializeComplete();
        BranchNameTextBox.Text = defaultBranch;
        _oldName = defaultBranch;
    }

    private void BranchNameTextBox_Leave(object sender, EventArgs e)
    {
        if (!AppSettings.AutoNormaliseBranchName || !BranchNameTextBox.Text.Any(PathUtil.IsValidPathChar))
        {
            return;
        }

        var caretPosition = BranchNameTextBox.SelectionStart;
        var branchName = _branchNameNormaliser.Normalise(BranchNameTextBox.Text, _gitBranchNameOptions);
        BranchNameTextBox.Text = branchName;
        BranchNameTextBox.SelectionStart = caretPosition;
    }

    private void OkClick(object sender, EventArgs e)
    {
        // Ok button set as the "AcceptButton" for the form
        // if the user hits [Enter] at any point, we need to trigger BranchNameTextBox Leave event
        Ok.Focus();

        var newName = BranchNameTextBox.Text;

        if (newName == _oldName)
        {
            DialogResult = DialogResult.Cancel;
            return;
        }

        var command = Commands.RenameBranch(_oldName, newName);
        var success = FormProcess.ShowDialog(this, UiCommands, command, Module.WorkingDir, null, true);

        DialogResult = success ? DialogResult.OK : DialogResult.None;
    }
}
