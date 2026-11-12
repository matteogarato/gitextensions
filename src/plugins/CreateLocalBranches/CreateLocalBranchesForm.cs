using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using ResourceManager;

namespace GitExtensions.Plugins.CreateLocalBranches;

public partial class CreateLocalBranchesForm : GitExtensionsFormBase
{
    private readonly GitUiEventArgs _gitUiCommands;

    public CreateLocalBranchesForm(GitUiEventArgs gitUiCommands)
    {
        InitializeComponent();
        InitializeComplete();

        _gitUiCommands = gitUiCommands;
    }

    private void button1_Click(object sender, EventArgs e)
    {
        GitArgumentBuilder args = new("branch") { "-a" };
        var references = _gitUiCommands.GitModule.GitExecutable.GetOutput(args)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (references.Length == 0)
        {
            MessageBoxes.ShowError(this, "No remote branches found.");
            DialogResult = DialogResult.Cancel;
            return;
        }

        foreach (var reference in references)
        {
            try
            {
                var branchName = reference.Trim(Delimiters.GitOutput);

                if (branchName.StartsWith("remotes/" + _NO_TRANSLATE_Remote.Text + "/"))
                {
                    args = new GitArgumentBuilder("branch")
                    {
                        "--track", branchName.Replace($"remotes/{_NO_TRANSLATE_Remote.Text}/", ""), branchName
                    };
                    _gitUiCommands.GitModule.GitExecutable.GetOutput(args);
                }
            }
            catch
            {
            }
        }

        MessageBoxes.Show(this,
            string.Format("{0} local tracking branches have been created/updated.", references.Length),
            "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
