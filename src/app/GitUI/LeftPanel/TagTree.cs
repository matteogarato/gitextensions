using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitUI.UserControls.RevisionGrid;

namespace GitUI.LeftPanel;

internal sealed class TagTree(TreeNode treeNode, IGitUiCommandsSource uiCommands, ICheckRefs refsSource)
    : BaseRefTree(treeNode, uiCommands, refsSource, RefsFilter.Tags)
{
    protected override Nodes FillTree(IReadOnlyList<IGitRef> tags, CancellationToken token)
    {
        Nodes nodes = new(this);
        Dictionary<string, BaseRevisionNode> pathToNodes = [];

        foreach (var tag in tags)
        {
            token.ThrowIfCancellationRequested();

            TagNode tagNode = new(this, tag.ObjectId, tag.Name, true);
            var parent =
                tagNode.CreateRootNode(pathToNodes, (tree, parentPath) => new BasePathNode(tree, parentPath));

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
}
