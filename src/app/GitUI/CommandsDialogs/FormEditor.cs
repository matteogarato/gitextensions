using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI.Theming;
using GitUI.Editor;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public sealed partial class FormEditor : GitModuleForm
{
    private readonly TranslationString _cannotOpenFile = new("Cannot open file:");
    private readonly TranslationString _cannotSaveFile = new("Cannot save file:");

    private readonly string? _fileName;
    private readonly TranslationString _saveChanges = new("Do you want to save changes?");
    private readonly TranslationString _saveChangesCaption = new("Save changes");

    private bool _hasChanges;

    public FormEditor(IGitUiCommands commands, string? fileName, bool showWarning, bool readOnly = false,
        int? lineNumber = null)
        : base(commands)
    {
        _fileName = fileName;
        InitializeComponent();
        panelMessage.BackColor = OtherColors.PanelMessageWarningColor;
        panelMessage.SetForeColorForBackColor();
        InitializeComplete();

        // for translation form
        if (_fileName is not null)
        {
            OpenFile(_fileName, lineNumber);
        }

        fileViewer.TextChanged += (s, e) => HasChanges = true;
        fileViewer.TextLoaded += (s, e) => HasChanges = false;
        panelMessage.Visible = showWarning;

        fileViewer.IsReadOnly = readOnly;
    }

    private bool HasChanges
    {
        get => _hasChanges;
        set
        {
            _hasChanges = value;
            toolStripSaveButton.Enabled = value;
        }
    }

    private void OpenFile(string fileName, int? line = null)
    {
        try
        {
            fileViewer.ViewFileAsync(fileName, line: line);
            fileViewer.IsReadOnly = false;
            Text = fileName;

            // loading a new file from disk, the text hasn't been changed yet.
            HasChanges = false;
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(this, _cannotOpenFile.Text + Environment.NewLine + ex.Message, TranslatedStrings.Error,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void FormEditor_FormClosing(object sender, FormClosingEventArgs e)
    {
        // only offer to save if there's something to save.
        if (HasChanges)
        {
            var saveChangesAnswer = MessageBoxes.Show(this, _saveChanges.Text, _saveChangesCaption.Text,
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            switch (saveChangesAnswer)
            {
                case DialogResult.Yes:
                    try
                    {
                        SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        if (MessageBoxes.Show(this, $"{_cannotSaveFile.Text}{Environment.NewLine}{ex.Message}",
                                TranslatedStrings.Error, MessageBoxButtons.OKCancel, MessageBoxIcon.Error) ==
                            DialogResult.Cancel)
                        {
                            e.Cancel = true;
                            return;
                        }
                    }

                    DialogResult = DialogResult.OK;
                    break;
                case DialogResult.Cancel:
                    e.Cancel = true;
                    return;
                default:
                    DialogResult = DialogResult.Cancel;
                    break;
            }
        }
        else
        {
            DialogResult = DialogResult.OK;
        }
    }

    private void toolStripSaveButton_Click(object sender, EventArgs e)
    {
        SaveChangesShowException();
    }

    private void SaveChangesShowException()
    {
        try
        {
            SaveChanges();
        }
        catch (Exception ex)
        {
            MessageBoxes.ShowError(this, $"{_cannotSaveFile.Text}{Environment.NewLine}{ex.Message}");
        }
    }

    private void SaveChanges()
    {
        if (!string.IsNullOrEmpty(_fileName))
        {
            if (fileViewer.FilePreamble is null ||
                Module.FilesEncoding.GetPreamble().SequenceEqual(fileViewer.FilePreamble))
            {
                FileUtility.SafeWriteAllText(_fileName, fileViewer.GetText(), Module.FilesEncoding, []);
            }
            else
            {
                FileUtility.SafeWriteAllText(_fileName, fileViewer.GetText(), Module.FilesEncoding,
                    fileViewer.FilePreamble);
            }

            // we've written the changes out to disk now, nothing to save.
            HasChanges = false;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Escape:
                Close();
                return true;
            case Keys.Control | Keys.S:
                SaveChangesShowException();
                return true;
            default:
                return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    internal TestAccessor GetTestAccessor()
    {
        return new TestAccessor(this);
    }

    internal readonly struct TestAccessor(FormEditor formEditor)
    {
        public FileViewer FileViewer
        {
            get => formEditor.fileViewer;
        }

        public bool HasChanges
        {
            get => formEditor.HasChanges;
        }

        public void SaveChanges()
        {
            formEditor.SaveChanges();
        }
    }
}
