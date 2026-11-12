using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public partial class FormFormatPatch : GitModuleForm
{
    private readonly TranslationString _currentBranchText = new("Current branch:");

    private readonly TranslationString _failCreatePatch =
        new("Unable to create patch file(s)");

    private readonly TranslationString _noOutputPathEnteredText =
        new("You need to enter an output path.");

    private readonly TranslationString _patchResultCaption =
        new("Patch result");

    private readonly TranslationString _revisionsNeededCaption =
        new("Patch error");

    private readonly TranslationString _revisionsNeededText =
        new("You need to select at least one revision");

    public FormFormatPatch(IGitUiCommands commands)
        : base(commands)
    {
        InitializeComponent();
        RevisionGrid.ShowUncommittedChangesIfPossible = false;
        InitializeComplete();
    }

    private void Browse_Click(object sender, EventArgs e)
    {
        var userSelectedPath = OsShellUtil.PickFolder(this);

        if (userSelectedPath is not null)
        {
            OutputPath.Text = userSelectedPath;
        }
    }

    private void FormFormatPath_Load(object sender, EventArgs e)
    {
        OutputPath.Text = AppSettings.LastFormatPatchDir;
        var selectedHead = Module.GetSelectedBranch();
        SelectedBranch.Text = _currentBranchText.Text + " " + selectedHead;

        OutputPath.TextChanged += OutputPath_TextChanged;
        RevisionGrid.Load();
    }

    private void OutputPath_TextChanged(object? sender, EventArgs e)
    {
        if (Directory.Exists(OutputPath.Text))
        {
            AppSettings.LastFormatPatchDir = OutputPath.Text;
        }
    }

    private void FormatPatch_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(OutputPath.Text))
        {
            MessageBoxes.Show(this, _noOutputPathEnteredText.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        string rev1;
        string rev2;
        var result = "";

        var revisions = RevisionGrid.GetSelectedRevisions(SortDirection.Descending);
        if (revisions.Count == 0)
        {
            MessageBoxes.Show(this, _revisionsNeededText.Text, _revisionsNeededCaption.Text, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        if (revisions.Count == 1)
        {
            var parents = revisions[0].ParentIds;
            rev1 = parents?.Count > 0 ? parents[0].ToString() : "";
            rev2 = revisions[0].Guid;
            result = Module.FormatPatch(rev1, rev2, OutputPath.Text);
        }
        else if (revisions.Count == 2)
        {
            var parents = revisions[0].ParentIds;
            rev1 = parents?.Count > 0 ? parents[0].ToString() : "";
            rev2 = revisions[1].Guid;
            result = Module.FormatPatch(rev1, rev2, OutputPath.Text);
        }
        else
        {
            var n = 0;
            foreach (var revision in revisions)
            {
                n++;
                var parents = revision.ParentIds;
                rev1 = parents?.Count > 0 ? parents[0].ToString() : "";
                rev2 = revision.Guid;
                result += Module.FormatPatch(rev1, rev2, OutputPath.Text, n);
            }
        }

        if (string.IsNullOrEmpty(result))
        {
            MessageBoxes.Show(this, _failCreatePatch.Text, _revisionsNeededCaption.Text, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        else
        {
            MessageBoxes.Show(this, result, _patchResultCaption.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }
}
