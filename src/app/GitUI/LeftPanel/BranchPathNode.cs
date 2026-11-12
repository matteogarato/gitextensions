using System.Diagnostics;

namespace GitUI.LeftPanel;

[DebuggerDisplay("(Branch path) FullPath = {FullPath}")]
internal sealed class BranchPathNode(Tree tree, string fullPath) : BasePathNode(tree, fullPath)
{
    public override string ToString()
    {
        return $"{Name}{PathSeparator}";
    }

    public void DeleteAll()
    {
        var branches = Nodes.DepthEnumerator<LocalBranchNode>().Select(branch => branch.FullPath);
        UiCommands.StartDeleteBranchDialog(ParentWindow(), branches);
    }

    public void CreateBranch()
    {
        var newBranchNamePrefix = FullPath + PathSeparator;
        UiCommands.StartCreateBranchDialog(ParentWindow(), default, newBranchNamePrefix);
    }
}
