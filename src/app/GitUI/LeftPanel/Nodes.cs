using System.Collections;

namespace GitUI.LeftPanel;

internal sealed class Nodes(Tree? tree) : IReadOnlyCollection<Node>
{
    private readonly List<Node> _nodesList = [];

    public Node? LastNode
    {
        get => _nodesList.Count > 0 ? _nodesList[^1] : null;
    }

    public Tree? Tree { get; } = tree;

    public Node this[int index] => _nodesList[index];

    int IReadOnlyCollection<Node>.Count
    {
        get => _nodesList.Count;
    }

    /// <summary>
    ///     Adds a new node to the collection.
    /// </summary>
    /// <param name="node">The node to add.</param>
    public void AddNode(Node node)
    {
        _nodesList.Add(node);
    }

    public void AddNodes(IEnumerable<Node> nodes)
    {
        _nodesList.AddRange(nodes);
    }

    public void Clear()
    {
        _nodesList.Clear();
    }

    /// <summary>
    ///     This function is responsible for building the TreeNode structure that matches this Nodes's
    ///     structure, recursively. To avoid needlessly recreating TreeNodes, it recycles existing ones
    ///     if they are considered equal, so it's important to implement Equals on Node classes.
    /// </summary>
    internal void FillTreeViewNode(TreeNode treeViewNode)
    {
        HashSet<Node> prevNodes = [];

        for (var i = 0; i < treeViewNode.Nodes.Count; i++)
        {
            prevNodes.Add(Node.GetNode(treeViewNode.Nodes[i]));
        }

        var oldNodeIdx = 0;

        foreach (var node in this)
        {
            TreeNode treeNode;

            if (oldNodeIdx < treeViewNode.Nodes.Count)
            {
                treeNode = treeViewNode.Nodes[oldNodeIdx];
                var oldNode = Node.GetNode(treeNode);

                if (!oldNode.Equals(node) && !prevNodes.Contains(node))
                {
                    treeNode = treeViewNode.Nodes.Insert(oldNodeIdx, string.Empty);
                }
            }
            else
            {
                treeNode = treeViewNode.Nodes.Add(string.Empty);
            }

            node.TreeViewNode = treeNode;
            node.Nodes.FillTreeViewNode(treeNode);
            oldNodeIdx++;
        }

        while (oldNodeIdx < treeViewNode.Nodes.Count)
        {
            treeViewNode.Nodes.RemoveAt(oldNodeIdx);
        }
    }

    public void InsertNode(int index, Node node)
    {
        _nodesList.Insert(index, node);
    }

    #region Enumerators

    public List<Node>.Enumerator GetEnumerator()
    {
        return _nodesList.GetEnumerator();
    }

    IEnumerator<Node> IEnumerable<Node>.GetEnumerator()
    {
        return _nodesList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     Returns all nodes of a given TNode type using depth-first, pre-order method.
    /// </summary>
    public IEnumerable<TNode> DepthEnumerator<TNode>() where TNode : NodeBase
    {
        foreach (var node in this)
        {
            if (node is TNode node1)
            {
                yield return node1;
            }

            foreach (var subNode in node.Nodes.DepthEnumerator<TNode>())
            {
                yield return subNode;
            }
        }
    }

    #endregion
}
