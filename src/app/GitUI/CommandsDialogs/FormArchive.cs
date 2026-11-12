using GitExtensions.Extensibility.Git;
using GitUI.HelperDialogs;
using ResourceManager;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommandsDialogs;

public partial class FormArchive : GitModuleForm
{
    private readonly TranslationString _noRevisionSelected =
        new("You need to choose a target revision.");

    private readonly TranslationString _saveFileDialogCaption =
        new("Save archive as");

    private readonly TranslationString _saveFileDialogFilterTar =
        new("Tar file (*.tar)");

    private readonly TranslationString _saveFileDialogFilterZip =
        new("Zip file (*.zip)");

    private GitRevision? _diffSelectedRevision;

    private GitRevision? _selectedRevision;

    public FormArchive(IGitUiCommands commands)
        : base(commands)
    {
        InitializeComponent();
        InitializeComplete();

        labelAuthor.Font = new Font(labelAuthor.Font, FontStyle.Bold);
        labelMessage.Font = new Font(labelMessage.Font, FontStyle.Bold);
    }

    public GitRevision? SelectedRevision
    {
        get => _selectedRevision;
        set
        {
            _selectedRevision = value;
            commitSummaryUserControl1.Revision = _selectedRevision;
        }
    }

    private GitRevision? DiffSelectedRevision
    {
        get => _diffSelectedRevision;
        set
        {
            _diffSelectedRevision = value;
            ////commitSummaryUserControl2.Revision = _diffSelectedRevision;
            if (_diffSelectedRevision is null)
            {
                const string defaultString = "...";
                labelDateCaption.Text = $"{ResourceManager.TranslatedStrings.CommitDate}:";
                labelAuthor.Text = defaultString;
                gbDiffRevision.Text = defaultString;
                labelMessage.Text = defaultString;
            }
            else
            {
                labelDateCaption.Text =
                    $"{ResourceManager.TranslatedStrings.CommitDate}: {_diffSelectedRevision.CommitDate}";
                labelAuthor.Text = _diffSelectedRevision.Author;
                gbDiffRevision.Text = _diffSelectedRevision.ObjectId.ToShortString();
                labelMessage.Text = _diffSelectedRevision.Subject;
            }
        }
    }

    public void SetDiffSelectedRevision(GitRevision? revision)
    {
        checkboxRevisionFilter.Checked = revision is not null;
        DiffSelectedRevision = revision;
    }

    public void SetPathArgument(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            checkBoxPathFilter.Checked = false;
            textBoxPaths.Text = "";
        }
        else
        {
            checkBoxPathFilter.Checked = true;
            textBoxPaths.Text = path;
        }
    }

    private void FormArchive_Load(object sender, EventArgs e)
    {
        buttonArchiveRevision.Focus();
        checkBoxPathFilter_CheckedChanged(this, EventArgs.Empty);
        checkboxRevisionFilter_CheckedChanged(this, EventArgs.Empty);
    }

    private void Save_Click(object sender, EventArgs e)
    {
        if (checkboxRevisionFilter.Checked && DiffSelectedRevision is null)
        {
            MessageBoxes.Show(this, _noRevisionSelected.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                MessageBoxIcon.Exclamation);
            return;
        }

        var revision = SelectedRevision?.Guid;

        var fileFilterCaption = GetSelectedOutputFormat() == OutputFormat.Zip
            ? _saveFileDialogFilterZip.Text
            : _saveFileDialogFilterTar.Text;
        var fileFilterEnding = GetSelectedOutputFormat() == OutputFormat.Zip ? "zip" : "tar";

        // TODO (feature): if there is a tag on the revision use the tag name as suggestion
        // TODO (feature): let user decide via GUI
        var filenameSuggestion = string.Format("{0}_{1}", new DirectoryInfo(Module.WorkingDir).Name, revision);
        if (checkBoxPathFilter.Checked && textBoxPaths.Lines.Length == 1 &&
            !string.IsNullOrWhiteSpace(textBoxPaths.Lines[0]))
        {
            filenameSuggestion += "_" + textBoxPaths.Lines[0].Trim().Replace(".", "_");
        }

        using SaveFileDialog saveFileDialog = new()
        {
            Filter = string.Format("{0}|*.{1}", fileFilterCaption, fileFilterEnding),
            Title = _saveFileDialogCaption.Text,
            FileName = filenameSuggestion
        };
        if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            var format = GetSelectedOutputFormat() == OutputFormat.Zip ? "zip" : "tar";

            var arguments = string.Format(@"archive --format=""{0}"" {1} --output ""{2}"" {3}", format, revision,
                saveFileDialog.FileName, GetPathArgumentFromGui());
            FormProcess.ShowDialog(this, UiCommands, arguments, Module.WorkingDir, null, true);
            Close();
        }
    }

    private string GetPathArgumentFromGui()
    {
        if (checkBoxPathFilter.Checked)
        {
            // 1. get all lines (paths) from text box
            // 2. wrap lines that are not empty with ""
            // 3. join together with space as separator
            return string.Join(" ", textBoxPaths.Lines.Select(a => a.QuoteNe()));
        }

        if (checkboxRevisionFilter.Checked)
        {
            // 1. get all changed (and not deleted files) from selected to current revision
            var files = UiCommands.Module
                .GetDiffFilesWithUntracked(DiffSelectedRevision?.Guid, SelectedRevision?.Guid, StagedStatus.None,
                    noCache: false, cancellationToken: default)
                .Where(f => !f.IsDeleted);

            // 2. wrap file names with ""
            // 3. join together with space as separator
            return string.Join(" ", files.Select(f => f.Name.QuoteNe()));
        }

        return "";
    }

    private OutputFormat GetSelectedOutputFormat()
    {
        return _NO_TRANSLATE_radioButtonFormatZip.Checked ? OutputFormat.Zip : OutputFormat.Tar;
    }

    private void btnChooseRevision_Click(object sender, EventArgs e)
    {
        using FormChooseCommit chooseForm = new(UiCommands, SelectedRevision?.Guid);
        if (chooseForm.ShowDialog(this) == DialogResult.OK && chooseForm.SelectedRevision is not null)
        {
            SelectedRevision = chooseForm.SelectedRevision;
        }
    }

    private void checkBoxPathFilter_CheckedChanged(object sender, EventArgs e)
    {
        textBoxPaths.Enabled = checkBoxPathFilter.Checked;
        if (checkBoxPathFilter.Checked)
        {
            checkboxRevisionFilter.Checked = false;
        }
    }

    private void btnDiffChooseRevision_Click(object sender, EventArgs e)
    {
        using FormChooseCommit chooseForm = new(UiCommands,
            DiffSelectedRevision is not null ? DiffSelectedRevision.Guid : string.Empty);
        if (chooseForm.ShowDialog(this) == DialogResult.OK && chooseForm.SelectedRevision is not null)
        {
            DiffSelectedRevision = chooseForm.SelectedRevision;
        }
    }

    private void checkboxRevisionFilter_CheckedChanged(object sender, EventArgs e)
    {
        btnDiffChooseRevision.Enabled = checkboxRevisionFilter.Checked;
        ////commitSummaryUserControl2.Enabled = checkboxRevisionFilter.Checked;
        ////lblChooseDiffRevision.Enabled = checkboxRevisionFilter.Checked;
        gbDiffRevision.Enabled = checkboxRevisionFilter.Checked;
        btnDiffChooseRevision.Enabled = checkboxRevisionFilter.Checked;
        if (checkboxRevisionFilter.Checked)
        {
            checkBoxPathFilter.Checked = false;
        }
    }

    private enum OutputFormat
    {
        Zip,
        Tar
    }
}
