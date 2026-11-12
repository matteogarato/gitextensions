using GitUI.Properties;

namespace GitUI.LeftPanel;

internal class BasePathNode(Tree tree, string fullPath) : BaseRevisionNode(tree, fullPath, true)
{
    public override void ApplyStyle()
    {
        base.ApplyStyle();

        TreeViewNode.ImageKey = TreeViewNode.SelectedImageKey =
            FullPath == TranslatedStrings.Inactive ? nameof(Images.EyeClosed) : nameof(Images.BranchFolder);
    }
}
