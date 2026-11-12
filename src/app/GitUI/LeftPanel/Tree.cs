using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitUI.UserControls;
using Microsoft.VisualStudio.Threading;

namespace GitUI.LeftPanel;

internal abstract class Tree : NodeBase, IDisposable
{
    private readonly ExclusiveTaskRunner _reloadTaskRunner = ThreadHelper.CreateExclusiveTaskRunner();
    private readonly IGitUiCommandsSource _uiCommandsSource;
    protected TaskCompletionSource LoadingCompleted = new();
    private bool _firstReloadNodesSinceModuleChanged = true;

    protected Tree(TreeNode treeNode, IGitUiCommandsSource uiCommands)
    {
        Nodes = new Nodes(this);
        _uiCommandsSource = uiCommands;
        TreeViewNode = treeNode;
        treeNode.Tag = this;

        uiCommands.UiCommandsChanged += (a, e) =>
        {
            // When GitModule has changed, clear selected node
            if (TreeViewNode?.TreeView is not null)
            {
                TreeViewNode.TreeView.SelectedNode = null;
            }

            // Certain operations need to happen the first time after we change modules. For example,
            // we don't want to use the expanded/collapsed state of existing nodes in the tree, but at
            // the same time, we don't want to remove them from the tree as this is visible to the user,
            // as well as less efficient.
            _firstReloadNodesSinceModuleChanged = true;
        };
    }

    public IGitUiCommands UiCommands
    {
        get => _uiCommandsSource.UiCommands;
    }

    /// <summary>
    ///     A flag to indicate that node SelectionChanged event is not user-originated and
    ///     must not trigger the event handling sequence.
    /// </summary>
    public bool IgnoreSelectionChangedEvent { get; set; }

    protected IGitModule Module
    {
        get => UiCommands.Module;
    }

    /// <summary>
    ///     Flag if this tree is enabled or invisible.
    /// </summary>
    protected bool IsAttached { get; private set; }

    public virtual void Dispose()
    {
        Detached();
        _reloadTaskRunner.Dispose();
    }

    public void Attached()
    {
        IsAttached = true;
        OnAttached();
    }

    protected virtual void OnAttached()
    {
    }

    public void Detached()
    {
        _reloadTaskRunner.CancelCurrent();
        IsAttached = false;
        OnDetached();
    }

    protected virtual void OnDetached()
    {
    }

    public void ClearTree()
    {
        TreeViewNode.Nodes.Clear();
    }

    public IEnumerable<TNode> DepthEnumerator<TNode>() where TNode : NodeBase
    {
        return Nodes.DepthEnumerator<TNode>();
    }

    internal IEnumerable<NodeBase> GetNodesAndSelf()
    {
        return DepthEnumerator<NodeBase>().Prepend(this);
    }

    internal IEnumerable<NodeBase> GetSelectedNodes()
    {
        return GetNodesAndSelf().Where(node => node.IsSelected);
    }

    // Invoke from child class to reload nodes for the current Tree. Clears Nodes, invokes
    // input async function that should populate Nodes, then fills the tree view with its contents,
    // making sure to disable/enable the control.
    protected JoinableTask ReloadNodesDetached(
        Func<Func<RefsFilter, IReadOnlyList<IGitRef>>, CancellationToken, Task<Nodes>> loadNodesTask,
        Func<RefsFilter, IReadOnlyList<IGitRef>> getRefs)
    {
        var treeView = TreeViewNode.TreeView;

        return _reloadTaskRunner.RunDetached(async cancellationToken =>
        {
            if (treeView is null || !IsAttached)
            {
                return;
            }

            try
            {
                LoadingCompleted = new TaskCompletionSource();

                // Module is invalid in Dashboard
                var newNodes = Module.IsValidGitWorkingDir()
                    ? await loadNodesTask(getRefs, cancellationToken)
                    : new Nodes(null);

                await treeView.SwitchToMainThreadAsync(cancellationToken);

                // Check again after switch to main thread
                treeView = TreeViewNode.TreeView;

                if (treeView is null || !IsAttached)
                {
                    return;
                }

                // remember multi-selected nodes
                HashSet<int> multiSelected = [.. GetSelectedNodes().Select(node => node.GetHashCode())];

                Nodes.Clear();
                Nodes.AddNodes(newNodes);

                // re-apply multi-selection
                if (multiSelected.Count > 0)
                {
                    foreach (var node in GetNodesAndSelf()
                                 .Where(node => multiSelected.Contains(node.GetHashCode())))
                    {
                        node.IsSelected = true;
                    }
                }

                try
                {
                    var originalSelectedNodeFullNamePath = treeView.SelectedNode?.GetFullNamePath();

                    treeView.BeginUpdate();
                    IgnoreSelectionChangedEvent = true;
                    FillTreeViewNode(originalSelectedNodeFullNamePath, _firstReloadNodesSinceModuleChanged);
                }
                finally
                {
                    IgnoreSelectionChangedEvent = false;
                    treeView.EndUpdate();
                    ExpandPathToSelectedNode();
                    _firstReloadNodesSinceModuleChanged = false;
                }
            }
            finally
            {
                LoadingCompleted.TrySetResult();
            }
        });
    }

    private void FillTreeViewNode(string? originalSelectedNodeFullNamePath, bool firstTime)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var expandedNodesState = firstTime ? [] : TreeViewNode.GetExpandedNodesState();
        Nodes.FillTreeViewNode(TreeViewNode);

        var selectedNode = TreeViewNode.TreeView!.SelectedNode;

        if (originalSelectedNodeFullNamePath != selectedNode?.GetFullNamePath()
            && TreeViewNode.GetNodeFromPath(originalSelectedNodeFullNamePath) is { } node)
        {
            TreeViewNode.TreeView.SelectedNode = node.Tag is not BaseRevisionNode branchNode || branchNode.Visible
                ? node
                : null;
        }

        PostFillTreeViewNode(firstTime);

        TreeViewNode.RestoreExpandedNodesState(expandedNodesState);
    }

    // Called after the TreeView has been populated from Nodes. A good place to update properties
    // of the TreeViewNode, such as it's name (TreeViewNode.Text), Expand/Collapse state, and
    // to set selected node (TreeViewNode.TreeView.SelectedNode).
    protected virtual void PostFillTreeViewNode(bool firstTime)
    {
    }

    private void ExpandPathToSelectedNode()
    {
        if (TreeViewNode.TreeView!.Nodes.Count == 0)
        {
            return;
        }

        // If no selected node, just make sure that the first node is visible
        var node = TreeViewNode.TreeView.SelectedNode ?? TreeViewNode.TreeView.Nodes[0];
        node.EnsureVerticallyVisible();
    }
}
