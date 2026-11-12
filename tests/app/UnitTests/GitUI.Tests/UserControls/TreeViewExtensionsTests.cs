using GitUI.UserControls;

namespace GitUITests.UserControls;

public class TreeViewExtensionsTests : IDisposable
{
    private readonly TreeNode _a = null!;
    private readonly TreeNode _b = null!;
    private readonly TreeNode _b1 = null!;
    private readonly TreeNode _b2 = null!;
    private readonly TreeNode _b21 = null!;
    private readonly TreeNode _b3 = null!;
    private readonly TreeNode _c = null!;
    private readonly TreeNode _root = null!;
    private readonly TreeView _treeView = null!;

    public TreeViewExtensionsTests()
    {
        _treeView = new TreeView();

        _root = _treeView.Nodes.Add("Root");

        _a = _root.Nodes.Add("A");
        _b = _root.Nodes.Add("B");
        _c = _root.Nodes.Add("C");

        _b1 = _b.Nodes.Add("B_1");
        _b2 = _b.Nodes.Add("B_2");
        _b3 = _b.Nodes.Add("B_3");

        _b21 = _b2.Nodes.Add("B_2_1");
    }

    void IDisposable.Dispose()
    {
        _treeView.Dispose();
    }

    [Fact]
    public void GetExpandedNodesState_should_return_no_expanded_nodes()
    {
        var expandedNodes = _root.GetExpandedNodesState();
        expandedNodes.Count.Should().Be(0);
    }

    [Fact]
    public void GetExpandedNodesState_should_return_one_expanded_node()
    {
        _b.Expand();
        var expandedNodes = _root.GetExpandedNodesState();
        expandedNodes.Count.Should().Be(1);
        expandedNodes.Contains(@"Root\B").Should().Be(true);
    }

    [Fact]
    public void GetExpandedNodesState_should_return_all_expanded_nodes()
    {
        _root.ExpandAll();
        var expandedNodes = _root.GetExpandedNodesState();
        expandedNodes.Count.Should().Be(8);
    }

    [Fact]
    public void RestoreExpandedNodesState_should_restore_no_nodes()
    {
        HashSet<string> expandedNodes = [];
        _root.RestoreExpandedNodesState(expandedNodes);

        var expandedNodesPost = _root.GetExpandedNodesState();
        expandedNodesPost.Count.Should().Be(0);
    }

    [Fact]
    public void RestoreExpandedNodesState_should_restore_one_node()
    {
        HashSet<string> expandedNodes = [@"Root\B"];
        _root.RestoreExpandedNodesState(expandedNodes);

        var expandedNodesPost = _root.GetExpandedNodesState();
        expandedNodesPost.Count.Should().Be(1);
        expandedNodes.Contains(@"Root\B").Should().Be(true);
    }

    [Fact]
    public void RestoreExpandedNodesState_should_restore_all_nodes()
    {
        HashSet<string> expandedNodes =
        [
            $"{_root.GetFullNamePath()}",
            $"{_a.GetFullNamePath()}",
            $"{_b.GetFullNamePath()}",
            $"{_b1.GetFullNamePath()}",
            $"{_b2.GetFullNamePath()}",
            $"{_b21.GetFullNamePath()}",
            $"{_b3.GetFullNamePath()}",
            $"{_c.GetFullNamePath()}"
        ];

        _root.RestoreExpandedNodesState(expandedNodes);

        var expandedNodesPost = _root.GetExpandedNodesState();
        expandedNodesPost.Count.Should().Be(8);
        expandedNodesPost.Should().BeEquivalentTo(expandedNodes);
    }

    [Fact]
    public void GetFullNamePath_should_return_node_names_if_set()
    {
        // Only override the names for 2 of the 4 nodes
        _b.Name = "B_Name";
        _b21.Name = "B_2_1_Name";
        _b21.GetFullNamePath().Should().Be(@"Root\B_Name\B_2\B_2_1_Name");
    }

    [StaFact]
    public void ScrollLeftMost_should_not_throw_for_a_disposed_treeView()
    {
        TreeView treeView = new();
        _ = treeView.Handle;
        treeView.Dispose();

        ((Action)(() => treeView.ScrollLeftMost())).Should().NotThrow();
    }

    [StaFact]
    public void ScrollLeftMost_should_not_create_the_handle()
    {
        using TreeView treeView = new();

        treeView.ScrollLeftMost();

        treeView.IsHandleCreated.Should().BeFalse();
    }

    [Fact]
    public void ScrollLeftMost_should_not_throw_for_null()
    {
        ((Action)(() => ((TreeView?)null).ScrollLeftMost())).Should().NotThrow();
    }
}
