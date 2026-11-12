using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.LeftPanel.Interfaces;
using GitUI.Properties;

namespace GitUI.LeftPanel;

[DebuggerDisplay("(Local) FullPath = {FullPath}, Hash = {ObjectId}, Visible: {Visible}")]
internal sealed class LocalBranchNode(Tree tree, in ObjectId objectId, string fullPath, bool isCurrent, bool visible)
    : BaseBranchLeafNode(tree, objectId, fullPath, visible, nameof(Images.BranchLocal),
        nameof(Images.BranchLocalMerged)), IGitRefActions, ICanRename, ICanDelete
{
    /// <summary>Indicates whether this is the currently checked-out branch.</summary>
    public bool IsCurrent { get; } = isCurrent;

    bool ICanDelete.Delete()
    {
        return UiCommands.StartDeleteBranchDialog(ParentWindow(), FullPath);
    }

    bool ICanRename.Rename()
    {
        return UiCommands.StartRenameDialog(ParentWindow(), FullPath);
    }

    bool IGitRefActions.Checkout()
    {
        return MessageBoxes.ConfirmBranchCheckout(ParentWindow(), FullPath) &&
               UiCommands.StartCheckoutBranch(ParentWindow(), FullPath, false);
    }

    bool IGitRefActions.CreateBranch()
    {
        return UiCommands.StartCreateBranchDialog(ParentWindow(), FullPath);
    }

    bool IGitRefActions.Merge()
    {
        return UiCommands.StartMergeBranchDialog(ParentWindow(), FullPath);
    }

    protected override FontStyle GetFontStyle()
    {
        return base.GetFontStyle() | (IsCurrent ? FontStyle.Bold : FontStyle.Regular);
    }

    public override bool Equals(object? obj)
    {
        return base.Equals(obj) && obj is LocalBranchNode;
    }

    public override int GetHashCode()
    {
        return base.GetHashCode();
    }

    internal override void OnDoubleClick()
    {
        Checkout();
    }

    internal override void OnSelected()
    {
        if (Tree.IgnoreSelectionChangedEvent)
        {
            return;
        }

        base.OnSelected();
        SelectRevision();
    }

    internal override void OnRename()
    {
        Rename();
    }

    internal override void OnDelete()
    {
        Delete();
    }
}
