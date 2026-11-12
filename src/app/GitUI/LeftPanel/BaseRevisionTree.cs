using GitExtUtils.GitUI;
using GitUI.UserControls.RevisionGrid;

namespace GitUI.LeftPanel;

internal abstract class BaseRevisionTree(TreeNode treeNode, IGitUiCommandsSource uiCommands, ICheckRefs refsSource)
    : Tree(treeNode, uiCommands)
{
    protected readonly ICheckRefs RefsSource = refsSource;
    private readonly ExclusiveTaskRunner _updateTaskRunner = ThreadHelper.CreateExclusiveTaskRunner();

    public override void Dispose()
    {
        _updateTaskRunner.Dispose();
        base.Dispose();
    }

    protected override void OnDetached()
    {
        _updateTaskRunner.CancelCurrent();
        base.OnDetached();
    }

    internal virtual void UpdateVisibility()
    {
        var treeView = TreeViewNode.TreeView;

        if (treeView is null || !IsAttached)
        {
            return;
        }

        _updateTaskRunner.RunDetached(async cancellationToken =>
        {
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
            await LoadingCompleted.Task;
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks

            await treeView.SwitchToMainThreadAsync(cancellationToken);

            // Check again after switch to main thread
            treeView = TreeViewNode.TreeView;

            if (treeView is null || !IsAttached)
            {
                return;
            }

            foreach (var node in Nodes.DepthEnumerator<BaseRevisionNode>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (node.ObjectId.IsZero)
                {
                    continue;
                }

                var isVisible = RefsSource.Contains(node.ObjectId);
                if (node.Visible != isVisible)
                {
                    node.Visible = isVisible;
                    node.ApplyStyle();
                }
            }
        });
    }
}
