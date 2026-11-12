using GitCommands.Git;
using GitCommands.Remotes;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils.GitUI;
using GitUI.UserControls.RevisionGrid;

namespace GitUI.LeftPanel;

internal sealed class RemoteBranchTree(
    TreeNode treeNode,
    IGitUiCommandsSource uiCommands,
    IAheadBehindDataProvider? aheadBehindDataProvider,
    ICheckRefs refsSource)
    : BaseRefTree(treeNode, uiCommands, refsSource, RefsFilter.Remotes)
{
    protected override Nodes FillTree(IReadOnlyList<IGitRef> branches, CancellationToken token)
    {
        // More than one local can point to a single remote branch, pick one of them.
        Nodes nodes = new(this);
        Dictionary<string, BaseRevisionNode> pathToNodes = [];
        IDictionary<string, AheadBehindData>? aheadBehindData = aheadBehindDataProvider?.GetData()
            ?.DistinctBy(r => r.Value.RemoteRef).ToDictionary(r => r.Value.RemoteRef, r => r.Value);

        List<RemoteRepoNode> enabledRemoteRepoNodes = [];
        var remoteByName =
            ThreadHelper.JoinableTaskFactory.Run(Module.GetRemotesAsync).ToDictionary(r => r.Name);

        ConfigFileRemoteSettingsManager remotesManager = new(() => Module);

        // Create nodes for enabled remotes with branches
        foreach (var branch in PrioritizedBranches(branches))
        {
            token.ThrowIfCancellationRequested();

            if (branch.ObjectId.IsZero)
            {
                throw new InvalidOperationException($"Branch '{branch.Name}' has no ObjectId.");
            }

            var remoteName = branch.Name.SubstringUntil('/');
            if (remoteByName.TryGetValue(remoteName, out var remote))
            {
                RemoteBranchNode remoteBranchNode = new(this, branch.ObjectId, branch.Name, true);
                if (aheadBehindData?.TryGetValue(branch.CompleteName, out var aheadBehind) is true)
                {
                    remoteBranchNode.UpdateAheadBehind(aheadBehind.ToDisplay(reverse: true),
                        $"{GitRefName.RefsHeadsPrefix}{aheadBehind.Branch}");
                }

                var parent = remoteBranchNode.CreateRootNode(
                    pathToNodes,
                    (tree, parentPath) => CreateRemoteBranchPathNode(tree, parentPath, remote));

                if (parent is not null)
                {
                    enabledRemoteRepoNodes.Add((RemoteRepoNode)parent);
                }
            }
        }

        // Create nodes for enabled remotes without branches
        var enabledRemotesNoBranches = GetEnabledRemoteNamesWithoutBranches(branches, remoteByName);
        foreach (var remoteName in enabledRemotesNoBranches)
        {
            if (remoteByName.TryGetValue(remoteName, out var remote))
            {
                RemoteRepoNode node = new(this, remoteName, remotesManager, remote, true);
                enabledRemoteRepoNodes.Add(node);
            }
        }

        // Add enabled remote nodes in order
        foreach (var node in PrioritizedRemotes(enabledRemoteRepoNodes))
        {
            nodes.AddNode(node);
        }

        // Add disabled remotes, if any
        var disabledRemotes = remotesManager.GetDisabledRemotes();
        if (disabledRemotes.Count > 0)
        {
            List<RemoteRepoNode> disabledRemoteRepoNodes = [];
            foreach (var remote in disabledRemotes)
            {
                RemoteRepoNode node = new(this, remote.Name, remotesManager, remote, false);
                disabledRemoteRepoNodes.Add(node);
            }

            RemoteRepoFolderNode disabledFolderNode = new(this, TranslatedStrings.Inactive);
            foreach (var node in PrioritizedRemotes(disabledRemoteRepoNodes))
            {
                disabledFolderNode.Nodes.AddNode(node);
            }

            nodes.AddNode(disabledFolderNode);
        }

        return nodes;

        BaseRevisionNode CreateRemoteBranchPathNode(Tree tree, string parentPath, Remote remote)
        {
            if (parentPath == remote.Name)
            {
                return new RemoteRepoNode(tree, parentPath, remotesManager, remote, true);
            }

            return new BasePathNode(tree, parentPath);
        }

        IReadOnlyList<string> GetEnabledRemoteNamesWithoutBranches(IReadOnlyList<IGitRef> branches,
            Dictionary<string, Remote> remoteByName)
        {
            HashSet<string> remotesWithBranches = [.. branches.Select(branch => branch.Name.SubstringUntil('/'))];

            HashSet<string> allRemotes = [.. remoteByName.Select(kv => kv.Value.Name)];

            return allRemotes.Except(remotesWithBranches).ToList();
        }
    }

    protected override void PostFillTreeViewNode(bool firstTime)
    {
        if (firstTime)
        {
            TreeViewNode.Expand();
        }
    }

    internal void PopupManageRemotesForm(string? remoteName)
    {
        UiCommands.StartRemotesDialog(TreeViewNode.TreeView, remoteName);
    }

    internal bool FetchAll()
    {
        UiCommands.StartPullDialogAndPullImmediately(
            out var pullCompleted,
            TreeViewNode.TreeView,
            pullAction: GitPullAction.FetchAll);
        return pullCompleted;
    }

    internal bool FetchPruneAll()
    {
        UiCommands.StartPullDialogAndPullImmediately(
            out var pullCompleted,
            TreeViewNode.TreeView,
            pullAction: GitPullAction.FetchPruneAll);
        return pullCompleted;
    }
}
