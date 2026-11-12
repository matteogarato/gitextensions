using GitCommands.Settings;
using GitUI.UserControls.RevisionGrid;
using Microsoft.VisualStudio.Threading;
using GitRevision = GitExtensions.Extensibility.Git.GitRevision;

namespace GitUI.LeftPanel;

internal sealed class StashTree(TreeNode treeNode, IGitUiCommandsSource uiCommands, ICheckRefs refsSource)
    : BaseRevisionTree(treeNode, uiCommands, refsSource)
{
    internal void Refresh(Lazy<IReadOnlyCollection<GitRevision>> getStashRevs)
    {
        if (!IsAttached)
        {
            return;
        }

        ReloadNodesDetached((_, cancellationToken) => LoadNodesAsync(getStashRevs, cancellationToken), null!);
    }

    private async Task<Nodes> LoadNodesAsync(Lazy<IReadOnlyCollection<GitRevision>> getStashRevs,
        CancellationToken token)
    {
        await TaskScheduler.Default;
        token.ThrowIfCancellationRequested();

        return FillStashTree(getStashRevs.Value, token);
    }

    private Nodes FillStashTree(IReadOnlyCollection<GitRevision> stashes, CancellationToken token)
    {
        Nodes nodes = new(this);
        Dictionary<string, BaseRevisionNode> pathToNodes = [];

        foreach (var stash in stashes)
        {
            token.ThrowIfCancellationRequested();

            // Visibility is set after the grid is loaded
            StashNode node = new(this, stash.ObjectId, stash.ReflogSelector!, stash.Subject, false);
            Node? parent = node.CreateRootNode(pathToNodes, (tree, parentPath) => new BasePathNode(tree, parentPath));

            if (parent is not null)
            {
                nodes.AddNode(parent);
            }
        }

        return nodes;
    }

    protected override void PostFillTreeViewNode(bool firstTime)
    {
        if (firstTime)
        {
            TreeViewNode.Collapse();
        }
    }

    public void StashAll(IWin32Window owner)
    {
        UiCommands.StashSave(owner, AppSettings.IncludeUntrackedFilesInManualStash);
    }

    public void StashStaged(IWin32Window owner)
    {
        UiCommands.StashStaged(owner);
    }

    public void OpenStash(IWin32Window owner)
    {
        UiCommands.StartStashDialog(owner);
    }
}
