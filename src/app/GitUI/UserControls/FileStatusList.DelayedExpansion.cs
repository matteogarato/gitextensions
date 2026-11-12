namespace GitUI.UserControls;

partial class FileStatusList
{
    private void ExpandAll(TreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        RestoreChildrenOfFolderNodes(node.Items(), () =>
        {
            node.ExpandAll();
            node.EnsureVerticallyVisible();
            if (node.TreeView is MultiSelectTreeView treeView)
            {
                treeView.FocusedNode = node;
            }
        });
    }

    private void FileStatusListView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is { } node)
        {
            RestoreChildrenOfFolderNodes([node], delayExpansion: true);
        }
    }

    private static void ReplaceChildrenOfFolderNodesWithPlaceholder(IEnumerable<TreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.Nodes.Count == 0)
            {
                continue;
            }

            var children = new TreeNode[node.Nodes.Count];
            node.Nodes.CopyTo(children, 0);
            TreeNode placeholder = new() { Tag = children };

            node.Nodes.Clear();
            node.Nodes.Add(placeholder);
        }
    }

    /// <summary>
    ///     Replaces possible placeholders with the actual children.
    /// </summary>
    /// <param name="nodes">The nodes which are prepared for expansion.</param>
    /// <param name="afterAction">An optional action which is performed before exiting Begin/EndUpdate.</param>
    /// <param name="delayExpansion">If <c>true</c>, the subchildren are replaced with placeholders.</param>
    private void RestoreChildrenOfFolderNodes(IEnumerable<TreeNode> nodes, Action? afterAction = null,
        bool delayExpansion = false)
    {
        var nodesWithPlaceholder = GetNodesWithPlaceHolder(nodes);
        if (nodesWithPlaceholder.Count == 0 && afterAction is null)
        {
            return;
        }

        try
        {
            FileStatusListView.BeforeExpand -= FileStatusListView_BeforeExpand;
            FileStatusListView.BeginUpdate();

            var children = RestoreAndGetAllChildren(nodesWithPlaceholder, delayExpansion);
            LoadFileIcons(children, default);

            afterAction?.Invoke();
        }
        finally
        {
            FileStatusListView.EndUpdate();
            FileStatusListView.BeforeExpand += FileStatusListView_BeforeExpand;
        }

        return;

        static List<(TreeNode Parent, TreeNode[] Children)> GetNodesWithPlaceHolder(IEnumerable<TreeNode> nodes)
        {
            List<(TreeNode Parent, TreeNode[] Children)> nodesWithPlaceholder = [];
            foreach (var node in nodes)
            {
                if (node.Nodes.Count == 1 && node.Nodes[0].Tag is TreeNode[] children)
                {
                    nodesWithPlaceholder.Add((node, children));
                }
            }

            return nodesWithPlaceholder;
        }

        static IEnumerable<TreeNode> RestoreAndGetAllChildren(
            List<(TreeNode Parent, TreeNode[] Children)> nodesWithPlaceholder, bool delayExpansion)
        {
            foreach ((var parent, var children) in nodesWithPlaceholder)
            {
                RestoreChildren(parent, children, delayExpansion);

                foreach (var child in children)
                {
                    foreach (var subchild in child.Items())
                    {
                        yield return subchild;
                    }
                }
            }
        }

        static void RestoreChildren(TreeNode parent, TreeNode[] children, bool delayExpansion)
        {
            if (delayExpansion)
            {
                ReplaceChildrenOfFolderNodesWithPlaceholder(children);
            }

            parent.Nodes.Clear();
            parent.Nodes.AddRange(children);
        }
    }
}
