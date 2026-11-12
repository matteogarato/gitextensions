using GitCommands.Git;
using GitCommands.Git.Extensions;
using GitCommands.Git.Tag;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.HelperDialogs;
using GitUI.ScriptsEngine;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public sealed partial class FormCreateTag : GitModuleForm
{
    private static readonly TranslationString _trsLightweight = new("Lightweight tag");
    private static readonly TranslationString _trsAnnotated = new("Annotated tag");
    private static readonly TranslationString _trsSignDefault = new("Sign with default GPG");
    private static readonly TranslationString _trsSignSpecificKey = new("Sign with specific GPG");

    private readonly IGitTagController _gitTagController;
    private readonly TranslationString _messageCaption = new("Tag");
    private readonly TranslationString _noRevisionSelected = new("Select 1 revision to create the tag on.");
    private readonly TranslationString _pushToCaption = new("Push tag to '{0}'");
    private string _currentRemote = "";

    public FormCreateTag(IGitUiCommands commands, ObjectId objectId)
        : base(commands)
    {
        InitializeComponent();
        InitializeComplete();

        annotate.Items.AddRange(_trsLightweight.Text, _trsAnnotated.Text, _trsSignDefault.Text,
            _trsSignSpecificKey.Text);
        annotate.SelectedIndex = 0;

        tagMessage.MistakeFont = new Font(tagMessage.MistakeFont, FontStyle.Underline);

        if (objectId.IsArtificial)
        {
            objectId = default;
        }

        if (objectId.IsZero)
        {
            objectId = Module.GetCurrentCheckout();
        }

        if (!objectId.IsZero)
        {
            commitPickerSmallControl1.SetSelectedCommitHash(objectId.ToString());
        }

        _gitTagController = new GitTagController(commands);
    }

    private void FormCreateTag_Load(object sender, EventArgs e)
    {
        textBoxTagName.Select();
        _currentRemote = Module.GetCurrentRemote();
        if (string.IsNullOrEmpty(_currentRemote))
        {
            _currentRemote = "origin";
        }

        pushTag.Text = string.Format(_pushToCaption.Text, _currentRemote);
    }

    private void OkClick(object sender, EventArgs e)
    {
        try
        {
            var tagName = CreateTag();

            if (pushTag.Checked && !string.IsNullOrEmpty(tagName))
            {
                PushTag(tagName);
            }
        }
        catch (Exception ex)
        {
            MessageBoxes.Show(this, ex.Message, TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string CreateTag()
    {
        var objectId = commitPickerSmallControl1.SelectedObjectId;

        if (objectId.IsZero)
        {
            MessageBoxes.Show(this, _noRevisionSelected.Text, _messageCaption.Text, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return "";
        }

        GitCreateTagArgs createTagArgs = new(textBoxTagName.Text,
            objectId,
            GetSelectedOperation(annotate.SelectedIndex),
            tagMessage.Text,
            textBoxGpgKey.Text,
            ForceTag.Checked);
        var success = _gitTagController.CreateTag(createTagArgs, this);
        if (!success)
        {
            return "";
        }

        DialogResult = DialogResult.OK;
        return textBoxTagName.Text;
    }

    private void PushTag(string tagName)
    {
        var pushCmd = Commands.PushTag(_currentRemote, tagName, false);

        var success = ScriptsRunner.RunEventScripts(ScriptEvent.BeforePush, this);
        if (!success)
        {
            return;
        }

        using FormRemoteProcess form = new(UiCommands, pushCmd)
        {
            Remote = _currentRemote, Text = string.Format(_pushToCaption.Text, _currentRemote)
        };
        form.ShowDialog();

        if (!Module.InTheMiddleOfAction() && !form.ErrorOccurred())
        {
            ScriptsRunner.RunEventScripts(ScriptEvent.AfterPush, this);
        }
    }

    private void AnnotateDropDownChanged(object sender, EventArgs e)
    {
        var tagOperation = GetSelectedOperation(annotate.SelectedIndex);
        textBoxGpgKey.Enabled = tagOperation == TagOperation.SignWithSpecificKey;
        keyIdLbl.Enabled = tagOperation == TagOperation.SignWithSpecificKey;
        var providesMessage = tagOperation.CanProvideMessage();
        tagMessage.Enabled = providesMessage;
        tagMessage.BorderStyle = providesMessage ? BorderStyle.FixedSingle : BorderStyle.None;
    }

    private static TagOperation GetSelectedOperation(int dropdownSelection)
    {
        return dropdownSelection switch
        {
            0 => TagOperation.Lightweight,
            1 => TagOperation.Annotate,
            2 => TagOperation.SignWithDefaultKey,
            3 => TagOperation.SignWithSpecificKey,
            _ => throw new NotSupportedException("Invalid dropdownSelection")
        };
    }
}
