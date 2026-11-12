using GitExtensions.Extensibility;

namespace GitUI.UserControls;

public static class TreeViewExtensions
{
    public static void EnsureVerticallyVisible(this TreeNode? node)
    {
        if (node is null)
        {
            return;
        }

        if (node.TreeView is not { } treeView)
        {
            DebugHelpers.Fail(
                @$"{nameof(EnsureVerticallyVisible)}: Node ""{node.Text}"" does not belong to a TreeView.");
            return;
        }

        node.EnsureVisible();

        // EnsureVisible leads to horizontal scrolling in some cases. We make sure to force horizontal scroll back to 0.
        treeView.ScrollLeftMost();
    }

    public static void ExpandTopDownTo(this TreeView? treeView, TreeNode node)
    {
        if (treeView is null)
        {
            return;
        }

        List<TreeNode> parents = [];
        AddParents(parents, node, treeView.Nodes);
        foreach (var parent in parents)
        {
            parent.Expand();
        }

        return;

        static bool AddParents(List<TreeNode> parentsOfNode, TreeNode node, TreeNodeCollection nodes)
        {
            var actualNodes = GetActualNodes(nodes);
            foreach (var parent in actualNodes)
            {
                if (parent == node)
                {
                    return true;
                }

                if (parent.Nodes.Count == 0)
                {
                    continue;
                }

                var found = AddParents(parentsOfNode, node, parent.Nodes);
                if (found)
                {
                    parentsOfNode.Insert(0, parent);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    ///     Returns a set of expanded node paths to be used with RestoreExpandedNodeState starting from the input node.
    ///     This function makes use of GetFullNamePath, rather than TreeNode.FullPath, so you can vary the node's Text,
    ///     as long as the node's Name remains stable.
    /// </summary>
    public static HashSet<string> GetExpandedNodesState(this TreeNode node)
    {
        HashSet<string> result = [];
        node.DoGetExpandedNodesState(result);
        return result;
    }

    /// <summary>
    ///     Similar to TreeNode.FullPath, this function returns a full path made up of TreeNode.Name rather
    ///     than TreeNode.Text, if the former is non-empty. This is useful as it allows the node's Text to
    ///     be changed, while the node's Name can remain a key for operations such as getting and restoring
    ///     the expanded node state.
    /// </summary>
    public static string GetFullNamePath(this TreeNode node)
    {
        var sep = node.TreeView is not null ? node.TreeView.PathSeparator : "\\";

        var result = GetNameOrText(node);
        var currNode = node;
        while (currNode.Parent is not null)
        {
            currNode = currNode.Parent;
            result = GetNameOrText(currNode) + sep + result;
        }

        return result;

        static string GetNameOrText(TreeNode n)
        {
            return n.Name.Length > 0 ? n.Name : n.Text;
        }
    }

    public static TreeNode? GetNodeFromPath(this TreeNode node, string? path)
    {
        if (node.GetFullNamePath() == path)
        {
            return node;
        }

        foreach (TreeNode childNode in node.Nodes)
        {
            if (childNode.GetNodeFromPath(path) is { } foundNode)
            {
                return foundNode;
            }
        }

        return null;
    }

    public static IEnumerable<TreeNode> Items(this TreeView? treeView)
    {
        return treeView is null ? [] : Recurse(treeView.Nodes);
    }

    public static IEnumerable<TreeNode> Items(this TreeNode? node)
    {
        if (node is null)
        {
            yield break;
        }

        yield return node;

        foreach (var subNode in Recurse(node.Nodes))
        {
            yield return subNode;
        }
    }

    public static IEnumerable<T> ItemTags<T>(this TreeView? treeView) where T : class
    {
        return treeView.Items().ItemTags<T>();
    }

    public static IEnumerable<T> ItemTags<T>(this TreeNode? node) where T : class
    {
        return node.Items().ItemTags<T>();
    }

    /// <summary>
    ///     Restores the expanded state of nodes under the input node using the set returned by GetExpandedNodesState.
    /// </summary>
    public static void RestoreExpandedNodesState(this TreeNode node, HashSet<string> expandedNodes)
    {
        foreach (var path in expandedNodes)
        {
            var foundNode = node.GetNodeFromPath(path);
            foundNode?.Expand();
        }
    }

    public static void ScrollLeftMost(this TreeView? treeView)
    {
        // Reading Handle creates the window if there is none, which throws for a disposed control and
        // is pointless for one which was not shown yet - it has nothing to scroll.
        if (treeView?.IsHandleCreated is true)
        {
            NativeMethods.SendMessageW(treeView.Handle, NativeMethods.WmHscroll, (IntPtr)NativeMethods.Sbh.Left,
                IntPtr.Zero);
        }
    }

    public static IEnumerable<T> SelectedItemTags<T>(this MultiSelectTreeView? treeView) where T : class
    {
        return treeView is null ? [] : treeView.SelectedNodes.ItemTags<T>();
    }

    private static void DoGetExpandedNodesState(this TreeNode node, HashSet<string> expandedNodes)
    {
        if (node.IsExpanded)
        {
            expandedNodes.Add(node.GetFullNamePath());
        }

        foreach (TreeNode childNode in node.Nodes)
        {
            childNode.DoGetExpandedNodesState(expandedNodes);
        }
    }

    private static IEnumerable<TreeNode> GetActualNodes(TreeNodeCollection nodes)
    {
        return nodes.Count == 1 && nodes[0].Tag is TreeNode[] actualNodes
            ? actualNodes
            : nodes.Cast<TreeNode>();
    }

    /// <summary>
    ///     Returns the Tag of the nodes which can be casted to T - without iterating subnodes.
    /// </summary>
    private static IEnumerable<T> ItemTags<T>(this IEnumerable<TreeNode> nodes) where T : class
    {
        return nodes.Select(node => node.Tag as T).Where(value => value is not null).Cast<T>();
    }

    private static IEnumerable<TreeNode> Recurse(TreeNodeCollection nodes)
    {
        foreach (var node in GetActualNodes(nodes))
        {
            foreach (var treeNode in node.Items())
            {
                yield return treeNode;
            }
        }
    }
}
