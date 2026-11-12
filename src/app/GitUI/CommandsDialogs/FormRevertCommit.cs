using GitCommands;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitUI.HelperDialogs;
using Microsoft.VisualStudio.Threading;
using ResourceManager;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.CommandsDialogs;

public partial class FormRevertCommit : GitExtensionsDialog
{
    private const int _parentsListItemHeight = 18;
    private readonly TranslationString _noneParentSelectedText = new("None parent is selected!");

    private bool _isMerge;
    private int _lvParentsListControlHeight;
    private int _parentsLabelControlHeight;

    public FormRevertCommit(IGitUiCommands commands, GitRevision revision)
        : base(commands, false)
    {
        Revision = revision;

        InitializeComponent();

        columnHeader1.Width = DpiUtil.Scale(columnHeader1.Width);
        columnHeader2.Width = DpiUtil.Scale(columnHeader2.Width);
        columnHeader3.Width = DpiUtil.Scale(columnHeader3.Width);
        columnHeader4.Width = DpiUtil.Scale(columnHeader4.Width);

        InitializeComplete();
    }

    public GitRevision Revision { get; }

    private void Form_Load(object sender, EventArgs e)
    {
        _parentsLabelControlHeight = ParentsLabel.Size.Height;
        _lvParentsListControlHeight = lvParentsList.Size.Height;

        LoadRevisionInfo();
    }

    private void Form_Shown(object? sender, EventArgs e)
    {
        if (lvParentsList.Visible)
        {
            lvParentsList.Focus();
        }
        else
        {
            AutoCommit.Focus();
        }
    }

    private void LoadRevisionInfo()
    {
        try
        {
            SuspendLayout();

            commitSummaryUserControl1.Revision = Revision;

            lvParentsList.Items.Clear();

            _isMerge = Module.IsMerge(Revision.ObjectId);

            // We need to hide these optional components first to get a correct base value of PreferredMinimumHeight
            ParentsLabel.Visible = false;
            lvParentsList.Visible = false;

            if (_isMerge)
            {
                MinimumSize = new Size(MinimumSize.Width,
                    PreferredMinimumHeight + _parentsLabelControlHeight + _lvParentsListControlHeight);
                Size = MinimumSize;
                ParentsLabel.Visible = true;
                lvParentsList.Visible = true;
            }
            else
            {
                MinimumSize = new Size(MinimumSize.Width,
                    PreferredMinimumHeight - _parentsLabelControlHeight - _lvParentsListControlHeight);
                Size = MinimumSize;
            }

            if (_isMerge)
            {
                var parents = Module.GetParentRevisions(Revision.ObjectId);

                for (var i = 0; i < parents.Count; i++)
                {
                    lvParentsList.Items.Add(new ListViewItem((i + 1).ToString())
                    {
                        SubItems =
                        {
                            parents[i].Subject, parents[i].Author, parents[i].CommitDate.ToShortDateString()
                        }
                    });
                }

                lvParentsList.TopItem!.Selected = true;
                var size = MinimumSize;
                size.Height += DpiUtil.Scale(_parentsListItemHeight * parents.Count);
                Size = size;
                MinimumSize = size;
            }
        }
        finally
        {
            ResumeLayout(true);
        }
    }

    private void Revert_Click(object sender, EventArgs e)
    {
        var parentIndex = 0;
        if (_isMerge)
        {
            if (lvParentsList.SelectedItems.Count != 1)
            {
                MessageBoxes.Show(this, _noneParentSelectedText.Text, TranslatedStrings.Error, MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            parentIndex = lvParentsList.SelectedItems[0].Index + 1;
        }

        CommitMessageManager commitMessageManager = new(this, Module.WorkingDirGitDir, Module.CommitEncoding);

        var existingCommitMessage =
            ThreadHelper.JoinableTaskFactory.Run(() => commitMessageManager.GetMergeOrCommitMessageAsync());

        var command = Commands.Revert(Revision.ObjectId, AutoCommit.Checked, parentIndex);

        // Don't verify whether the command is successful.
        // If it fails, likely there is a conflict that needs to be resolved.
        FormProcess.ShowDialog(this, UiCommands, command, Module.WorkingDir, null, true);

        if (!string.IsNullOrWhiteSpace(existingCommitMessage))
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                try
                {
                    await TaskScheduler.Default;

                    var message = await commitMessageManager.GetMergeOrCommitMessageAsync();
                    var newCommitMessageContent = $"{existingCommitMessage}\n\n{message}";
                    await commitMessageManager.WriteCommitMessageToFileAsync(newCommitMessageContent,
                        CommitMessageType.Merge,
                        usingCommitTemplate: false, ensureCommitMessageSecondLineEmpty: false);
                }
                catch (Exception)
                {
                }
            });
        }

        MergeConflictHandler.HandleMergeConflicts(UiCommands, this, AutoCommit.Checked);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void lvParentsList_Resize(object sender, EventArgs e)
    {
        lvParentsList.Columns[1].Width = lvParentsList.ClientSize.Width - lvParentsList.Columns[0].Width -
                                         lvParentsList.Columns[2].Width - lvParentsList.Columns[3].Width;
    }

    private void btnAbort_Click(object sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
    }
}
