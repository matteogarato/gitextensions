using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.HelperDialogs;
using GitUI.UserControls.RevisionGrid;
using ResourceManager;

namespace GitUI.CommandsDialogs.BrowseDialog;

public sealed partial class FormBisect : GitModuleForm
{
    // TODO: Improve me
    private readonly TranslationString _bisectStart =
        new("Mark selected revisions as start bisect range?");

    private readonly IRevisionGridInfo _revisionGridInfo;

    public FormBisect(RevisionGridControl revisionGrid)
        : base(revisionGrid.UiCommands)
    {
        _revisionGridInfo = revisionGrid;
        InitializeComponent();
        InitializeComplete();
        UpdateButtonsState();
    }

    private void UpdateButtonsState()
    {
        var inTheMiddleOfBisect = Module.InTheMiddleOfBisect();
        Start.Enabled = !inTheMiddleOfBisect;
        Good.Enabled = inTheMiddleOfBisect;
        Bad.Enabled = inTheMiddleOfBisect;
        Stop.Enabled = inTheMiddleOfBisect;
        btnSkip.Enabled = inTheMiddleOfBisect;
    }

    private void Start_Click(object sender, EventArgs e)
    {
        FormProcess.ShowDialog(this, UiCommands, Commands.StartBisect(), Module.WorkingDir, null, true);

        UpdateButtonsState();

        var revisions = _revisionGridInfo.GetSelectedRevisions();
        if (revisions.Count > 1)
        {
            if (MessageBoxes.Show(this, _bisectStart.Text, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                DialogResult.Yes)
            {
                BisectRange(revisions[0].ObjectId, revisions[^1].ObjectId);
                Close();
            }
        }

        return;

        void BisectRange(ObjectId startObjectId, ObjectId endObjectId)
        {
            var command = Commands.ContinueBisect(GitBisectOption.Good, startObjectId);
            var success = FormProcess.ShowDialog(this, UiCommands, command, Module.WorkingDir, null, true);
            if (!success)
            {
                return;
            }

            command = Commands.ContinueBisect(GitBisectOption.Bad, endObjectId);
            FormProcess.ShowDialog(this, UiCommands, command, Module.WorkingDir, null, true);
        }
    }

    private void Good_Click(object sender, EventArgs e)
    {
        ContinueBisect(GitBisectOption.Good);
    }

    private void Bad_Click(object sender, EventArgs e)
    {
        ContinueBisect(GitBisectOption.Bad);
    }

    private void Stop_Click(object sender, EventArgs e)
    {
        FormProcess.ShowDialog(this, UiCommands, Commands.StopBisect(), Module.WorkingDir, null, false);
        Close();
    }

    private void btnSkip_Click(object sender, EventArgs e)
    {
        ContinueBisect(GitBisectOption.Skip);
    }

    private void ContinueBisect(GitBisectOption bisectOption)
    {
        FormProcess.ShowDialog(this, UiCommands, Commands.ContinueBisect(bisectOption), Module.WorkingDir, null, false);
        Close();
    }
}
