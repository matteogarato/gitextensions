using System.Diagnostics;
using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitCommands.Submodules;
using GitExtensions.Extensibility;
using GitExtensions.Extensibility.Git;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;

namespace GitUI.LeftPanel;

internal sealed class SubmoduleTree : Tree
{
    private readonly ISubmoduleStatusProvider _submoduleStatusProvider;
    private Nodes? _currentNodes;
    private SubmoduleStatusEventArgs? _currentSubmoduleInfo;

    public SubmoduleTree(TreeNode treeNode, IGitUiCommandsSource commandsSource)
        : base(treeNode, commandsSource)
    {
        _submoduleStatusProvider = UiCommands.GetRequiredService<ISubmoduleStatusProvider>();
        _submoduleStatusProvider.StatusUpdating += Provider_StatusUpdating;
        _submoduleStatusProvider.StatusUpdated += Provider_StatusUpdated;
    }

    public override void Dispose()
    {
        base.Dispose();

        _submoduleStatusProvider.StatusUpdating -= Provider_StatusUpdating;
        _submoduleStatusProvider.StatusUpdated -= Provider_StatusUpdated;
    }

    private void Provider_StatusUpdating(object? sender, EventArgs e)
    {
        _currentNodes = null;
    }

    private void Provider_StatusUpdated(object? sender, SubmoduleStatusEventArgs e)
    {
        _currentSubmoduleInfo = e;

        if (IsAttached)
        {
            OnStatusUpdated(e);
        }
    }

    private void OnStatusUpdated(SubmoduleStatusEventArgs e)
    {
        TreeViewNode.TreeView!.InvokeAndForget(async () =>
        {
            CancellationTokenSource? cts = null;
            Task<Nodes>? loadNodesTask = null;

            if (e.StructureUpdated)
            {
                _currentNodes = null;
            }

            if (_currentNodes is not null)
            {
                // Structure is up-to-date, update status
                var infos =
                    e.Info.AllSubmodules.ToDictionary(info => info.Path, info => info);
                Validates.NotNull(e.Info.TopProject);
                infos[e.Info.TopProject.Path] = e.Info.TopProject;
                List<SubmoduleNode> nodes = [.. _currentNodes.DepthEnumerator<SubmoduleNode>()];

                foreach (var node in nodes)
                {
                    if (infos.TryGetValue(node.Info.Path, out var info))
                    {
                        node.Info = info;
                        infos.Remove(node.Info.Path);
                    }
                    else
                    {
                        // structure no longer matching
                        DebugHelpers.Assert(true,
                            $"Status info with {1 + e.Info.AllSubmodules.Count} records do not match current nodes ({nodes.Count})");
                        _currentNodes = null;
                        break;
                    }
                }

                if (infos.Count > 0)
                {
                    // This normally occurs with illegal paths
                    Trace.WriteLine(
                        $"{infos.Count} status info records remains after matching current nodes, structure seem to mismatch ({nodes.Count}/{e.Info.AllSubmodules.Count}: {string.Join(",", infos.Keys.ToList())})");

                    _currentNodes = null;
                }
            }

            if (_currentNodes is null)
            {
                // Load the nodes in the tree
                // Module.GetRefs() is not used for submodules
                var joinableTask = ReloadNodesDetached((_, token) =>
                    {
                        cts = CancellationTokenSource.CreateLinkedTokenSource(e.Token, token);
                        loadNodesTask = LoadNodesAsync(e.Info, cts.Token);
                        return loadNodesTask;
                    },
                    null!);
                await joinableTask.JoinAsync(e.Token);
            }

            if (cts is not null && loadNodesTask is not null)
            {
                _currentNodes = await loadNodesTask;
            }

            if (_currentNodes is not null)
            {
                var token = cts?.Token ?? e.Token;
                try
                {
                    await LoadNodeDetailsAsync(_currentNodes, token).ConfigureAwaitRunInline();
                    LoadNodeToolTips(_currentNodes, token);
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                }
            }

            Interlocked.CompareExchange(ref _currentSubmoduleInfo, null, e);
        });
    }

    private async Task<Nodes> LoadNodesAsync(SubmoduleInfoResult info, CancellationToken token)
    {
        await TaskScheduler.Default;
        token.ThrowIfCancellationRequested();

        return FillSubmoduleTree(info);
    }

    private async Task LoadNodeDetailsAsync(Nodes loadedNodes, CancellationToken token)
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);

        if (TreeViewNode.TreeView is not null)
        {
            TreeViewNode.TreeView.BeginUpdate();

            try
            {
                loadedNodes.DepthEnumerator<SubmoduleNode>().ForEach(node => node.RefreshDetails());
            }
            finally
            {
                TreeViewNode.TreeView.EndUpdate();
            }
        }
    }

    private void LoadNodeToolTips(Nodes loadedNodes, CancellationToken token)
    {
        if (TreeViewNode.TreeView is null)
        {
            return;
        }

        loadedNodes.DepthEnumerator<SubmoduleNode>()
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            .ForEach(async node =>
            {
                try
                {
                    await node.SetStatusToolTipAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                //// Comment out to debug BugReporter
                ////catch (GitExtUtils.ExternalOperationException)
                ////{
                ////}
            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
    }

    protected override void PostFillTreeViewNode(bool firstTime)
    {
        if (firstTime)
        {
            TreeViewNode.ExpandAll();
        }
    }

    private Nodes FillSubmoduleTree(SubmoduleInfoResult result)
    {
        Validates.NotNull(result.TopProject);

        var threadModule = (GitModule?)result.Module;

        Validates.NotNull(threadModule);

        List<SubmoduleNode> submoduleNodes = [];

        // We always want to display submodules rooted from the top project.
        CreateSubmoduleNodes(result, threadModule, ref submoduleNodes);

        return AddTopAndNodesToTree(submoduleNodes, threadModule, result);
    }

    private void CreateSubmoduleNodes(SubmoduleInfoResult result, IGitModule threadModule,
        ref List<SubmoduleNode> nodes)
    {
        // result.OurSubmodules/AllSubmodules contain a recursive list of submodules, but don't provide info about the super
        // project path. So we deduce these by substring matching paths against an ordered list of all paths.
        List<string> modulePaths = [.. result.AllSubmodules.Select(info => info.Path)];

        // Add current and parent module paths
        var parentModule = threadModule;

        while (parentModule is not null)
        {
            modulePaths.Add(parentModule.WorkingDir);
            parentModule = parentModule.SuperprojectModule;
        }

        // Sort descending so we find the nearest outer folder first
        modulePaths = [.. modulePaths.OrderByDescending(path => path)];

        foreach (var submoduleInfo in result.AllSubmodules)
        {
            var superPath = GetSubmoduleSuperPath(submoduleInfo.Path);

            if (!Directory.Exists(superPath))
            {
                MessageBoxes.SubmoduleDirectoryDoesNotExist(null, superPath ?? submoduleInfo.Path, submoduleInfo.Text);
                continue;
            }

            var localPath = Path.GetDirectoryName(submoduleInfo.Path[superPath.Length..]).ToPosixPath();

            var isCurrent = submoduleInfo.Bold;

            nodes.Add(new SubmoduleNode(this,
                submoduleInfo,
                isCurrent,
                isCurrent ? result.CurrentSubmoduleStatus : null,
                localPath!,
                superPath));
        }

        return;

        string? GetSubmoduleSuperPath(string submodulePath)
        {
            return modulePaths.Find(path => submodulePath != path && submodulePath.Contains(path));
        }
    }

    private static string GetNodeRelativePath(IGitModule topModule, SubmoduleNode node)
    {
        return node.SuperPath.SubstringAfter(topModule.WorkingDir).ToPosixPath() + node.LocalPath;
    }

    private Nodes AddTopAndNodesToTree(
        List<SubmoduleNode> submoduleNodes,
        IGitModule threadModule,
        SubmoduleInfoResult result)
    {
        // Create tree of SubmoduleFolderNode for each path directory and add input SubmoduleNodes as leaves.

        // Example of (SuperPath + LocalPath).ToPosixPath() for all nodes:
        //
        // C:/code/gitextensions2/Externals/conemu-inside
        // C:/code/gitextensions2/Externals/Git.hub
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor/gitextensions
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor/gitextensions/Externals/conemu-inside
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor/gitextensions/Externals/Git.hub
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor/gitextensions/Externals/ICSharpCode.TextEditor
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor/gitextensions/Externals/NBug
        // C:/code/gitextensions2/Externals/ICSharpCode.TextEditor/gitextensions/GitExtensionsDoc
        // C:/code/gitextensions2/Externals/NBug
        // C:/code/gitextensions2/GitExtensionsDoc
        //
        // What we want to do is first remove the topModule portion, "C:/code/gitextensions2/", and
        // then build our tree by breaking up each path into parts, separated by '/'.
        //
        // Note that when we break up the paths, some parts are just directories, the others are submodule nodes:
        //
        // Externals / ICSharpCode.TextEditor / gitextensions / Externals / Git.hub
        //  folder          submodule             submodule      folder     submodule
        //
        // Input 'nodes' is an array of SubmoduleNodes for all the submodules; now we need to create SubmoduleFolderNodes
        // and insert everything into a tree.

        var topModule = threadModule.GetTopModule();

        // Build a mapping of top-module-relative path to node
        Dictionary<string, Node> pathToNodes = [];

        // Add existing SubmoduleNodes
        foreach (var node in submoduleNodes)
        {
            pathToNodes[GetNodeRelativePath(topModule, node)] = node;
        }

        // Create and add missing SubmoduleFolderNodes
        foreach (var node in submoduleNodes)
        {
            var parts = GetNodeRelativePath(topModule, node).Split(Delimiters.ForwardSlash);

            for (var i = 0; i < parts.Length - 1; ++i)
            {
                var path = string.Join("/", parts.Take(i + 1));

                if (!pathToNodes.ContainsKey(path))
                {
                    pathToNodes[path] = new SubmoduleFolderNode(this, parts[i]);
                }
            }
        }

        // Add top-module node
        Validates.NotNull(result.TopProject);
        SubmoduleNode topModuleNode = new(
            this,
            result.TopProject,
            result.TopProject.Bold,
            result.TopProject.Bold ? result.CurrentSubmoduleStatus : null,
            "",
            result.TopProject.Path);

        // Now build the tree
        HashSet<Node> nodesInTree = [];
        foreach (var node in submoduleNodes)
        {
            Node parentNode = topModuleNode;
            var parts = GetNodeRelativePath(topModule, node).Split(Delimiters.ForwardSlash);

            for (var i = 0; i < parts.Length; ++i)
            {
                var path = string.Join("/", parts.Take(i + 1));
                var nodeToAdd = pathToNodes[path];

                // If node is not already in the tree, add it
                if (!nodesInTree.Contains(nodeToAdd))
                {
                    parentNode.Nodes.AddNode(nodeToAdd);
                    nodesInTree.Add(nodeToAdd);
                }

                parentNode = nodeToAdd;
            }
        }

        // Compact chains of single-child folder nodes for a cleaner display
        CompactSingleChildFolderChains(topModuleNode.Nodes);

        Nodes nodes = new(this);
        nodes.AddNode(topModuleNode);

        return nodes;

        static void CompactSingleChildFolderChains(Nodes nodes)
        {
            foreach (var node in nodes)
            {
                if (node is SubmoduleFolderNode folderNode)
                {
                    folderNode.CompactSingleChildFolders();
                }

                CompactSingleChildFolderChains(node.Nodes);
            }
        }
    }

    public void UpdateSubmodule(IWin32Window owner, SubmoduleNode node)
    {
        UiCommands.StartUpdateSubmoduleDialog(owner, node.LocalPath, node.SuperPath);
    }

    public void OpenSubmodule(SubmoduleNode node)
    {
        node.Open();
    }

    public void OpenSubmoduleInGitExtensions(SubmoduleNode node)
    {
        node.LaunchGitExtensions();
    }

    public void ManageSubmodules(IWin32Window owner)
    {
        UiCommands.StartSubmodulesDialog(owner);
    }

    public void SynchronizeSubmodules(IWin32Window owner)
    {
        UiCommands.StartSyncSubmodulesDialog(owner);
    }

    public void ResetSubmodule(IWin32Window owner, SubmoduleNode node)
    {
        var resetType = FormResetChanges.ShowResetDialog(owner, true, true);

        if (resetType == FormResetChanges.ActionEnum.Cancel)
        {
            return;
        }

        GitModule module = new(UiCommands.GetRequiredService<IGitExecutorProvider>(), node.Info.Path);
        module.ResetAllChanges(clean: resetType == FormResetChanges.ActionEnum.ResetAndDelete);
    }

    public void StashSubmodule(IWin32Window owner, SubmoduleNode node)
    {
        var uiCmds = UiCommands.WithWorkingDirectory(node.Info.Path);
        uiCmds.StashSave(owner, AppSettings.IncludeUntrackedFilesInManualStash);
    }

    public void CommitSubmodule(IWin32Window owner, SubmoduleNode node)
    {
        var submodulCommands = UiCommands.WithWorkingDirectory(node.Info.Path.EnsureTrailingPathSeparator());
        submodulCommands.StartCommitDialog(owner);
    }
}
