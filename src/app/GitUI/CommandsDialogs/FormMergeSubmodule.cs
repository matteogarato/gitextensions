using GitCommands.Git;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.HelperDialogs;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public sealed partial class FormMergeSubmodule : GitModuleForm
{
    private readonly TranslationString _deleted = new("deleted");

    private readonly string _filename;
    private readonly TranslationString _stageFilename = new("Stage {0}");

    public FormMergeSubmodule(IGitUiCommands commands, string filename)
        : base(commands)
    {
        InitializeComponent();
        InitializeComplete();
        lbSubmodule.Text = filename;
        _filename = filename;
    }

    private void FormMergeSubmodule_Load(object sender, EventArgs e)
    {
        var item = ThreadHelper.JoinableTaskFactory.Run(() => Module.GetConflictAsync(_filename));

        tbBase.Text = item.Base.ObjectId.IsZero ? _deleted.Text : item.Base.ObjectId.ToString();
        tbLocal.Text = item.Local.ObjectId.IsZero ? _deleted.Text : item.Local.ObjectId.ToString();
        tbRemote.Text = item.Remote.ObjectId.IsZero ? _deleted.Text : item.Remote.ObjectId.ToString();
        tbCurrent.Text = Module.GetSubmodule(_filename).GetCurrentCheckout() is { IsZero: false } id
            ? id.ToString()
            : "";
        btCheckoutBranch.Enabled = !item.Base.ObjectId.IsZero && !item.Remote.ObjectId.IsZero;
    }

    private void btRefresh_Click(object sender, EventArgs e)
    {
        tbCurrent.Text = Module.GetSubmodule(_filename).GetCurrentCheckout() is { IsZero: false } id
            ? id.ToString()
            : "";
    }

    private void StageSubmodule()
    {
        GitArgumentBuilder args = new("add") { "--", _filename.QuoteNe() };
        var output = Module.GitExecutable.GetOutput(args);

        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        var text = string.Format(_stageFilename.Text, _filename);
        FormStatus.ShowErrorDialog(this, UiCommands, text, text, output);
    }

    private void btStageCurrent_Click(object sender, EventArgs e)
    {
        StageSubmodule();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void btOpenSubmodule_Click(object sender, EventArgs e)
    {
        GitUiCommands.LaunchBrowse(Module.GetSubmoduleFullPath(_filename));
    }

    private void btCheckoutBranch_Click(object sender, EventArgs e)
    {
        ObjectId[] ids = [ObjectId.Parse(tbLocal.Text), ObjectId.Parse(tbRemote.Text)];
        var submoduleCommands = UiCommands.WithWorkingDirectory(Module.GetSubmoduleFullPath(_filename));
        if (!submoduleCommands.StartCheckoutBranch(this, ids))
        {
            return;
        }

        StageSubmodule();
        DialogResult = DialogResult.OK;
        Close();
    }
}
