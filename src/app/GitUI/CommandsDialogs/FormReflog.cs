using System.Text.RegularExpressions;
using GitCommands.Git;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.HelperDialogs;
using Microsoft.VisualStudio.Threading;
using ResourceManager;

namespace GitUI.CommandsDialogs;

public partial class FormReflog : GitModuleForm
{
    private readonly TranslationString _continueResetCurrentBranchCaptionText = new("Changes not committed...");

    private readonly TranslationString _continueResetCurrentBranchEvenWithChangesText =
        new("You have changes in your working directory that could be lost.\n\nDo you want to continue?");

    private string? _currentBranch;
    private bool _isBranchCheckedOut;
    private bool _isDirtyDir;
    private int _lastHitRowIndex;

    public FormReflog(IGitUiCommands uiCommands)
        : base(uiCommands)
    {
        InitializeComponent();
        lblDirtyWorkingDirectory.SetForeColorForBackColor();
        InitializeComplete();

        gridReflog.RowTemplate.Height = DpiUtil.Scale(24);
        gridReflog.ColumnHeadersHeight = DpiUtil.Scale(30);

        Sha.DataPropertyName = nameof(RefLine.Sha);
        Ref.DataPropertyName = nameof(RefLine.Ref);
        Action.DataPropertyName = nameof(RefLine.Action);
    }

    [GeneratedRegex(@"^(?<sha>[^ ]+) (?<ref>[^:]+): (?<action>.+)$", RegexOptions.ExplicitCapture)]
    private static partial Regex ReflogRegex { get; }

    private void FormReflog_Load(object sender, EventArgs e)
    {
        _isDirtyDir = UiCommands.Module.IsDirtyDir();
        _currentBranch = UiCommands.Module.GetSelectedBranch();
        _isBranchCheckedOut = _currentBranch != DetachedHeadParser.DetachedBranch;
        linkCurrentBranch.Text = "current branch (" + _currentBranch + ")";
        linkCurrentBranch.Visible = _isBranchCheckedOut;
        _lastHitRowIndex = 0;
        lblDirtyWorkingDirectory.Visible = _isDirtyDir;
        resetCurrentBranchOnThisCommitToolStripMenuItem.Enabled = _isBranchCheckedOut;

        List<string> branches =
        [
            "HEAD",
            .. UiCommands.Module.GetRefs(RefsFilter.Heads).Select(r => r.Name).OrderBy(n => n),
            .. UiCommands.Module.GetRemoteBranches().Select(r => r.Name).OrderBy(n => n)
        ];
        Branches.DataSource = branches;
    }

    private void Branches_SelectedIndexChanged(object sender, EventArgs e)
    {
        ThreadHelper.JoinableTaskFactory.Run(DisplayRefLogAsync);

        async Task DisplayRefLogAsync()
        {
            var item = (string)Branches.SelectedItem!;
            await TaskScheduler.Default;
            GitArgumentBuilder arguments = new("reflog") { "--no-abbrev", item };
            var output = UiCommands.Module.GitExecutable.GetOutput(arguments);
            List<RefLine> refLines = [.. ConvertReflogOutput()];
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _lastHitRowIndex = 0;
            SortableRefLineList refLinesList = new();
            refLinesList.AddRange(refLines);
            gridReflog.DataSource = refLinesList;

            IEnumerable<RefLine> ConvertReflogOutput()
            {
                return from line in output.LazySplit('\n')
                    where line.Length != 0
                    select ReflogRegex.Match(line)
                    into match
                    where match.Success
                    select new RefLine(ObjectId.Parse(match.Groups["sha"].ValueSpan), match.Groups["ref"].Value,
                        match.Groups["action"].Value);
            }
        }
    }

    private void createABranchOnThisCommitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (gridReflog.SelectedCells.Count == 0 && gridReflog.SelectedRows.Count == 0)
        {
            return;
        }

        UiCommands.DoActionOnRepo(() =>
        {
            using FormCreateBranch form = new(UiCommands, GetShaOfRefLine());
            form.CheckoutAfterCreation = false;
            form.UserAbleToChangeRevision = false;
            form.CouldBeOrphan = false;
            return form.ShowDialog(this) == DialogResult.OK;
        });
    }

    private ObjectId GetShaOfRefLine()
    {
        var row = GetSelectedRow();
        var refLine = (RefLine)row.DataBoundItem!;
        return refLine.Sha;

        DataGridViewRow GetSelectedRow()
        {
            if (gridReflog.SelectedRows.Count > 0)
            {
                return gridReflog.SelectedRows[0];
            }

            if (gridReflog.SelectedCells.Count > 0)
            {
                return gridReflog.Rows[gridReflog.SelectedCells[0].RowIndex];
            }

            return gridReflog.CurrentRow!;
        }
    }

    private void resetCurrentBranchOnThisCommitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        if (_isDirtyDir)
        {
            if (MessageBoxes.Show(this, _continueResetCurrentBranchEvenWithChangesText.Text,
                    _continueResetCurrentBranchCaptionText.Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) ==
                DialogResult.No)
            {
                return;
            }
        }

        var gitRevision = UiCommands.Module.GetRevision(GetShaOfRefLine());
        var resetType =
            _isDirtyDir ? FormResetCurrentBranch.ResetType.Soft : FormResetCurrentBranch.ResetType.Hard;
        UiCommands.DoActionOnRepo(() =>
        {
            using var form = FormResetCurrentBranch.Create(UiCommands, gitRevision, resetType);
            return form.ShowDialog(this) == DialogResult.OK;
        });
    }

    private void copySha1ToolStripMenuItem_Click(object sender, EventArgs e)
    {
        ClipboardUtil.TrySetText(GetShaOfRefLine().ToString());
    }

    private void linkCurrentBranch_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
    {
        Branches.SelectedItem = _currentBranch;
    }

    private void linkHead_Click(object sender, EventArgs e)
    {
        Branches.SelectedIndex = 0;
    }

    private void gridReflog_MouseMove(object sender, MouseEventArgs e)
    {
        var hit = gridReflog.HitTest(e.X, e.Y);

        if (hit.Type == DataGridViewHitTestType.Cell && _lastHitRowIndex != hit.RowIndex)
        {
            if (_lastHitRowIndex < gridReflog.Rows.Count)
            {
                gridReflog.Rows[_lastHitRowIndex].Selected = false;
            }

            _lastHitRowIndex = hit.RowIndex;
            gridReflog.Rows[_lastHitRowIndex].Selected = true;
        }
    }

    private void gridReflog_MouseClick(object sender, MouseEventArgs e)
    {
        contextMenuStripReflog.Show((Control)sender, e.Location);
    }

    private sealed class SortableRefLineList : SortableBindingList<RefLine>
    {
        static SortableRefLineList()
        {
            AddSortableProperty(refLine => refLine.Sha, (x, y) => x.Sha.CompareTo(y.Sha));
            AddSortableProperty(refLine => refLine.Ref,
                (x, y) => string.Compare(x.Ref, y.Ref, StringComparison.Ordinal));
            AddSortableProperty(refLine => refLine.Action,
                (x, y) => string.Compare(x.Action, y.Action, StringComparison.CurrentCulture));
        }
    }
}

internal sealed class RefLine(ObjectId sha, string @ref, string action)
{
    public ObjectId Sha { get; set; } = sha;
    public string Ref { get; set; } = @ref;
    public string Action { get; set; } = action;
}
