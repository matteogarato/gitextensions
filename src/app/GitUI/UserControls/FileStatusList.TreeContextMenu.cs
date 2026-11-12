using GitUI.Properties;

namespace GitUI.UserControls;

partial class FileStatusList
{
    private readonly ToolStripMenuItem _collapseAll = new("C&ollapse all", Images.CollapseAll);
    private readonly ToolStripMenuItem _collapseRootFolders = new("Collap&se root folders", Images.TreeCollapseAll);
    private readonly ToolStripMenuItem _expandAll = new("E&xpand all", Images.ExpandAll);
    private readonly ToolStripMenuItem _selectAll = new("S&elect all", Images.FileTree);
    private readonly ToolStripSeparator _treeContextMenuSeparator = new() { Name = nameof(_treeContextMenuSeparator) };

    private void CreateTreeContextMenuItems()
    {
        _selectAll.Click += SelectAll_Click;
        _collapseAll.Click += CollapseAll_Click;
        _expandAll.Click += ExpandAll_Click;
        _collapseRootFolders.Click += CollapseRootFolders_Click;
    }

    private void InsertTreeContextMenuItems(ToolStripItemCollection items, int index)
    {
        if (items.Find(_treeContextMenuSeparator.Name!, false).Length > 0)
        {
            return;
        }

        items.Insert(index++, _selectAll);
        items.Insert(index++, _collapseAll);
        items.Insert(index++, _expandAll);
        items.Insert(index++, _treeContextMenuSeparator);
        items.Add(_collapseRootFolders);
    }

    private void UpdateStatusOfTreeContextMenuItems()
    {
        var hasSubnodes = FileStatusListView.SelectedNodes.Any(node => node.Nodes.Count > 0);

        _collapseAll.Visible = hasSubnodes;
        _expandAll.Visible = hasSubnodes;
        _selectAll.Visible = hasSubnodes;
        _treeContextMenuSeparator.Visible = hasSubnodes;

        _collapseRootFolders.Visible =
            _isFileTreeMode && FileStatusListView.Nodes.Cast<TreeNode>().Any(node => node.IsExpanded);
    }

    private void CollapseAll_Click(object? sender, EventArgs e)
    {
        foreach (var node in FileStatusListView.SelectedNodes)
        {
            node.Collapse(false);
        }
    }

    private void CollapseRootFolders_Click(object? sender, EventArgs e)
    {
        if (FileStatusListView.FocusedNode?.Parent is { } parent)
        {
            while (parent.Parent is not null)
            {
                parent = parent.Parent;
            }

            FileStatusListView.SelectedNode = parent;
        }

        foreach (TreeNode node in FileStatusListView.Nodes)
        {
            node.Collapse(true);
        }
    }

    private void ExpandAll_Click(object? sender, EventArgs e)
    {
        foreach (var node in FileStatusListView.SelectedNodes)
        {
            ExpandAll(node);
        }
    }

    private void SelectAll_Click(object? sender, EventArgs e)
    {
        HashSet<TreeNode> selectedItems = [];
        foreach (var node in FileStatusListView.SelectedNodes)
        {
            ExpandAll(node);
            foreach (var leaf in node.Items().Where(node => node.Tag is FileStatusItem))
            {
                selectedItems.Add(leaf);
            }
        }

        FileStatusListView.SetSelectedNodes(selectedItems, FileStatusListView.FocusedNode);
    }
}
