using GitExtensions.Extensibility.Git;

namespace GitUI.HelperDialogs;

public sealed partial class FormCommitDiff : GitModuleForm
{
    public FormCommitDiff(IGitUiCommands commands, ObjectId objectId)
        : base(commands)
    {
        InitializeComponent();
        InitializeComplete();

        CommitDiff.TextChanged += (s, e) => Text = CommitDiff.Text;

        CommitDiff.SetRevision(objectId, null);
    }
}
