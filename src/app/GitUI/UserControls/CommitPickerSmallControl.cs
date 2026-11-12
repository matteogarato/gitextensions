using System.ComponentModel;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitUI.HelperDialogs;

namespace GitUI.UserControls;

public partial class CommitPickerSmallControl : GitModuleControl
{
    public CommitPickerSmallControl()
    {
        InitializeComponent();
        InitializeComplete();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ObjectId SelectedObjectId { get; private set; }

    /// <summary>
    ///     Occurs whenever the selected commit hash changes.
    /// </summary>
    public event EventHandler? SelectedObjectIdChanged;

    /// <summary>
    ///     shows a message box if commitHash is invalid.
    /// </summary>
    public void SetSelectedCommitHash(string? commitHash)
    {
        var oldCommitHash = SelectedObjectId;

        SelectedObjectId = Module.RevParse(commitHash!);

        if (SelectedObjectId.IsZero && !string.IsNullOrWhiteSpace(commitHash))
        {
            SelectedObjectId = oldCommitHash;
            MessageBoxes.Show("The given commit hash is not valid for this repository and was therefore discarded.",
                TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        else
        {
            SelectedObjectIdChanged?.Invoke(this, EventArgs.Empty);
        }

        var isArtificialCommitForEmptyRepo = commitHash == "HEAD";

        lbCommits.Text = "";

        if (SelectedObjectId.IsZero || isArtificialCommitForEmptyRepo)
        {
            textBoxCommitHash.Text = "";
        }
        else
        {
            var selectedObjectId = SelectedObjectId;
            textBoxCommitHash.Text = selectedObjectId.ToShortString();
            ThreadHelper.FileAndForget(async () =>
            {
                var currentCheckout = Module.GetCurrentCheckout();

                if (currentCheckout.IsZero)
                {
                    return;
                }

                var toRef = selectedObjectId.IsArtificial ? "HEAD" : selectedObjectId.ToString();
                var text = Module.GetCommitCountString(currentCheckout, toRef);

                await this.SwitchToMainThreadAsync();

                lbCommits.Text = text;
            });
        }
    }

    private void buttonPickCommit_Click(object sender, EventArgs e)
    {
        using FormChooseCommit chooseForm =
            new(UiCommands, SelectedObjectId.IsZero ? null : SelectedObjectId.ToString());
        if (chooseForm.ShowDialog(this) == DialogResult.OK && chooseForm.SelectedRevision is not null)
        {
            SetSelectedCommitHash(chooseForm.SelectedRevision.Guid);
        }
    }

    private void textBoxCommitHash_TextLeave(object sender, EventArgs e)
    {
        SetSelectedCommitHash(textBoxCommitHash.Text.Trim());
    }
}
