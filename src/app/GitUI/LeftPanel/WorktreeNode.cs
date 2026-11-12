using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.Properties;

namespace GitUI.LeftPanel;

[DebuggerDisplay("(Worktree) Path = {Worktree.Path}, Branch = {Worktree.Branch}")]
internal sealed class WorktreeNode(Tree tree, GitWorktree worktree, bool isCurrent, string displayPath) : Node(tree)
{
    public GitWorktree Worktree { get; } = worktree;
    public bool IsCurrent { get; } = isCurrent;
    private string DisplayPath { get; } = displayPath;

    internal override void OnSelected()
    {
        if (Tree.IgnoreSelectionChangedEvent)
        {
            return;
        }

        if (Worktree.Sha1 is not null)
        {
            GoToRevision(Worktree.Sha1);
        }
    }

    internal override void OnDoubleClick()
    {
        if (!IsCurrent && !Worktree.IsDeleted)
        {
            OpenWorktree();
        }
    }

    public void OpenWorktree()
    {
        if (Worktree.IsDeleted)
        {
            return;
        }

        UiCommands.WorktreeSwitch(ParentWindow(), Worktree.Path);
    }

    public void DeleteWorktree()
    {
        if (UiCommands.WorktreeDelete(ParentWindow(), Worktree.Path))
        {
            ((WorktreeTree)Tree).Refresh();
        }
    }

    protected override FontStyle GetFontStyle()
    {
        return base.GetFontStyle() | (IsCurrent ? FontStyle.Bold : FontStyle.Regular);
    }

    public override void ApplyStyle()
    {
        base.ApplyStyle();

        if (Worktree.IsDeleted)
        {
            TreeViewNode.ForeColor = SystemColors.GrayText;
        }

        TreeViewNode.ImageKey = TreeViewNode.SelectedImageKey = nameof(Images.WorkTree);
        TreeViewNode.ToolTipText = GetToolTipText();

        return;

        string GetToolTipText()
        {
            var shortSha = Worktree.Sha1?.Length >= 7 ? Worktree.Sha1[..7] : Worktree.Sha1;

            var status = IsCurrent ? " (current)"
                : Worktree.IsDeleted ? " (deleted)"
                : "";

            var branchLine = Worktree.HeadType is GitWorktreeHeadType.Bare ? "bare"
                : Worktree.HeadType is GitWorktreeHeadType.Detached ? $"detached at {shortSha}"
                : Worktree.Branch ?? "unknown";

            return shortSha is not null
                ? $"{Worktree.Path}{status}\nBranch: {branchLine}\nHEAD: {shortSha}"
                : $"{Worktree.Path}{status}\nBranch: {branchLine}";
        }
    }

    protected override string DisplayText()
    {
        return Worktree.GetDisplayName(DisplayPath);
    }

    protected override string NodeName()
    {
        return Worktree.Path;
    }
}
