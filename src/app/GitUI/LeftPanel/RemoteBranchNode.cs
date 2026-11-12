using System.Diagnostics;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.LeftPanel.Interfaces;
using GitUI.Properties;

namespace GitUI.LeftPanel;

[DebuggerDisplay("(Remote) FullPath = {FullPath}, Hash = {ObjectId}, Visible: {Visible}")]
internal sealed class RemoteBranchNode(Tree tree, in ObjectId objectId, string fullPath, bool visible)
    : BaseBranchLeafNode(tree, objectId, fullPath, visible, nameof(Images.BranchRemote),
        nameof(Images.BranchRemoteMerged)), IGitRefActions, ICanDelete
{
    bool ICanDelete.Delete()
    {
        var remoteBranchInfo = GetRemoteBranchInfo();
        return UiCommands.StartDeleteRemoteBranchDialog(TreeViewNode.TreeView,
            remoteBranchInfo.Remote + '/' + remoteBranchInfo.BranchName);
    }

    bool IGitRefActions.CreateBranch()
    {
        return UiCommands.StartCreateBranchDialog(TreeViewNode.TreeView, FullPath);
    }

    bool IGitRefActions.Checkout()
    {
        return MessageBoxes.ConfirmBranchCheckout(ParentWindow(), FullPath) &&
               UiCommands.StartCheckoutRemoteBranch(TreeViewNode.TreeView, FullPath);
    }

    bool IGitRefActions.Merge()
    {
        return UiCommands.StartMergeBranchDialog(TreeViewNode.TreeView, FullPath);
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

    public bool Fetch()
    {
        var remoteBranchInfo = GetRemoteBranchInfo();
        UiCommands.StartPullDialogAndPullImmediately(
            out var pullCompleted,
            TreeViewNode.TreeView,
            remoteBranchInfo.BranchName,
            remoteBranchInfo.Remote,
            GitPullAction.Fetch);
        return pullCompleted;
    }

    private RemoteBranchInfo GetRemoteBranchInfo()
    {
        var remote = FullPath.LazySplit('/').First();
        var branch = FullPath[(remote.Length + 1)..];
        return new RemoteBranchInfo(remote, branch);
    }

    internal override void OnDelete()
    {
        Delete();
    }

    internal override void OnDoubleClick()
    {
        Checkout();
    }

    public bool FetchAndMerge()
    {
        return Fetch() && Merge();
    }

    public bool FetchAndCheckout()
    {
        return Fetch() && Checkout();
    }

    public bool FetchAndCreateBranch()
    {
        return Fetch() && CreateBranch();
    }

    public bool FetchAndRebase()
    {
        return Fetch() && Rebase();
    }

    private readonly struct RemoteBranchInfo(string remote, string branchName)
    {
        public string Remote { get; } = remote;
        public string BranchName { get; } = branchName;
    }
}
