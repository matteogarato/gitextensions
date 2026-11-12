using System.Diagnostics;
using GitExtensions.Extensibility.Git;
using GitUI.LeftPanel.Interfaces;
using GitUI.Properties;

namespace GitUI.LeftPanel;

[DebuggerDisplay("(Tag) FullPath = {FullPath}, Hash = {ObjectId}, Visible: {Visible}")]
internal sealed class TagNode : BaseRevisionNode, IGitRefActions, ICanDelete
{
    public TagNode(Tree tree, in ObjectId objectId, string fullPath, bool visible)
        : base(tree, fullPath, visible)
    {
        ObjectId = objectId;
    }

    bool ICanDelete.Delete()
    {
        return UiCommands.StartDeleteTagDialog(TreeViewNode.TreeView, FullPath);
    }

    bool IGitRefActions.CreateBranch()
    {
        return UiCommands.StartCreateBranchDialog(TreeViewNode.TreeView, ObjectId);
    }

    bool IGitRefActions.Merge()
    {
        return UiCommands.StartMergeBranchDialog(TreeViewNode.TreeView, FullPath);
    }

    bool IGitRefActions.Checkout()
    {
        return UiCommands.StartCheckoutRevisionDialog(TreeViewNode.TreeView, FullPath);
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

    internal override void OnDoubleClick()
    {
        CreateBranch();
    }

    internal override void OnDelete()
    {
        Delete();
    }

    public override void ApplyStyle()
    {
        base.ApplyStyle();

        TreeViewNode.ImageKey = TreeViewNode.SelectedImageKey =
            Visible
                ? nameof(Images.TagHorizontal)
                : nameof(Images.EyeClosed);
    }
}
